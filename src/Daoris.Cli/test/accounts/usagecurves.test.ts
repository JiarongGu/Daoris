import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { curveNow, tightestSaid } from '../../src/accounts/usagecurves.ts';
import type { Curve, Switch } from '../../src/accounts/rotation.ts';
import { saidLine } from '../../src/accounts/windows.ts';
import type { WindowSaid } from '../../src/accounts/windows.ts';
import { driverFile, driverTestFile } from '../_csharp.ts';
import { cliSource, SOURCES } from '../_sources.ts';

/**
 * The usage curve (D165 point 2; the switch design §2, §3 and §5; SWITCH1b-iii): how much of each window an account may have
 * used by now, and whether what its agent said is over that, which `profile use`'s said line marks as the start's line does
 * (`opening.said-over`). The CLI's half of a TWIN with the driver's `UsageCurves`: 🔴 both read the table in
 * `fixtures/usage-curves.json`, row for row, the driver's `UsageCurvesTwinTests` reading it as this file does.
 */

type Scope = { switch: Switch; curve: Curve };
type Said = [window: string, used: number | null, reset: string];
type Tightest = { window: string; curve: number; room: number } | null;

const here = dirname(dirname(fileURLToPath(import.meta.url)));
const TABLE = JSON.parse(readFileSync(join(here, 'fixtures', 'usage-curves.json'), 'utf8')) as {
  now: string;
  value: [name: string, switched: Switch, curve: Curve | null, window: string, reset: string, value: number | null][];
  tightest: [name: string, switched: Switch, curve: Curve | null, said: Said[], tightest: Tightest, clause: string | null][];
};
const NOW = new Date(TABLE.now);

const scope = (switched: Switch, curve: Curve | null): Scope => ({ switch: switched, curve: curve ?? {} });
const sixPlaces = (value: number | null) => (value === null ? null : Math.round(value * 1e6) / 1e6);

function windows(said: Said[]): WindowSaid[] {
  return said.map(([window, used, reset]) => ({
    window, used, reset: new Date(reset), standing: null, credits: false, seen: new Date(NOW.getTime() - 20 * 60_000),
    session: 's1', source: 'said',
  }));
}

test('the curve now on a window is the driver\'s, row for row (the twin\'s table)', () => {
  assert.ok(TABLE.value.length > 0);
  for (const [name, switched, curve, window, reset, expected] of TABLE.value) {
    assert.equal(sixPlaces(curveNow(scope(switched, curve), window, new Date(reset), NOW)), expected, name);
  }
});

test('the tightest window an account said is the driver\'s, row for row (the twin\'s table)', () => {
  assert.ok(TABLE.tightest.length > 0);
  for (const [name, switched, curve, said, expected] of TABLE.tightest) {
    const found = tightestSaid(scope(switched, curve), windows(said), NOW);
    const read = found === null ? null : { window: found.window.window, curve: sixPlaces(found.curve), room: sixPlaces(found.room) };
    assert.deepEqual(read, expected, name);
  }
});

test('the said line marks a window over its curve with the curve\'s value now, as the start\'s line does', () => {
  for (const [name, switched, curve, said, , clause] of TABLE.tightest) {
    const line = saidLine(windows(said), NOW, 'UTC', scope(switched, curve));
    if (clause === null) assert.doesNotMatch(line, /over its/, name);
    else assert.ok(line.endsWith(`; ${clause}`), `${name}: ${line}`);
  }
});

test('the curve\'s constants are the driver\'s, each copy naming the other', () => {
  const driver = readFileSync(driverFile('UsageCurves.cs'), 'utf8');
  const cli = readFileSync(join(SOURCES, cliSource('usagecurves.ts')), 'utf8');

  assert.match(driver, /WeeklyLead = 20, WeeklyKnee = 95, WeeklyEnd = 100;/);
  assert.match(driver, /"session" => TimeSpan\.FromHours\(5\),\s+"weekly" => TimeSpan\.FromDays\(7\),/);
  assert.match(cli, /WEEKLY_LEAD = 20;/);
  assert.match(cli, /WEEKLY_KNEE = 95;/);
  assert.match(cli, /WEEKLY_END = 100;/);
  assert.match(cli, /UsageCurves/);
  // The driver's twin test reads this table by its name.
  assert.match(readFileSync(driverTestFile('UsageCurvesTwinTests.cs'), 'utf8'), /"usage-curves\.json"/);
});
