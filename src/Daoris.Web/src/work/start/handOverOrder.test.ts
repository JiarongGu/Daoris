import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import type { ToolDoor } from '../../agents/tools';
import { otherToolDoors } from './handOverOffer';

// MIXAGENT1c (D191's settling of the mixed agents design): under the `on` rule an agent's ask goes to the next agent in the person's
// order, the order this page's hand-over offer draws from. One table both read: the driver's `HandOverOrder.Others` is held to the
// driver suite's `fixtures/hand-over-order.json` by `HandOverOrderTests`, and `otherToolDoors` here, row for row.

type Row = [name: string, own: string, doors: [harness: string, present: boolean, accountOf: string | null][], order: string[]];
const TABLE = JSON.parse(readFileSync(
  join(process.cwd(), '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'fixtures', 'hand-over-order.json'), 'utf8')) as { rows: Row[] };

describe("the person's order a hand-over goes to, as the driver's table holds it", () => {
  it('reads the table', () => {
    expect(TABLE.rows.length).toBeGreaterThan(5);
  });

  it.each(TABLE.rows.map((row) => [row[0], row] as const))('%s', (_, [, own, doors, order]) => {
    const roster: ToolDoor[] = doors.map(([harness, present, accountOf]) => ({ harness, present, accountOf }));
    expect(otherToolDoors({ adapter: own }, roster).map((door) => door.harness)).toEqual(order);
  });
});
