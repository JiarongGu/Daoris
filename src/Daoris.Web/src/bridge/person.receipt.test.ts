import { describe, expect, it } from 'vitest';
import { standingOf } from './person';

// What a press that opened a place says it made, as STATE's `standingConfirmation.helpPresses` carries it beside the press: read field by
// field, so a receipt the page cannot read is left off and the press stays in its list.

const press = (more: Record<string, unknown>) => ({ id: 'a', module: 'DAORIS.DRIVER', route: 'REGISTRY_CREATE', session: 's1', at: '2026-11-10T10:00:00Z', outcome: 'done', ...more });
const receiptOf = (more: Record<string, unknown>) => standingOf({ helpPresses: [press(more)] })?.helpPresses[0]?.receipt;

describe("a press's receipt over the bridge", () => {
  it("is a department's folder, line and quests", () => {
    expect(receiptOf({ receipt: { kind: 'opened', repository: 'ledger', workspace: 'aurora', folder: '/work/aurora/ledger', line: 'main', room: null, quests: ['q7'], done: true } }))
      .toEqual({ kind: 'opened', repository: 'ledger', workspace: 'aurora', folder: '/work/aurora/ledger', line: 'main', room: null, quests: ['q7'], done: true });
  });

  it("is a room's one quest", () => {
    expect(receiptOf({ route: 'PLACE_APPLY', receipt: { kind: 'room', repository: 'engine', room: 'tools/web', quests: ['q9'], done: true } }))
      .toMatchObject({ kind: 'room', repository: 'engine', room: 'tools/web', quests: ['q9'], done: true, workspace: null, folder: null, line: null });
  });

  it('is left off where the page cannot read it, and the press stays', () => {
    for (const receipt of [null, 'x', { kind: 'other', repository: 'ledger' }, { kind: 'opened' }]) {
      const read = standingOf({ helpPresses: [press({ receipt })] })?.helpPresses;
      expect(read).toHaveLength(1);
      expect(read?.[0]).not.toHaveProperty('receipt');
    }
    expect(standingOf({ helpPresses: [press({})] })?.helpPresses[0]).not.toHaveProperty('receipt');
  });

  it('keeps only the quests it can read', () => {
    expect(receiptOf({ receipt: { kind: 'room', repository: 'engine', room: 'x', quests: ['q9', 4, '', null], done: false } })).toMatchObject({ quests: ['q9'], done: false });
  });
});
