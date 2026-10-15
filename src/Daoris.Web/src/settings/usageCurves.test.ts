import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { curveNow, type CurveScope, type CurvePoint, tightestSaid, wholePoints } from './usageCurves';

/**
 * The usage curve on the page (SWITCH1d; D165 point 2; the switch design §2 and §5): how much of each window an account may have
 * used by now, which the Accounts screen draws each window against and the said line marks a window over. The page's half of a
 * TWIN with the driver's `UsageCurves` and the CLI's `usagecurves.ts`: 🔴 all three read the CLI's
 * `test/fixtures/usage-curves.json`, row for row.
 */

type Switch = CurveScope['switch'];
type Curve = Record<string, CurvePoint[]>;
type Said = [window: string, used: number | null, reset: string];
type Tightest = { window: string; curve: number; room: number } | null;

// From the page's package, where its suite runs, as `shellWord.test.ts` reads its table.
const TABLE = JSON.parse(readFileSync(join(process.cwd(), '..', 'Daoris.Cli', 'test', 'fixtures', 'usage-curves.json'), 'utf8')) as {
  now: string;
  value: [name: string, switched: Switch, curve: Curve | null, window: string, reset: string, value: number | null][];
  tightest: [name: string, switched: Switch, curve: Curve | null, said: Said[], tightest: Tightest, clause: string | null][];
};
const NOW = new Date(TABLE.now);

const scope = (switched: Switch, curve: Curve | null): CurveScope => ({ switch: switched, curve: curve ?? {} });
const sixPlaces = (value: number | null) => (value === null ? null : Math.round(value * 1e6) / 1e6);

describe('the usage curve, as the driver and the terminal measure it', () => {
  it('reads the table at all: a table that holds nothing passes every row below', () => {
    expect(TABLE.value.length).toBeGreaterThan(0);
    expect(TABLE.tightest.length).toBeGreaterThan(0);
  });

  for (const [name, switched, curve, window, reset, expected] of TABLE.value) {
    it(`now on a window: ${name}`, () => {
      expect(sixPlaces(curveNow(scope(switched, curve), window, reset, NOW))).toBe(expected);
    });
  }

  for (const [name, switched, curve, said, expected] of TABLE.tightest) {
    it(`the tightest window said: ${name}`, () => {
      const windows = said.map(([window, used, reset]) => ({ window, used, reset }));
      const found = tightestSaid(scope(switched, curve), windows, NOW);
      expect(found === null ? null : { window: found.window.window, curve: sixPlaces(found.curve), room: sixPlaces(found.room) })
        .toEqual(expected);
    });
  }

  it('says a number of points as a whole number, a half away from zero', () => {
    expect([wholePoints(0.5), wholePoints(-0.5), wholePoints(62.4)]).toEqual([1, -1, 62]);
  });

  it('reads a scope from a shell older than the switch as the smart curve', () => {
    expect(curveNow({}, 'session', '2026-10-13T13:00:00Z', NOW)).toBe(90);
  });
});
