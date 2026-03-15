import { Pipe, PipeTransform } from '@angular/core';

/**
 * Transforms an org unit scope integer to a human-readable label.
 * 0 → "All Org Units"
 * 1 → "Specific Org Unit"
 * 2 → "Own Records Only"
 */
@Pipe({
  name: 'orgUnitScope',
  standalone: true,
})
export class OrgUnitScopePipe implements PipeTransform {
  transform(value: number): string {
    switch (value) {
      case 0:  return 'All Org Units';
      case 1:  return 'Specific Org Unit';
      case 2:  return 'Own Records Only';
      default: return 'Unknown';
    }
  }
}
