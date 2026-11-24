import { Route } from '@angular/router';
import { routes } from './app.routes';
import { permissionGuard } from './core/guards/permission-data.guard';

/** Every route, children included, flattened. */
function allRoutes(list: Route[]): Route[] {
  return list.flatMap(r => [r, ...allRoutes(r.children ?? [])]);
}

function route(path: string): Route {
  const found = allRoutes(routes).find(r => r.path === path);
  if (!found) throw new Error(`No route '${path}'`);
  return found;
}

// WO-2026-11 A-1b: the edit route needs ActionItems.Edit, like PUT /api/action-items/{id}.
describe('app.routes (WO-2026-11 A-1b)', () => {
  it('guards actions/:id/edit with ActionItems.Edit', () => {
    const edit = route('actions/:id/edit');
    expect(edit.canActivate).toContain(permissionGuard);
    expect(edit.data).toEqual({ requiredArea: 'ActionItems', requiredAction: 'Edit' });
  });

  it('leaves the view route open (the detail page hides Edit instead)', () => {
    expect(route('actions/:id/view').canActivate).toBeUndefined();
  });
});
