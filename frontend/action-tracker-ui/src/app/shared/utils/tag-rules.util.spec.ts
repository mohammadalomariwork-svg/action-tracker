import { cleanTag, cleanTags, sameTags, tagKey, tagsCell, tagsPatch, tagsProblem } from './tag-rules.util';

// WO-2026-11 A-5 — the same cases as the server's TagRulesTests.
describe('tag-rules.util (WO-2026-11 A-5)', () => {
  it('trims and collapses white space', () => {
    expect(cleanTag('  Urgent  ')).toBe('Urgent');
    expect(cleanTag('Q4   review')).toBe('Q4 review');
    expect(cleanTag('a\tb\nc')).toBe('a b c');
    expect(cleanTag(null)).toBe('');
  });

  it('matches regardless of case', () => {
    expect(tagKey('Board Pack')).toBe(tagKey('board pack'));
  });

  it('null means leave the tags and is fine', () => {
    expect(tagsProblem(null)).toBeNull();
    expect(tagsProblem(undefined)).toBeNull();
  });

  it('names the reason with the server wording', () => {
    expect(tagsProblem(['ok', ''])).toBe('A tag cannot be empty.');
    expect(tagsProblem(['ok', '   '])).toBe('A tag cannot be empty.');
    expect(tagsProblem(['ok', 'a,b'])).toBe('Tag "a,b" cannot contain a comma.');
    expect(tagsProblem(['ok', 'bell\u0007'])).toBe('Tag "bell?" contains a character that is not allowed.');
    expect(tagsProblem(['ok', '1234567890123456789012345678901']))
      .toBe('Tag "1234567890123456789012345678901" is longer than 30 characters.');
  });

  it('allows 30 characters after cleaning', () => {
    expect(tagsProblem(['  ' + 'x'.repeat(30) + '  '])).toBeNull();
  });

  it('allows at most ten distinct tags; duplicates do not count', () => {
    const ten = Array.from({ length: 10 }, (_, i) => `t${i + 1}`);
    expect(tagsProblem([...ten, 'T1', ' t2 '])).toBeNull();
    expect(tagsProblem([...ten, 't11'])).toBe('An action item can have at most 10 tags.');
  });

  it('keeps the order and the first spelling', () => {
    expect(cleanTags([' Urgent ', 'q4', 'URGENT', 'Q4  ', 'Board  pack'])).toEqual(['Urgent', 'q4', 'Board pack']);
  });

  it('sends tags on an edit only when they changed', () => {
    expect(sameTags(['Urgent', 'Q4'], ['q4', ' urgent'])).toBeTrue();
    expect(tagsPatch(['Urgent', 'Q4'], ['q4', 'URGENT'])).toBeUndefined();
    expect(tagsPatch(['Urgent'], ['Urgent', 'New '])).toEqual(['Urgent', 'New']);
    expect(tagsPatch(['Urgent'], [])).toEqual([]);
    expect(tagsPatch([], [])).toBeUndefined();
  });

  it('joins names for an export cell', () => {
    expect(tagsCell([{ name: 'a' }, { name: 'b c' }])).toBe('a, b c');
    expect(tagsCell(undefined)).toBe('');
  });
});
