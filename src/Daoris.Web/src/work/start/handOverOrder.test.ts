import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import type { ToolDoor } from '../../agents/tools';
import { nextChoices, otherToolDoors } from './handOverOffer';

// MIXAGENT1c (D191's settling of the mixed agents design): under the `on` rule an agent's ask goes to the next agent in the person's
// order, the order this page's hand-over offer draws from. One table both read: the driver's `HandOverOrder.Others` is held to the
// driver suite's `fixtures/hand-over-order.json` by `HandOverOrderTests`, and `otherToolDoors` here, row for row.
// MODELROLE1e (D205 §3.5(e), round 4's Opus N1): once the session's actor has an order of its own, every path takes it. The same file's
// `actorRows` hold the driver's `HandOverOrder.Steps` and this page's `nextChoices` to one table too.

type Door = [harness: string, present: boolean, accountOf: string | null];
type Row = [name: string, own: string, doors: Door[], order: string[]];
type ActorRow = [
  name: string, own: string, doors: Door[], entries: [agent: string, model: string | null, from: string][],
  steps: [harness: string, model: string | null, from: string][],
];
const TABLE = JSON.parse(readFileSync(
  join(process.cwd(), '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'fixtures', 'hand-over-order.json'), 'utf8')) as {
  rows: Row[]; actorRows: ActorRow[];
};
const roster = (doors: Door[]): ToolDoor[] => doors.map(([harness, present, accountOf]) => ({ harness, present, accountOf }));

describe("the person's order a hand-over goes to, as the driver's table holds it", () => {
  it('reads the table', () => {
    expect(TABLE.rows.length).toBeGreaterThan(5);
    expect(TABLE.actorRows.length).toBeGreaterThan(5);
  });

  it.each(TABLE.rows.map((row) => [row[0], row] as const))('%s', (_, [, own, doors, order]) => {
    expect(otherToolDoors({ adapter: own }, roster(doors)).map((door) => door.harness)).toEqual(order);
  });
});

describe("the actor's order a hand-over goes to, as the driver's table holds it", () => {
  it.each(TABLE.actorRows.map((row) => [row[0], row] as const))('%s', (_, [, own, doors, entries, steps]) => {
    const actor = { choices: entries.map(([agent, model, from]) => ({ agent, model, from })) };

    expect(nextChoices({ adapter: own }, roster(doors), actor).map((step) => [step.door.harness, step.model, step.from])).toEqual(steps);
  });
});
