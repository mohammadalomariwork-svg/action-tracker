/**
 * Filters an array of items to only those whose orgUnitId
 * is in the user's visible org unit list.
 * If visibleOrgUnitIds is empty, returns all items unfiltered
 * (fallback for admin/global-access users).
 */
export function filterByOrgUnit<T extends { orgUnitId?: string }>(
  items: T[],
  visibleOrgUnitIds: string[]
): T[] {
  if (!visibleOrgUnitIds || visibleOrgUnitIds.length === 0) {
    return items;
  }
  return items.filter(
    item => !item.orgUnitId || visibleOrgUnitIds.includes(item.orgUnitId)
  );
}
