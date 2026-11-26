import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { placed, readQueueRow, readQueueRows } from './queuePlaces';

/**
 * The page's twin of the comparator composes each waiting item's place from the rows `QUEUES` answers, as the terminal's reader does
 * (`QueueRows.Placed`): the driver's `fixtures/queue-places.json` holds the cases, read row for row here and by `QueueRowsTwinTests`, so the
 * window and `daoris queue` give one place for one item.
 */

type Case = {
  name: string;
  rows: unknown[];
  places: { queue: string; item: string; position: number; of: number; ahead: string[] }[];
};

const TABLE = JSON.parse(readFileSync(join(process.cwd(), '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'fixtures', 'queue-places.json'), 'utf8')) as {
  cases: Case[];
};

describe('the places of the waiting items, as the shared table gives them', () => {
  it.each(TABLE.cases.map((each) => [each.name, each] as const))('%s', (_name, each) => {
    const rows = each.rows.map(readQueueRow);
    expect(rows.every((row) => row !== null)).toBe(true);

    const spots = placed(rows.filter((row) => row !== null)).map((spot) => ({
      queue: spot.row.queue, item: spot.row.item, position: spot.position, of: spot.of, ahead: spot.ahead.map((row) => row.item),
    }));

    expect(spots).toEqual(each.places);
  });
});

describe('the rows as the route answers them', () => {
  it('reads each field, a class none of the four as normal, and an answer with no rows as none', () => {
    const [row] = readQueueRows({
      rows: [{
        queue: 'starts', item: 'q1', class: 'pressing', stage: 'resume', arrived: '2026-11-26T10:00:00Z', order: 2, source: 'ask',
        by: 'the person', why: 'the release', ask: 'cafee7', agedFrom: 'low', repository: 'engine', code: 'cap', verdict: 'AtCapacity',
        values: { cap: '2', stray: 3 }, reason: 'The cap is spent.',
      }],
    });

    expect(row).toEqual({
      queue: 'starts', item: 'q1', class: 'normal', stage: 'resume', arrived: '2026-11-26T10:00:00Z', order: 2, source: 'ask',
      by: 'the person', why: 'the release', ask: 'cafee7', agedFrom: 'low', repository: 'engine', code: 'cap', verdict: 'AtCapacity',
      values: { cap: '2' }, reason: 'The cap is spent.',
    });
    expect(readQueueRows(null)).toEqual([]);
    expect(readQueueRows({ rows: [{ queue: 'starts', item: 'q', stage: 'sideways' }] })).toEqual([]);
  });
});
