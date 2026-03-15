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
 * Structural directive that conditionally includes its host element in the DOM
 * based on whether the current user has a specific effective permission.
 *
 * Works like `*ngIf` — removes the element entirely when the permission is absent,
 * and restores it when the permission is granted reactively.
 *
 * @example
 * ```html
 * <button *appHasPermission="'Projects'" [appHasPermissionAction]="'Create'">
 *   Add Project
 * </button>
 *
 * <div *appHasPermission="'Reports'; action: 'Export'; else noAccess">Export</div>
 * <ng-template #noAccess>No access</ng-template>
 * ```
 *
 * Import `HasPermissionDirective` directly into any standalone component that
 * needs it:
 * ```ts
 * imports: [HasPermissionDirective]
 * ```
 */
@Directive({
  selector: '[appHasPermission]',
  standalone: true,
})
export class HasPermissionDirective implements OnInit {
  private readonly permissionState = inject(PermissionStateService);
  private readonly templateRef     = inject(TemplateRef<unknown>);
  private readonly viewContainer   = inject(ViewContainerRef);
  private readonly destroyRef      = inject(DestroyRef);

  private _area    = '';
  private _action  = '';
  private _elseRef: TemplateRef<unknown> | null = null;

  private viewRef:     EmbeddedViewRef<unknown> | null = null;
  private elseViewRef: EmbeddedViewRef<unknown> | null = null;

  @Input('appHasPermission')
  set appHasPermission(area: string) {
    this._area = area;
    this.updateView();
  }

  @Input('appHasPermissionAction')
  set appHasPermissionAction(action: string) {
    this._action = action;
    this.updateView();
  }

  @Input('appHasPermissionElse')
  set appHasPermissionElse(ref: TemplateRef<unknown> | null) {
    this._elseRef = ref;
    this.updateView();
  }

  ngOnInit(): void {
    // Re-evaluate whenever the effective permissions change (e.g. after login).
    this.permissionState.permissions$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.updateView());
  }

  private updateView(): void {
    const allowed =
      this._area && this._action
        ? this.permissionState.hasPermission(this._area, this._action)
        : true; // Render if inputs not yet set

    if (allowed) {
      if (!this.viewRef) {
        this.clearElse();
        this.viewRef = this.viewContainer.createEmbeddedView(this.templateRef);
      }
    } else {
      if (this.viewRef) {
        this.viewContainer.clear();
        this.viewRef = null;
      }
      if (this._elseRef && !this.elseViewRef) {
        this.elseViewRef = this.viewContainer.createEmbeddedView(this._elseRef);
      }
    }
  }

  private clearElse(): void {
    if (this.elseViewRef) {
      this.elseViewRef.destroy();
      this.elseViewRef = null;
    }
  }
}
