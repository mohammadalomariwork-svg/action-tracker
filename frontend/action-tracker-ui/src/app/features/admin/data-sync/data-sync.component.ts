import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription, timer } from 'rxjs';
import { DataSyncService } from '../../../services/data-sync.service';
import {
  CRON_PRESETS,
  CronPreset,
  DataSyncJob,
  DataSyncJobRun,
  DataSyncProbeResult,
  TIME_ZONE_OPTIONS,
  UpdateDataSyncJob,
} from '../../../models/data-sync.model';
import { ToastService } from '../../../core/services/toast.service';
import { AdminBreadcrumbComponent } from '../components/shared/admin-breadcrumb/admin-breadcrumb.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { HasPermissionDirective } from '../../../shared';

/** Per-job editable state kept alongside the server snapshot. */
interface JobEdit {
  presetKey: string;
  cron: string;
  timeZoneId: string;
  isEnabled: boolean;
  saving: boolean;
  running: boolean;
  probing: boolean;
  probe: DataSyncProbeResult | null;
  showProbe: boolean;
  showHistory: boolean;
  historyLoading: boolean;
  runs: DataSyncJobRun[];
}

@Component({
  selector: 'app-data-sync',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    DecimalPipe,
    FormsModule,
    AdminBreadcrumbComponent,
    PageHeaderComponent,
    HasPermissionDirective,
  ],
  templateUrl: './data-sync.component.html',
  styleUrl: './data-sync.component.scss',
})
export class DataSyncComponent implements OnInit, OnDestroy {
  private readonly service = inject(DataSyncService);
  private readonly toast   = inject(ToastService);

  readonly presets   = CRON_PRESETS;
  readonly timeZones = TIME_ZONE_OPTIONS;

  readonly loading    = signal(true);
  readonly refreshing = signal(false);
  readonly jobs       = signal<DataSyncJob[]>([]);
  readonly edits      = signal<Record<string, JobEdit>>({});

  readonly anyRunning = computed(() => this.jobs().some(j => j.isRunning));
  readonly enabledCount = computed(() => this.jobs().filter(j => j.isEnabled).length);
  readonly failedCount  = computed(() => this.jobs().filter(j => j.lastRunStatus === 'Failed').length);

  private pollSub: Subscription | null = null;

  ngOnInit(): void {
    this.load();
  }

  ngOnDestroy(): void {
    this.stopPolling();
  }

  // ── Loading ────────────────────────────────────────────────────────────────

  load(silent = false): void {
    if (!silent) this.loading.set(true); else this.refreshing.set(true);

    this.service.getJobs().subscribe({
      next: res => {
        if (res.success) {
          this.jobs.set(res.data ?? []);
          this.syncEdits(res.data ?? [], /*preserveUserEdits*/ silent);
          this.refreshOpenHistories();
          this.anyRunning() ? this.startPolling() : this.stopPolling();
        } else {
          this.toast.error(res.message || 'Failed to load data sync jobs.');
        }
        this.loading.set(false);
        this.refreshing.set(false);
      },
      error: err => {
        this.toast.error(err?.error?.message || 'Failed to load data sync jobs.');
        this.loading.set(false);
        this.refreshing.set(false);
        this.stopPolling();
      },
    });
  }

  private syncEdits(jobs: DataSyncJob[], preserveUserEdits: boolean): void {
    const current = { ...this.edits() };
    for (const job of jobs) {
      const existing = current[job.id];
      if (existing && preserveUserEdits) {
        // keep the admin's unsaved edits, only refresh transient flags
        existing.running = job.isRunning;
        continue;
      }
      current[job.id] = {
        presetKey:      this.presetFor(job.cronExpression),
        cron:           job.cronExpression,
        timeZoneId:     job.timeZoneId ?? '',
        isEnabled:      job.isEnabled,
        saving:         false,
        running:        job.isRunning,
        probing:        existing?.probing ?? false,
        probe:          existing?.probe ?? null,
        showProbe:      existing?.showProbe ?? false,
        showHistory:    existing?.showHistory ?? false,
        historyLoading: false,
        runs:           existing?.runs ?? [],
      };
    }
    this.edits.set(current);
  }

  edit(job: DataSyncJob): JobEdit {
    return this.edits()[job.id];
  }

  isDirty(job: DataSyncJob): boolean {
    const e = this.edit(job);
    if (!e) return false;
    return e.cron.trim() !== job.cronExpression
        || (e.timeZoneId || '') !== (job.timeZoneId || '')
        || e.isEnabled !== job.isEnabled;
  }

  // ── Polling while a run is in progress ─────────────────────────────────────

  private startPolling(): void {
    if (this.pollSub) return;
    this.pollSub = timer(3000, 3000).subscribe(() => this.load(true));
  }

  private stopPolling(): void {
    this.pollSub?.unsubscribe();
    this.pollSub = null;
  }

  private refreshOpenHistories(): void {
    for (const job of this.jobs()) {
      const e = this.edit(job);
      if (e?.showHistory) this.loadHistory(job, true);
    }
  }

  // ── Schedule editing ───────────────────────────────────────────────────────

  presetFor(cron: string): string {
    const match = this.presets.find(p => p.cron && p.cron === cron.trim());
    return match ? match.key : 'custom';
  }

  onPresetChange(job: DataSyncJob, key: string): void {
    const e = this.edit(job);
    const preset: CronPreset | undefined = this.presets.find(p => p.key === key);
    e.presetKey = key;
    if (preset && preset.cron) e.cron = preset.cron;
  }

  onCronTyped(job: DataSyncJob): void {
    const e = this.edit(job);
    e.presetKey = this.presetFor(e.cron);
  }

  resetEdit(job: DataSyncJob): void {
    const e = this.edit(job);
    e.cron       = job.cronExpression;
    e.presetKey  = this.presetFor(job.cronExpression);
    e.timeZoneId = job.timeZoneId ?? '';
    e.isEnabled  = job.isEnabled;
  }

  save(job: DataSyncJob): void {
    const e = this.edit(job);
    const cron = e.cron.trim();
    if (!cron) {
      this.toast.error('Cron expression is required.');
      return;
    }

    const dto: UpdateDataSyncJob = {
      cronExpression: cron,
      timeZoneId:     e.timeZoneId ? e.timeZoneId : null,
      isEnabled:      e.isEnabled,
    };

    e.saving = true;
    this.service.updateJob(job.id, dto).subscribe({
      next: res => {
        e.saving = false;
        if (res.success) {
          this.replaceJob(res.data);
          this.toast.success(`"${job.name}" schedule saved.`);
        } else {
          this.toast.error(res.message || 'Failed to save schedule.');
        }
      },
      error: err => {
        e.saving = false;
        this.toast.error(err?.error?.message || 'Failed to save schedule.');
      },
    });
  }

  // ── Actions ────────────────────────────────────────────────────────────────

  runNow(job: DataSyncJob): void {
    const e = this.edit(job);
    e.running = true;
    this.service.runNow(job.id).subscribe({
      next: res => {
        if (res.success) {
          this.replaceJob({ ...res.data, isRunning: true });
          this.toast.info(res.message || 'Sync started.');
          this.startPolling();
        } else {
          e.running = false;
          this.toast.error(res.message || 'Failed to start sync.');
        }
      },
      error: err => {
        e.running = false;
        this.toast.error(err?.error?.message || 'Failed to start sync.');
      },
    });
  }

  probe(job: DataSyncJob): void {
    const e = this.edit(job);
    e.probing   = true;
    e.showProbe = true;
    this.service.probe(job.id).subscribe({
      next: res => {
        e.probing = false;
        if (res.success) {
          e.probe = res.data;
          const ok = res.data.sourceReachable && res.data.targetReachable;
          ok ? this.toast.success('Source and target are reachable.')
             : this.toast.warning('Connection test finished with problems. See details below.');
        } else {
          this.toast.error(res.message || 'Connection test failed.');
        }
      },
      error: err => {
        e.probing = false;
        this.toast.error(err?.error?.message || 'Connection test failed.');
      },
    });
  }

  toggleHistory(job: DataSyncJob): void {
    const e = this.edit(job);
    e.showHistory = !e.showHistory;
    if (e.showHistory) this.loadHistory(job);
  }

  loadHistory(job: DataSyncJob, silent = false): void {
    const e = this.edit(job);
    if (!silent) e.historyLoading = true;
    this.service.getRuns(job.id, 25).subscribe({
      next: res => {
        e.historyLoading = false;
        if (res.success) e.runs = res.data ?? [];
      },
      error: () => {
        e.historyLoading = false;
        if (!silent) this.toast.error('Failed to load run history.');
      },
    });
  }

  private replaceJob(updated: DataSyncJob): void {
    this.jobs.update(list => list.map(j => (j.id === updated.id ? updated : j)));
    const current = { ...this.edits() };
    const e = current[updated.id];
    if (e) {
      e.cron       = updated.cronExpression;
      e.presetKey  = this.presetFor(updated.cronExpression);
      e.timeZoneId = updated.timeZoneId ?? '';
      e.isEnabled  = updated.isEnabled;
      e.running    = updated.isRunning;
    }
    this.edits.set(current);
  }

  // ── Display helpers ────────────────────────────────────────────────────────

  statusClass(status?: string | null): string {
    switch (status) {
      case 'Succeeded': return 'bg-success';
      case 'Failed':    return 'bg-danger';
      case 'Running':   return 'bg-info text-dark';
      default:          return 'bg-secondary';
    }
  }

  statusIcon(status?: string | null): string {
    switch (status) {
      case 'Succeeded': return 'bi-check-circle-fill';
      case 'Failed':    return 'bi-x-circle-fill';
      case 'Running':   return 'bi-arrow-repeat';
      default:          return 'bi-dash-circle';
    }
  }

  duration(ms?: number | null): string {
    if (ms == null) return '—';
    if (ms < 1000) return `${ms} ms`;
    const s = ms / 1000;
    if (s < 60) return `${s.toFixed(1)} s`;
    const m = Math.floor(s / 60);
    return `${m}m ${(s - m * 60).toFixed(0)}s`;
  }

  trackJob = (_: number, j: DataSyncJob) => j.id;
  trackRun = (_: number, r: DataSyncJobRun) => r.id;
}
