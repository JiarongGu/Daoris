import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { beforeEach, describe, expect, it } from 'vitest';
import i18n from '../../locales/i18n';
import { sentence } from '../../locales/format';
import { saidLine } from './accounts';
import { type CurvePoint, type CurveScope, ownPoints } from './usageCurves';

/**
 * The usage curve on the page (SWITCH1d; D165 point 2; the switch design §2 and §5), since PUSH1e judged in .NET alone (D182 point
 * 6): the driver's `UsageCurves` measures each window and the accounts' answer carries the verdicts, a window's curve now in whole
 * points and whether it is over it, and each scope's accounts whose tightest window is over its curve. The page words them. 🔴 The
 * CLI's `test/fixtures/usage-curves.json` holds the words: every `tightest` row's clause is the page's, given the verdict .NET
 * pushes for that row (`DriverModuleAccountsTests.The_scopes_verdicts_are_the_shared_tables_rows` holds the verdicts to the rows).
 */

type Switch = CurveScope['switch'];
type Curve = Record<string, CurvePoint[]>;
type Said = [window: string, used: number | null, reset: string];
type Tightest = { window: string; curve: number; room: number } | null;

// From the page's package, where its suite runs, as `shellWord.test.ts` reads its table.
const TABLE = JSON.parse(readFileSync(join(process.cwd(), '..', 'Daoris.Cli', 'test', 'fixtures', 'usage-curves.json'), 'utf8')) as {
  now: string;
  tightest: [name: string, switched: Switch, curve: Curve | null, said: Said[], tightest: Tightest, clause: string | null][];
};
const NOW = new Date(TABLE.now);

/** The verdict .NET pushes for a row: its tightest window over the curve, the curve's value a whole percent, a half away from zero. */
const pushed = (tightest: Tightest) => (tightest === null || tightest.room >= 0 ? null : {
  window: tightest.window, curve: Math.sign(tightest.curve) * Math.round(Math.abs(tightest.curve)),
});

describe('the usage curve, as .NET judges it and the page words it (PUSH1e)', () => {
  it('reads the table at all: a table that holds nothing passes every row below', () => {
    expect(TABLE.tightest.length).toBeGreaterThan(0);
  });

  /**
   * SWITCH1d: the page's said line marks a window over its curve with the curve's value now, as the terminal's `saidLine` and the
   * start's line do, each row's clause in the page's words, which name the session window *five-hour*.
   */
  for (const [name, , , said, tightest, clause] of TABLE.tightest) {
    it(`the said line: ${name}`, () => {
      const windows = said.map(([window, used, reset]) => ({ window, used, reset, credits: false, seen: NOW.toISOString() }));
      const line = saidLine({ seen: NOW.toISOString(), windows }, { over: pushed(tightest) });
      if (clause === null) expect(line).not.toMatch(/over its/);
      else expect(line.endsWith(`; ${clause.replace('its session curve', 'its five-hour curve')}`)).toBe(true);
    });
  }

  it('leads the said line by where its newest reading came from', () => {
    const seen = NOW.toISOString();
    const read = { seen, windows: [{ window: 'session', used: 0.2, reset: '2026-10-13T13:00:00Z', credits: false, seen, source: 'read' as const }] };
    expect(saidLine(read)).toMatch(/^read .+: 20% of its five-hour limit used/);
  });

  it('reads the person’s own points of a window, and none where they set none', () => {
    expect(ownPoints({ switch: 'curve', curve: { session: [[0, 80]] } }, 'session')).toEqual([[0, 80]]);
    expect(ownPoints({ switch: 'smart', curve: { session: [[0, 80]] } }, 'weekly')).toBeNull();
    expect(ownPoints({}, 'constructor')).toBeNull();
  });
});

/**
 * A curve's rules, said by the door (SWITCH1d; the switch design §2; PUSH1e): the point editor saves, and a curve that breaks a rule
 * is refused by the modules' `ACCOUNT_USE_CURVE`, its rule the catalogue's form and its values the window, the point and the numbers.
 * 🔴 The page's half of the refusal's words: each rule's values as the door sends them (the same rows as the modules'
 * `A_curve_that_breaks_a_rule_is_refused_in_its_code_naming_the_rule`) say, in English, the sentence of the CLI's `rotation-use.json`
 * `refusal` row for that curve, word for word, so the catalogue cannot drift from the terminal's sentence.
 */
describe('a curve’s rules, as the door refuses them', () => {
  type Row = [why: string, curve: string, refused: string | null];
  const ROWS = (JSON.parse(readFileSync(join(process.cwd(), '..', 'Daoris.Cli', 'test', 'fixtures', 'rotation-use.json'), 'utf8')) as {
    refusal: Row[];
  }).refusal;
  const refusalOf = (curve: unknown) => ROWS.find(([, written]) => JSON.stringify(JSON.parse(written)) === JSON.stringify(curve))?.[2];

  /** Each rule's curve and the values the door refuses it with, as the modules' theory holds them. */
  const DOOR: [curve: unknown, values: Record<string, string>][] = [
    [{ weekly: [[0, 30], [50, 70], [100, 60]] }, { context: 'falling', window: 'weekly', point: '3', previous: '2', value: '60', before: '70' }],
    [{ weekly: [[50, 30], [40, 70]] }, { context: 'rising', window: 'weekly', point: '2', previous: '1', value: '40', before: '50' }],
    [{ monthly: [[0, 50]] }, { context: 'window', window: 'monthly' }],
    [{ weekly: '0:30' }, { context: 'list', window: 'weekly' }],
    [{ weekly: [] }, { context: 'count', window: 'weekly', size: '0' }],
    [{ weekly: [[0]] }, { context: 'point', window: 'weekly', point: '1', previous: '0' }],
    [{ session: [[-5, 90]] }, { context: 'elapsed', window: 'session', point: '1', previous: '0', value: '-5' }],
    [{ session: [[0, 101]] }, { context: 'used', window: 'session', point: '1', previous: '0', value: '101' }],
    [[[0, 30]], { context: 'shape' }],
  ];

  beforeEach(() => i18n.changeLanguage('en'));

  for (const [curve, values] of DOOR) {
    it(`${values.context}: said as the terminal says it`, () => {
      const refused = refusalOf(curve);
      expect(refused).toBeTruthy();
      expect(sentence({ code: 'ACCOUNT_USE_CURVE', parameters: { ...values, said: refused! } })).toBe(refused);
    });
  }

  it('says it in 中文, with the window and the numbers', async () => {
    await i18n.changeLanguage('zh');
    expect(sentence({ code: 'ACCOUNT_USE_CURVE', parameters: DOOR[0]![1] }))
      .toBe('`weekly` 的第 3 行已用 60%，少于第 2 行的 70%——已用量不会下降。');
  });

  it('says a rule this page has no form for by the terminal’s own sentence', () => {
    expect(sentence({ code: 'ACCOUNT_USE_CURVE', parameters: { context: 'newer', said: 'A newer rule.' } }))
      .toBe('That curve cannot be used: A newer rule.');
  });
});
