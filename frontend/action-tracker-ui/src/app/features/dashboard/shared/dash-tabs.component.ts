import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { PermissionStateService } from '../../permissions/services/permission-state.service';

interface DashTab {
  label: string;
  icon: string;
  path: string;
  exact: boolean;
  /** Tab is hidden unless the user holds this permission. */
  needs?: { area: string; action: string };
}

const TABS: DashTab[] = [
  { label: 'Overview',     icon: 'bi bi-speedometer2',  path: '/dashboard',          exact: true },
  { label: 'Action Items', icon: 'bi bi-check2-square', path: '/dashboard/actions',  exact: false },
  { label: 'Projects',     icon: 'bi bi-kanban',        path: '/dashboard/projects', exact: false },
  { label: 'Strategy',     icon: 'bi bi-bullseye',      path: '/dashboard/strategy', exact: false,
    needs: { area: 'StrategicPlans', action: 'View' } },
];

/** Switcher shown at the top of every dashboard. */
@Component({
  selector: 'app-dash-tabs',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="dt" aria-label="Dashboards">
      @for (tab of tabs(); track tab.path) {
        <a class="dt__tab" [routerLink]="tab.path" routerLinkActive="dt__tab--active"
           [routerLinkActiveOptions]="{ exact: tab.exact }" ariaCurrentWhenActive="page">
          <i [class]="tab.icon" aria-hidden="true"></i>{{ tab.label }}
        </a>
      }
    </nav>
  `,
  styleUrl: './dash-tabs.component.scss',
})
export class DashTabsComponent {
  private readonly permissions = inject(PermissionStateService);
  private readonly loaded = toSignal(this.permissions.permissions$, { initialValue: [] });

  readonly tabs = computed(() => {
    this.loaded();   // re-evaluate once permissions arrive
    return TABS.filter(t => !t.needs || this.permissions.hasPermission(t.needs.area, t.needs.action));
  });
}
