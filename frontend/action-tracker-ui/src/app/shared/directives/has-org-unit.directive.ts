import {
  DestroyRef,
  Directive,
  EmbeddedViewRef,
  Input,
  OnInit,
  TemplateRef,
  ViewContainerRef,
  inject,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PermissionStateService } from '../../features/permissions/services/permission-state.service';

/**
 * Structural directive that conditionally renders its host element based on
 * whether the given org unit ID is visible to the current user.
 *
 * @example
 * ```html
 * <tr *appHasOrgUnit="row.orgUnitId">...</tr>
 * ```
 */
@Directive({
  selector: '[appHasOrgUnit]',
  standalone: true,
})
export class HasOrgUnitDirective implements OnInit {
  private readonly permissionState = inject(PermissionStateService);
  private readonly templateRef     = inject(TemplateRef<unknown>);
  private readonly viewContainer   = inject(ViewContainerRef);
  private readonly destroyRef      = inject(DestroyRef);

  private _orgUnitId = '';
  private viewRef: EmbeddedViewRef<unknown> | null = null;

  @Input('appHasOrgUnit')
  set appHasOrgUnit(orgUnitId: string) {
    this._orgUnitId = orgUnitId;
    this.updateView();
  }

  ngOnInit(): void {
    this.permissionState.visibleOrgUnitIds$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.updateView());
  }

  private updateView(): void {
    const visible = this._orgUnitId
      ? this.permissionState.canSeeOrgUnit(this._orgUnitId)
      : true;

    if (visible) {
      if (!this.viewRef) {
        this.viewRef = this.viewContainer.createEmbeddedView(this.templateRef);
      }
    } else {
      this.viewContainer.clear();
      this.viewRef = null;
    }
  }
}
