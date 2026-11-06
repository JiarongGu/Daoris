import { describe, expect, it } from 'vitest';
import { NODE_KINDS } from '../bridge/generated/routes';
import { kindKeys, nodeKinds, readNodeKinds, unwordedKinds } from '../../scripts/kind-words.mjs';
import { load } from '../../scripts/names-check.mjs';

/**
 * The words of each node kind (D247): the names check reads the generated `NODE_KINDS` as JSON and requires each kind's two titles and each
 * outcome it routes on in every language, so a kind the driver's kit declares with no words fails the build.
 */
describe('the node kinds\' words', () => {
  it('reads the generated list as the page imports it', () => {
    expect(readNodeKinds()).toEqual(NODE_KINDS.map((row) => ({ ...row })));
    expect(NODE_KINDS.map((row) => row.kind)).toContain('monitor');
  });

  it('reads a list over several lines, and none from a file that lists none', () => {
    const text = '// contracts: x\nexport const NODE_KINDS: readonly NodeKindRow[] = [\n  {"kind":"a","outcomes":[]},\n  {"kind":"b","outcomes":["x"]}\n];\n';
    expect(nodeKinds(text)?.map((row: { kind: string }) => row.kind)).toEqual(['a', 'b']);
    expect(nodeKinds('export type DriverRoute = \'A\';\n')).toBeNull();
  });

  it('asks each kind for its two titles and each outcome for its word', () => {
    expect(kindKeys([{ kind: 'quest.close', outcomes: [] }, { kind: 'monitor', outcomes: ['merged'] }])).toEqual([
      'workflow.edit.kind.quest.close', 'workflow.step.quest.close',
      'workflow.edit.kind.monitor', 'workflow.step.monitor', 'workflow.outcome.monitor.merged',
    ]);
  });

  it('finds each key a language leaves out or blank, and none it words', () => {
    const found = unwordedKinds([{ kind: 'a', outcomes: [] }], {
      en: { 'workflow.edit.kind.a': 'A', 'workflow.step.a': 'A' },
      zh: { 'workflow.edit.kind.a': 'A', 'workflow.step.a': ' ' },
    });
    expect(found.map((each) => `${each.language} ${each.key}`)).toEqual(['zh workflow.step.a']);
    expect(found.every((each) => each.rule === 'kind')).toBe(true);
  });

  it('words every kind the kit declares, in both languages', () => {
    const { en, zh } = load();
    expect(unwordedKinds(NODE_KINDS, { en, zh })).toEqual([]);
  });
});
