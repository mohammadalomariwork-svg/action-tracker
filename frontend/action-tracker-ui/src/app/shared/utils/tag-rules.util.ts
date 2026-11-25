/**
 * Tag rules on the client (WO-2026-11 A-5) — a copy of the server's
 * `TagRules.cs`; **change both together** (the spec repeats `TagRulesTests`).
 *
 * - Leading / trailing spaces are removed and runs of white space inside become one space.
 * - Duplicates are dropped without regard to case (the first spelling wins).
 * - At most 10 tags, each at most 30 characters.
 * - Empty tags, commas and control characters are refused.
 *
 * The server decides: when it already has a tag with the same name in another
 * case, the item gets the workspace's spelling.
 */

import { TAG_MAX_LENGTH, TAG_MAX_PER_ITEM } from '../../models/action-item-tag.model';

/** The white space .NET's `\s` matches (so both sides collapse the same characters). */
const WHITE_SPACE_RUN = /[\t\n\v\f\r\u0085\p{Z}]+/gu;
/** .NET `char.IsControl`: U+0000–U+001F and U+007F–U+009F. */
const CONTROL = /[\u0000-\u001F\u007F-\u009F]/;

/** Trimmed, with every run of white space turned into one space. */
export function cleanTag(raw: string | null | undefined): string {
  return (raw ?? '').replace(WHITE_SPACE_RUN, ' ').trim();
}

/** The key a tag is matched by (case ignored). */
export function tagKey(cleaned: string): string {
  return cleaned.toUpperCase();
}

/** The problem with one cleaned tag on its own, or null. Same wording as the server. */
export function singleTagProblem(name: string): string | null {
  if (name.length === 0) return 'A tag cannot be empty.';
  if (name.includes(',')) return `Tag "${name}" cannot contain a comma.`;
  if (CONTROL.test(name)) return `Tag "${[...name].map(c => (CONTROL.test(c) ? '?' : c)).join('')}" contains a character that is not allowed.`;
  if (name.length > TAG_MAX_LENGTH) return `Tag "${name}" is longer than ${TAG_MAX_LENGTH} characters.`;
  return null;
}

/** The first rule `tags` breaks (server wording), or null. Null / undefined input is fine. */
export function tagsProblem(tags: readonly (string | null | undefined)[] | null | undefined): string | null {
  if (!tags) return null;
  const seen = new Set<string>();
  for (const raw of tags) {
    const name = cleanTag(raw);
    const problem = singleTagProblem(name);
    if (problem) return problem;
    seen.add(tagKey(name));
  }
  return seen.size > TAG_MAX_PER_ITEM ? `An action item can have at most ${TAG_MAX_PER_ITEM} tags.` : null;
}

/** Cleaned and de-duplicated (first spelling wins), in the order given. Call after `tagsProblem`. */
export function cleanTags(tags: readonly (string | null | undefined)[]): string[] {
  const seen = new Set<string>();
  const result: string[] = [];
  for (const raw of tags) {
    const name = cleanTag(raw);
    if (name && !seen.has(tagKey(name))) {
      seen.add(tagKey(name));
      result.push(name);
    }
  }
  return result;
}

/** True when both lists name the same tags, whatever the case and order. */
export function sameTags(a: readonly string[], b: readonly string[]): boolean {
  const ka = new Set(cleanTags(a).map(tagKey));
  const kb = new Set(cleanTags(b).map(tagKey));
  return ka.size === kb.size && [...ka].every(k => kb.has(k));
}

/**
 * The `tags` field of an edit body: the cleaned list when it changed, else
 * undefined so the PUT leaves the tags alone (and a save without tag changes
 * also works while the server's tag tables are missing).
 */
export function tagsPatch(original: readonly string[], current: readonly string[]): string[] | undefined {
  return sameTags(original, current) ? undefined : cleanTags(current);
}

/** Tag names for an export cell ("a, b") — tags cannot contain commas. */
export function tagsCell(tags: readonly { name: string }[] | null | undefined): string {
  return (tags ?? []).map(t => t.name).join(', ');
}
