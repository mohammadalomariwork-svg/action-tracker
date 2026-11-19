import { ChartConfiguration, ChartEvent, ActiveElement } from 'chart.js';

// ─────────────────────────────────────────────────────────────────────────────
// Series colours shared by every dashboard.
//
// Canvas charts cannot read CSS variables, so these are fixed mid-tones that
// hold up on both the dark and the light surface. Blues come from the logo;
// green / amber / red are reserved for states that mean good / warning / bad.
// ─────────────────────────────────────────────────────────────────────────────
export const BRAND = { royal: '#2A5CF0', blue: '#4D8BFF', sky: '#7AADFF' };
export const TONE  = { good: '#10B981', warn: '#F59E0B', bad: '#EF4444', neutral: '#94A3B8', muted: '#64748B', violet: '#8B6CF6' };

/** Keyed by ActionStatus value. */
export const STATUS_COLOR: Record<string, string> = {
  '1': TONE.neutral,   // To Do
  '2': BRAND.blue,     // In Progress
  '3': TONE.violet,    // In Review
  '4': TONE.good,      // Done
  '5': TONE.bad,       // Overdue
  '6': TONE.warn,      // Deferred
  '7': TONE.muted,     // Cancelled
};

/** Keyed by ActionPriority value. */
export const PRIORITY_COLOR: Record<string, string> = {
  '4': TONE.bad, '3': TONE.warn, '2': BRAND.blue, '1': TONE.neutral,
};

export const DUE_COLOR: Record<string, string> = {
  overdue: TONE.bad, next7: TONE.warn, next30: BRAND.blue, later: BRAND.sky,
};

export const AGE_COLOR: Record<string, string> = {
  '0-7': BRAND.sky, '8-30': BRAND.blue, '31-90': TONE.warn, '90+': TONE.bad,
};

/** Project health and KPI health share one vocabulary. */
export const HEALTH_COLOR: Record<string, string> = {
  OnTrack: TONE.good, AtRisk: TONE.warn, Delayed: TONE.bad, OffTrack: TONE.bad,
  Completed: BRAND.blue, NotStarted: TONE.neutral, OnHold: TONE.violet,
  Cancelled: TONE.muted, NoData: TONE.neutral,
};

/** Keyed by ProjectStatus value. */
export const PROJECT_STATUS_COLOR: Record<string, string> = {
  '1': TONE.neutral, '2': BRAND.blue, '3': TONE.violet, '4': TONE.good, '5': TONE.muted, '6': TONE.warn,
};

// ─────────────────────────────────────────────────────────────────────────────
// Theme-aware chart options
// ─────────────────────────────────────────────────────────────────────────────

/** Resolve a CSS custom property to a concrete value for the canvas. */
export function cssVar(name: string, fallback: string): string {
  const v = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  return v || fallback;
}

function tooltipTheme() {
  return {
    backgroundColor: cssVar('--el', '#2B303B'),
    borderColor: cssVar('--bdh', 'rgba(160,185,255,0.26)'),
    borderWidth: 1,
    titleColor: cssVar('--t1', '#E6EBF5'),
    bodyColor: cssVar('--t2', '#AEBBD0'),
    padding: 10,
    cornerRadius: 8,
  };
}

/**
 * Click + hover behaviour for a chart whose segments drill down.
 * `pick` receives the index of the clicked element (and its dataset index).
 */
function interaction(pick?: (index: number, datasetIndex: number) => void) {
  if (!pick) return {};
  return {
    onClick: (_e: ChartEvent, els: ActiveElement[]) => {
      if (els.length) pick(els[0].index, els[0].datasetIndex);
    },
    onHover: (e: ChartEvent, els: ActiveElement[]) => {
      const target = e.native?.target as HTMLElement | undefined;
      if (target) target.style.cursor = els.length ? 'pointer' : 'default';
    },
  };
}

export function doughnutOptions(pick?: (index: number) => void): ChartConfiguration<'doughnut'>['options'] {
  return {
    responsive: true,
    maintainAspectRatio: false,
    cutout: '68%',
    plugins: {
      legend: { display: false },   // pages render an accessible, clickable legend instead
      tooltip: { ...tooltipTheme(), callbacks: { label: ctx => ` ${ctx.label}: ${ctx.parsed}` } },
    },
    ...interaction(pick),
  };
}

export interface BarOptions {
  horizontal?: boolean;
  stacked?: boolean;
  legend?: boolean;
  /** Suffix for value ticks/tooltips, e.g. '%'. */
  unit?: string;
  max?: number;
}

export function barOptions(
  pick?: (index: number, datasetIndex: number) => void, o: BarOptions = {},
): ChartConfiguration<'bar'>['options'] {
  const tick = cssVar('--t2', '#AEBBD0');
  const grid = cssVar('--bd', 'rgba(160,185,255,0.13)');
  const unit = o.unit ?? '';
  const valueAxis = {
    beginAtZero: true,
    stacked: !!o.stacked,
    max: o.max,
    ticks: { precision: 0, font: { size: 11 }, color: tick, callback: (v: string | number) => `${v}${unit}` },
    grid: { color: grid },
    border: { display: false },
  };
  const categoryAxis = {
    stacked: !!o.stacked,
    ticks: { font: { size: 11 }, color: tick },
    grid: { display: false },
    border: { display: false },
  };

  return {
    responsive: true,
    maintainAspectRatio: false,
    indexAxis: o.horizontal ? 'y' : 'x',
    plugins: {
      legend: {
        display: !!o.legend,
        position: 'bottom',
        labels: { color: tick, font: { size: 12 }, usePointStyle: true, pointStyle: 'circle', padding: 16 },
      },
      tooltip: {
        ...tooltipTheme(),
        callbacks: {
          label: ctx => {
            const value = o.horizontal ? ctx.parsed.x : ctx.parsed.y;
            const name  = o.legend ? `${ctx.dataset.label}: ` : '';
            return ` ${name}${value}${unit}`;
          },
        },
      },
    },
    scales: o.horizontal ? { x: valueAxis, y: categoryAxis } : { x: categoryAxis, y: valueAxis },
    ...interaction(pick),
  };
}

export function lineOptions(): ChartConfiguration<'line'>['options'] {
  const tick = cssVar('--t2', '#AEBBD0');
  const grid = cssVar('--bd', 'rgba(160,185,255,0.13)');
  return {
    responsive: true,
    maintainAspectRatio: false,
    interaction: { mode: 'index', intersect: false },
    plugins: {
      legend: {
        position: 'bottom',
        labels: { color: tick, font: { size: 12 }, usePointStyle: true, pointStyle: 'circle', padding: 16 },
      },
      tooltip: tooltipTheme(),
    },
    scales: {
      x: { ticks: { font: { size: 11 }, color: tick }, grid: { display: false }, border: { display: false } },
      y: {
        beginAtZero: true,
        ticks: { precision: 0, font: { size: 11 }, color: tick },
        grid: { color: grid },
        border: { display: false },
      },
    },
  };
}

/** Round to at most one decimal and drop a trailing ".0". */
export function pct(value: number | null | undefined): string {
  if (value == null) return '—';
  return `${Math.round(value * 10) / 10}%`;
}
