import {
  classificationKey, classificationNameProblem, cleanClassificationName,
  duplicateClassificationProblem, internalClassificationPatch,
} from './project-classification-rules.util';

// WO-2026-11 P-1 — repeats ProjectClassificationRulesTests (server); change both together.
describe('project-classification-rules.util (WO-2026-11 P-1)', () => {
  it('trims and collapses white space like the server', () => {
    expect(cleanClassificationName('  Capital  works ')).toBe('Capital works');
    expect(cleanClassificationName('Research\t\tgrant')).toBe('Research grant');
    expect(cleanClassificationName('Line\nbreak')).toBe('Line break');
    expect(cleanClassificationName('Already clean')).toBe('Already clean');
    expect(cleanClassificationName(null)).toBe('');
  });

  it('compares names without regard to case', () => {
    expect(classificationKey('Capital Works')).toBe(classificationKey('CAPITAL works'));
  });

  it('names the broken rule in the server wording', () => {
    expect(classificationNameProblem(null)).toBe('Enter a name for the classification.');
    expect(classificationNameProblem('')).toBe('Enter a name for the classification.');
    expect(classificationNameProblem('   ')).toBe('Enter a name for the classification.');
    expect(classificationNameProblem('Bad\u0007bell')).toBe('The classification name contains a character that is not allowed.');
  });

  it('accepts 100 characters and refuses 101 (after cleaning)', () => {
    expect(classificationNameProblem('a'.repeat(100))).toBeNull();
    expect(classificationNameProblem('a'.repeat(101))).toBe('The classification name can be at most 100 characters.');
    expect(classificationNameProblem('  ' + 'a'.repeat(100) + '  ')).toBeNull();
  });

  it('allows commas and punctuation', () => {
    expect(classificationNameProblem('Teaching, learning & research (TLR)')).toBeNull();
  });

  it('spots a duplicate in any case, except the row being renamed', () => {
    const existing = [{ id: 'a', name: 'Capital works' }, { id: 'b', name: 'Research' }];
    expect(duplicateClassificationProblem(' capital  WORKS ', existing))
      .toBe('This workspace already has a classification named "capital WORKS" (names are compared without regard to case).');
    expect(duplicateClassificationProblem('Capital Works', existing, 'a')).toBeNull();
    expect(duplicateClassificationProblem('New one', existing)).toBeNull();
  });

  it('builds the edit patch: set, clear, or nothing when unchanged', () => {
    expect(internalClassificationPatch(null, null)).toEqual({});
    expect(internalClassificationPatch('a', 'a')).toEqual({});
    expect(internalClassificationPatch(undefined, '')).toEqual({});
    expect(internalClassificationPatch(null, 'b')).toEqual({ internalClassificationId: 'b' });
    expect(internalClassificationPatch('a', 'b')).toEqual({ internalClassificationId: 'b' });
    expect(internalClassificationPatch('a', null)).toEqual({ clearInternalClassification: true });
    expect(internalClassificationPatch('a', '')).toEqual({ clearInternalClassification: true });
  });
});
