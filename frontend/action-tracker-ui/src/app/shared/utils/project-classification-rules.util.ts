/**
 * Internal project classification rules on the client (WO-2026-11 P-1) — a copy
 * of the server's `ProjectClassificationRules.cs`; **change both together** (the
 * spec repeats `ProjectClassificationRulesTests`).
 *
 * - Leading / trailing spaces are removed and runs of white space inside become one space.
 * - 1 to 100 characters after that; no control characters.
 * - Unique within the workspace without regard to case (the server answers 409 otherwise).
 *
 * Also the payload helper for project edits: `internalClassificationPatch`.
 */

import { PROJECT_CLASSIFICATION_MAX_LENGTH } from '../../models/project-classification.model';

/** The white space .NET's `\s` matches (same set as `tag-rules.util.ts`). */
const WHITE_SPACE_RUN = /[\t\n\v\f\r\u0085\p{Z}]+/gu;
/** .NET `char.IsControl`: U+0000–U+001F and U+007F–U+009F. */
const CONTROL = /[\u0000-\u001F\u007F-\u009F]/;

/** Trimmed, with every run of white space turned into one space. */
export function cleanClassificationName(raw: string | null | undefined): string {
  return (raw ?? '').replace(WHITE_SPACE_RUN, ' ').trim();
}

/** The key names are compared by (case ignored). */
export function classificationKey(cleaned: string): string {
  return cleaned.toUpperCase();
}

/** The rule `raw` breaks, in the server's words, or null. */
export function classificationNameProblem(raw: string | null | undefined): string | null {
  const name = cleanClassificationName(raw);
  if (name.length === 0) return 'Enter a name for the classification.';
  if (CONTROL.test(name)) return 'The classification name contains a character that is not allowed.';
  if (name.length > PROJECT_CLASSIFICATION_MAX_LENGTH) {
    return `The classification name can be at most ${PROJECT_CLASSIFICATION_MAX_LENGTH} characters.`;
  }
  return null;
}

/**
 * The server's duplicate-name message when `raw` matches another live
 * classification in `existing` (ignoring `exceptId`), else null.
 */
export function duplicateClassificationProblem(
  raw: string | null | undefined,
  existing: readonly { id: string; name: string }[],
  exceptId?: string | null,
): string | null {
  const name = cleanClassificationName(raw);
  const key = classificationKey(name);
  const clash = existing.some(c => c.id !== exceptId && classificationKey(cleanClassificationName(c.name)) === key);
  return clash
    ? `This workspace already has a classification named "${name}" (names are compared without regard to case).`
    : null;
}

/**
 * The internal-classification part of a project PUT body: the new id when it
 * changed, `clearInternalClassification: true` when it was removed, nothing
 * when it is unchanged (the server keeps the current one when the field is
 * left out). Empty string / null / undefined all mean "none".
 */
export function internalClassificationPatch(
  original: string | null | undefined,
  current: string | null | undefined,
): { internalClassificationId?: string; clearInternalClassification?: boolean } {
  const before = original || null;
  const after = current || null;
  if (before === after) return {};
  return after ? { internalClassificationId: after } : { clearInternalClassification: true };
}
