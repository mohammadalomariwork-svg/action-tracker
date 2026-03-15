import { Pipe, PipeTransform } from '@angular/core';

/**
 * Formats the `source` string from EffectivePermissionDto into a
 * human-readable badge label.
 *
 * "Role"                  → "From Role"
 * "UserOverride-Granted"  → "Override: Granted"
 * "UserOverride-Revoked"  → "Override: Revoked"
 * (anything else)         → original string
 */
@Pipe({
  name: 'permissionSource',
  standalone: true,
})
export class PermissionSourcePipe implements PipeTransform {
  transform(value: string): string {
    switch (value) {
      case 'Role':                 return 'From Role';
      case 'UserOverride-Granted': return 'Override: Granted';
      case 'UserOverride-Revoked': return 'Override: Revoked';
      default:                     return value;
    }
  }
}
