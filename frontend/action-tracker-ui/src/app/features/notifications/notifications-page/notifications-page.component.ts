import { Component, OnInit, OnDestroy, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { formatDistanceToNow, isToday, isYesterday, isThisWeek } from 'date-fns';
import { NotificationService } from '../../../core/services/notification.service';
import { AppNotification } from '../../../core/models/notification.model';
import { ToastService } from '../../../core/services/toast.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { BreadcrumbComponent } from '../../../shared/components/breadcrumb/breadcrumb.component';

interface DateGroup {
  label: string;
  items: AppNotification[];
}

const NOTIFICATION_TYPES = [
  'ActionItem', 'Project', 'Milestone', 'Workspace',
  'Risk', 'Kpi', 'StrategicObjective', 'System',
];

const TYPE_ICONS: Record<string, string> = {
  ActionItem:         'bi-check2-square',
  Project:            'bi-folder',
  Milestone:          'bi-flag',
  Workspace:          'bi-building',
  Risk:               'bi-exclamation-triangle',
  Kpi:                'bi-graph-up',
  StrategicObjective: 'bi-bullseye',
  System:             'bi-info-circle',
};

const ACTION_COLORS: Record<string, string> = {
  Created:       '#0d6efd',
  Completed:     '#198754',
  Overdue:       '#dc3545',
  Escalated:     '#fd7e14',
  StatusChanged: '#6f42c1',
  Assigned:      '#20c997',
  Updated:       '#0d6efd',
  Deleted:       '#6c757d',
};

@Component({
  selector: 'app-notifications-page',
  standalone: true,
  imports: [CommonModule, FormsModule, ConfirmDialogComponent, PageHeaderComponent, BreadcrumbComponent],
  templateUrl: './notifications-page.component.html',
  styleUrl: './notifications-page.component.scss',
})
export class NotificationsPageComponent implements OnInit, OnDestroy {
  private readonly service = inject(NotificationService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  @ViewChild('clearConfirm') clearConfirm!: ConfirmDialogComponent;

  readonly types = NOTIFICATION_TYPES;

  notifications = signal<AppNotification[]>([]);
  unreadCount = signal(0);
  loading = signal(false);
  hasMore = signal(true);
  page = signal(1);
  readonly pageSize = 20;

  filterType = signal('');
  filterRead = signal<'' | 'unread' | 'read'>('');

  // track items being deleted for fade-out
  deletingIds = signal<Set<string>>(new Set());
  // track new items for slide-in
  newIds = signal<Set<string>>(new Set());

  private subs: Subscription[] = [];

  ngOnInit(): void {
    this.loadNotifications(true);

    this.subs.push(
      this.service.unreadCount$.subscribe(c => this.unreadCount.set(c)),
    );

    this.subs.push(
      this.service.newNotification$.subscribe(n => {
        // Only prepend if filter allows
        if (this.filterRead() === 'read') return;
        if (this.filterType() && n.type !== this.filterType()) return;

        this.newIds.update(s => new Set(s).add(n.id));
        this.notifications.update(list => [n, ...list]);
        setTimeout(() => {
          this.newIds.update(s => { const ns = new Set(s); ns.delete(n.id); return ns; });
        }, 300);
      }),
    );
  }

  ngOnDestroy(): void {
    this.subs.forEach(s => s.unsubscribe());
  }

  loadNotifications(reset: boolean): void {
    if (reset) {
      this.page.set(1);
      this.notifications.set([]);
      this.hasMore.set(true);
    }

    this.loading.set(true);

    const isRead = this.filterRead() === 'unread' ? false
                 : this.filterRead() === 'read' ? true
                 : undefined;
    const type = this.filterType() || undefined;

    this.service.getAll(this.page(), this.pageSize, isRead, type).subscribe({
      next: res => {
        if (res.success) {
          const items = res.data.items;
          if (reset) {
            this.notifications.set(items);
          } else {
            this.notifications.update(list => [...list, ...items]);
          }
          this.hasMore.set(res.data.hasNextPage);
        }
        this.loading.set(false);
      },
      error: () => {
        this.toast.error('Failed to load notifications.');
        this.loading.set(false);
      },
    });
  }

  loadMore(): void {
    this.page.update(p => p + 1);
    this.loadNotifications(false);
  }

  onFilterChange(): void {
    this.loadNotifications(true);
  }

  // ── Actions ──────────────────────────────────────────────

  clickNotification(n: AppNotification): void {
    if (!n.isRead) {
      this.service.markAsRead(n.id).subscribe();
      this.service.decrementUnread();
      n.isRead = true;
    }
    if (n.url) {
      this.router.navigateByUrl(n.url);
    }
  }

  markAsRead(n: AppNotification, event: Event): void {
    event.stopPropagation();
    if (n.isRead) return;
    this.service.markAsRead(n.id).subscribe({
      next: () => {
        n.isRead = true;
        this.service.decrementUnread();
      },
    });
  }

  markAsUnread(n: AppNotification, event: Event): void {
    event.stopPropagation();
    // Backend doesn't have a markAsUnread endpoint, so just toggle locally
    n.isRead = false;
    this.service.refreshUnreadCount();
  }

  deleteNotification(n: AppNotification, event: Event): void {
    event.stopPropagation();
    this.deletingIds.update(s => new Set(s).add(n.id));
    setTimeout(() => {
      this.service.delete(n.id).subscribe({
        next: () => {
          this.notifications.update(list => list.filter(x => x.id !== n.id));
          this.deletingIds.update(s => { const ns = new Set(s); ns.delete(n.id); return ns; });
          if (!n.isRead) this.service.decrementUnread();
          this.toast.success('Notification deleted.');
        },
        error: () => {
          this.deletingIds.update(s => { const ns = new Set(s); ns.delete(n.id); return ns; });
          this.toast.error('Failed to delete notification.');
        },
      });
    }, 200);
  }

  markAllRead(): void {
    this.service.markAllAsRead().subscribe({
      next: () => {
        this.notifications.update(list =>
          list.map(n => ({ ...n, isRead: true })),
        );
        this.service.clearUnread();
        this.toast.success('All notifications marked as read.');
      },
      error: () => this.toast.error('Failed to mark all as read.'),
    });
  }

  openClearConfirm(): void {
    this.clearConfirm.open();
  }

  onClearConfirmed(confirmed: boolean): void {
    if (!confirmed) return;
    this.service.deleteAllRead().subscribe({
      next: () => {
        this.notifications.update(list => list.filter(n => !n.isRead));
        this.toast.success('All read notifications cleared.');
      },
      error: () => this.toast.error('Failed to clear read notifications.'),
    });
  }

  // ── Grouping ─────────────────────────────────────────────

  get dateGroups(): DateGroup[] {
    const groups: Record<string, AppNotification[]> = {
      Today: [],
      Yesterday: [],
      'This Week': [],
      Earlier: [],
    };

    for (const n of this.notifications()) {
      const d = new Date(n.createdAt);
      if (isToday(d))          groups['Today'].push(n);
      else if (isYesterday(d)) groups['Yesterday'].push(n);
      else if (isThisWeek(d))  groups['This Week'].push(n);
      else                     groups['Earlier'].push(n);
    }

    return Object.entries(groups)
      .filter(([, items]) => items.length > 0)
      .map(([label, items]) => ({ label, items }));
  }

  // ── Helpers ──────────────────────────────────────────────

  getIcon(type: string): string {
    return TYPE_ICONS[type] ?? 'bi-bell';
  }

  getIconColor(actionType: string): string {
    return ACTION_COLORS[actionType] ?? '#6c757d';
  }

  relativeTime(dateStr: string): string {
    try {
      return formatDistanceToNow(new Date(dateStr), { addSuffix: true });
    } catch {
      return '';
    }
  }

  isDeleting(id: string): boolean {
    return this.deletingIds().has(id);
  }

  isNew(id: string): boolean {
    return this.newIds().has(id);
  }
}
