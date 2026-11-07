import { describe, expect, it } from 'vitest';
import type { SupervisorRulesRead } from '../../data/wiring';
import { blankRule, chainOf, draftOf, inheritedFrom, nextCount, pressOf, readable, reaction, recount, replaced, rulesOfRole, sameAsStored, setOf } from './supervisorRulesDraft';

// CONTEXTDRIVE1g: the supervisor's rules as the editor holds them, and the press that writes a place's set whole.

const READ: SupervisorRulesRead = {
  counts: [
    { count: 'answers-alone', window: 'in-a-row', actions: ['hold', 'refer', 'off'], least: 1, most: 20 },
    { count: 'failures', window: 'since-try-again', actions: ['hold', 'off'], least: 1, most: 20 },
    { count: 'limits', window: 'this-end', actions: ['ask', 'wait', 'next', 'weighted', 'off'], least: 1, most: 1 },
  ],
  sets: [
    { place: 'machine', revision: 3, rules: [{ id: 'r-1', count: 'answers-alone', window: 'in-a-row', at: 2, do: 'hold' }, { id: 'r-2', count: 'failures', window: 'since-try-again', at: 3, do: 'hold' }] },
    { place: 'repository:daoris', revision: 1, rules: [{ id: 'r-1', count: 'answers-alone', window: 'in-a-row', at: 3, do: 'hold', roles: ['build'] }] },
  ],
};

describe('The supervisor rules draft', () => {
  it('opens a place on its set at its revision, and an empty place at revision 0', () => {
    expect(draftOf('repository:daoris', setOf(READ, 'repository:daoris'))).toEqual({
      place: 'repository:daoris', revision: 1, rows: [{ key: 0, count: 'answers-alone', at: '3', do: 'hold', upTo: '', roles: ['build'] }],
    });
    expect(draftOf('workspace:w', setOf(READ, 'workspace:w'))).toEqual({ place: 'workspace:w', revision: 0, rows: [] });
  });

  it('writes the set whole at the revision read, a wait with its minutes and a role with its id, a window left to the door', () => {
    const draft = draftOf('machine', setOf(READ, 'machine'));
    draft.rows.push({ key: 9, count: 'limits', at: '1', do: 'wait', upTo: '30', roles: ['build'] });

    expect(pressOf(draft)).toEqual({
      change: 'set', id: 'supervisor', place: 'machine', rulesRevision: 3,
      rules: [{ count: 'answers-alone', at: 2, do: 'hold' }, { count: 'failures', at: 3, do: 'hold' }, { count: 'limits', at: 1, do: 'wait', upTo: 30, roles: ['build'] }],
    });
  });

  it("keeps a rule's roles whole: a rule set at the terminal for two roles writes back for both when another rule is saved", () => {
    const read: SupervisorRulesRead = {
      ...READ,
      sets: [{ place: 'repository:x', revision: 4, rules: [
        { id: 'r-1', count: 'limits', window: 'this-end', at: 1, do: 'weighted', roles: ['build', 'design'] },
        { id: 'r-2', count: 'failures', window: 'since-try-again', at: 3, do: 'hold', roles: ['build', 'design'] },
      ] }],
    };
    const draft = draftOf('repository:x', setOf(read, 'repository:x'));
    expect(sameAsStored(draft, setOf(read, 'repository:x'))).toBe(true);
    draft.rows[1]!.at = '4';

    expect(pressOf(draft).rules!.map((rule) => rule.roles)).toEqual([['build', 'design'], ['build', 'design']]);
    draft.rows[1]!.at = '3';
    draft.rows[0]!.roles = ['build'];
    expect(sameAsStored(draft, setOf(read, 'repository:x'))).toBe(false);
  });

  it('starts a new rule on the first count not ruled here, at its least and its first act', () => {
    const rows = draftOf('machine', setOf(READ, 'machine')).rows;
    const count = nextCount(READ.counts, rows)!;

    expect(count.count).toBe('limits');
    expect(blankRule(count, 5)).toEqual({ key: 5, count: 'limits', at: '1', do: 'ask', upTo: '', roles: [] });
  });

  it('holds a moved rule to the new count: its threshold within the bounds, its act one the count allows, minutes only for a wait', () => {
    const row = { key: 1, count: 'answers-alone', at: '12', do: 'hold', upTo: '', roles: [] };
    expect(recount(row, READ.counts[2]!)).toEqual({ key: 1, count: 'limits', at: '1', do: 'ask', upTo: '', roles: [] });
    expect(reaction(row, 'wait').upTo).toBe('30');
    expect(reaction({ ...row, do: 'wait', upTo: '45' }, 'hold').upTo).toBe('');
  });

  it('knows an unchanged draft and an unreadable one', () => {
    const draft = draftOf('machine', setOf(READ, 'machine'));
    expect(sameAsStored(draft, setOf(READ, 'machine'))).toBe(true);
    draft.rows[0]!.at = '4';
    expect(sameAsStored(draft, setOf(READ, 'machine'))).toBe(false);
    expect(readable(draft)).toBe(true);
    draft.rows[0]!.at = '';
    expect(readable(draft)).toBe(false);
  });

  it("says what a place's rule replaces and what it inherits, by role", () => {
    const here = draftOf('repository:daoris', setOf(READ, 'repository:daoris'));
    const roles = ['build', 'chat'];

    // A Build rule replaces the machine's for Build's lines; Chat's lines still read the machine's.
    expect(replaced(READ, here, here.rows[0]!, roles).map((each) => [each.place, each.rule.at])).toEqual([['machine', 2]]);
    expect(inheritedFrom(READ, here, roles).map((each) => [each.place, each.rule.count])).toEqual([['machine', 'answers-alone'], ['machine', 'failures']]);
    const everyRole = { ...here, rows: [{ ...here.rows[0]!, roles: [] }] };
    expect(inheritedFrom(READ, everyRole, roles).map((each) => each.rule.count)).toEqual(['failures']);
    expect(inheritedFrom(READ, draftOf('machine', setOf(READ, 'machine')), roles)).toEqual([]);
  });

  it("reads a repository's chain through its workspace as ROLES answers it", () => {
    const read: SupervisorRulesRead = {
      ...READ,
      wider: { 'repository:r': ['workspace:w'] },
      sets: [
        READ.sets[0]!,
        { place: 'workspace:w', revision: 1, rules: [{ id: 'w-1', count: 'answers-alone', window: 'in-a-row', at: 4, do: 'hold' }] },
        { place: 'repository:r', revision: 1, rules: [{ id: 'r-1', count: 'answers-alone', window: 'in-a-row', at: 3, do: 'hold' }] },
      ],
    };
    const here = draftOf('repository:r', setOf(read, 'repository:r'));

    expect(chainOf(read, 'repository:r')).toEqual(['repository:r', 'workspace:w', 'machine']);
    expect(chainOf(read, 'workspace:w')).toEqual(['workspace:w', 'machine']);
    expect(replaced(read, here, here.rows[0]!, ['build']).map((each) => [each.place, each.rule.at])).toEqual([['workspace:w', 4]]);
    expect(inheritedFrom(read, { ...here, rows: [] }, ['build']).map((each) => [each.place, each.rule.count])).toEqual([['workspace:w', 'answers-alone'], ['machine', 'failures']]);
  });

  it('finds the rules naming a role at every place', () => {
    expect(rulesOfRole(READ, 'build')).toEqual([{ place: 'repository:daoris', rule: READ.sets[1]!.rules[0] }]);
    expect(rulesOfRole(undefined, 'build')).toEqual([]);
  });
});
