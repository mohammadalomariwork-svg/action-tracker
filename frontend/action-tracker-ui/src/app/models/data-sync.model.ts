export type DataSyncRunStatus = 'Running' | 'Succeeded' | 'Failed';
export type DataSyncTrigger   = 'Scheduled' | 'Manual';

export interface DataSyncJob {
  id: string;
  jobKey: string;
  name: string;
  description: string;
  cronExpression: string;
  timeZoneId?: string | null;
  isEnabled: boolean;
  scheduleDescription?: string | null;
  /** UTC ISO string; null when disabled / not scheduled. */
  nextRunAt?: string | null;
  isRunning: boolean;
  lastRunAt?: string | null;
  lastRunStatus?: DataSyncRunStatus | null;
  lastRunMessage?: string | null;
  lastRunRowCount?: number | null;
  lastRunDurationMs?: number | null;
  sourceSummary?: string | null;
  targetSummary?: string | null;
  hasHandler: boolean;
  createdAt: string;
  updatedAt?: string | null;
  updatedBy?: string | null;
}

export interface UpdateDataSyncJob {
  cronExpression: string;
  timeZoneId?: string | null;
  isEnabled: boolean;
}

export interface DataSyncJobRun {
  id: string;
  jobId: string;
  startedAt: string;
  finishedAt?: string | null;
  durationMs?: number | null;
  status: DataSyncRunStatus;
  trigger: DataSyncTrigger;
  triggeredBy?: string | null;
  rowsRead?: number | null;
  rowsWritten?: number | null;
  message?: string | null;
}

export interface DataSyncProbeResult {
  sourceReachable: boolean;
  sourceRowCount?: number | null;
  sourceError?: string | null;
  targetReachable: boolean;
  targetRowCount?: number | null;
  targetError?: string | null;
  sourceOnlyColumns: string[];
  targetOnlyColumns: string[];
  commonColumnCount: number;
  elapsedMs: number;
}

/** Common schedules offered in the UI; "custom" keeps whatever is typed. */
export interface CronPreset {
  key: string;
  label: string;
  cron: string;
}

export const CRON_PRESETS: CronPreset[] = [
  { key: 'every-15-min', label: 'Every 15 minutes', cron: '*/15 * * * *' },
  { key: 'every-30-min', label: 'Every 30 minutes', cron: '*/30 * * * *' },
  { key: 'hourly',       label: 'Every hour',       cron: '0 * * * *'    },
  { key: 'every-6h',     label: 'Every 6 hours',    cron: '0 */6 * * *'  },
  { key: 'daily-02',     label: 'Daily at 02:00',   cron: '0 2 * * *'    },
  { key: 'daily-06',     label: 'Daily at 06:00',   cron: '0 6 * * *'    },
  { key: 'custom',       label: 'Custom…',          cron: ''             },
];

export const TIME_ZONE_OPTIONS: { id: string; label: string }[] = [
  { id: '',                       label: 'UTC' },
  { id: 'Arabian Standard Time',  label: 'Gulf Standard Time (UTC+4, Abu Dhabi)' },
];
