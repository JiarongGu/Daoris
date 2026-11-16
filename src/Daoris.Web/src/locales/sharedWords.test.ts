import { describe, expect, it } from 'vitest';
import { lineLevels, readRegisters, shared, things } from '../../scripts/shared-words.mjs';
import { load } from '../../scripts/names-check.mjs';

/**
 * The census of window words shared by two things (WORDCLASH1): the glossary's terms, the driver's seeded roles and the reporting
 * line's levels give no word to two things, so a level is never named by a word a seeded role or a term already says.
 */
describe('the window words two things share', () => {
  it('reads the line levels as the first class of the line file declares them', () => {
    const text = 'namespace X;\n\npublic static class LineLevels\n{\n    public const string Machine = "machine-supervisor";\n    public const string Owner = "owner";\n}\n\npublic static class Other\n{\n    public const string Agent = "agent";\n}\n';
    expect(lineLevels(text)).toEqual(['machine-supervisor', 'owner']);
    expect(lineLevels('namespace X;\n')).toBeNull();
  });

  it('finds a line level named by a seeded role\'s word, and one thing named alike in two registers is one thing', () => {
    const glossary = { terms: [{ term: 'supervisor', en: 'supervisor', zh: '主管', means: '', avoid: { en: [], zh: [] } }] } as never;
    const registers = {
      roles: [{ id: 'lead', name: { en: 'Lead', zh: '主导' } }, { id: 'supervisor', name: { en: 'Supervisor', zh: '主管' } }],
      levels: ['lead', 'owner'],
    };

    const found = shared(things(glossary, registers));

    expect(found.map((each) => each.key)).toEqual(['shared:lead']);
    expect(found[0].rule).toBe('shared');
    expect(found[0].message).toContain('role lead (主导)');
    expect(found[0].message).toContain('level lead (no Chinese)');
  });

  it('finds no word two things share among the glossary, the seeded roles and the line\'s levels', () => {
    const registers = readRegisters();
    expect(registers.roles?.length).toBeGreaterThan(0);
    expect(registers.levels).toContain('owner');
    expect(shared(things(load().glossary, registers))).toEqual([]);
  });
});
