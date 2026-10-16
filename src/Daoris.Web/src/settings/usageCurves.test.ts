import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { beforeEach, describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import { sentence } from '../locales/format';
import { saidLine } from './accounts';
import {
  curveNow, curveProblem, curveSentence, type CurveScope, type CurvePoint, tightestSaid, wholePoints,
} from './usageCurves';

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

  /**
   * SWITCH1d: the page's said line marks a window over its curve with the curve's value now, as the terminal's `saidLine` and
   * the start's line do, each row's clause in the page's words, which name the session window *five-hour*.
   */
  for (const [name, switched, curve, said, , clause] of TABLE.tightest) {
    it(`the said line: ${name}`, () => {
      const windows = said.map(([window, used, reset]) => ({ window, used, reset, credits: false, seen: NOW.toISOString() }));
      const line = saidLine({ seen: NOW.toISOString(), windows }, { use: scope(switched, curve), now: NOW });
      if (clause === null) expect(line).not.toMatch(/over its/);
      else expect(line.endsWith(`; ${clause.replace('its session curve', 'its five-hour curve')}`)).toBe(true);
    });
  }

  it('leads the said line by where its newest reading came from', () => {
    const seen = NOW.toISOString();
    const read = { seen, windows: [{ window: 'session', used: 0.2, reset: '2026-10-13T13:00:00Z', credits: false, seen, source: 'read' as const }] };
    expect(saidLine(read)).toMatch(/^read .+: 20% of its five-hour limit used/);
  });

  it('says a number of points as a whole number, a half away from zero', () => {
    expect([wholePoints(0.5), wholePoints(-0.5), wholePoints(62.4)]).toEqual([1, -1, 62]);
  });

  it('reads a scope from a shell older than the switch as the smart curve', () => {
    expect(curveNow({}, 'session', '2026-10-13T13:00:00Z', NOW)).toBe(90);
  });
});

/**
 * A curve's rules on the page (SWITCH1d; the switch design §2): the point editor says what is wrong before it saves, in the
 * sentence the modules' `ACCOUNT_USE_CURVE` refusal is said in, its rule the catalogue's form. 🔴 The page's half of the twin the
 * CLI's `curveProblem` and the driver's `UseCurve.ProblemOf` keep: every `refusal` row of the CLI's `rotation-use.json` is found
 * by the page's rule and said in English word for word, so the catalogue cannot drift from the terminal's sentence.
 */
describe('a curve’s rules, as both doors refuse them', () => {
  type Row = [why: string, curve: string, refused: string | null];
  const ROWS = (JSON.parse(readFileSync(join(process.cwd(), '..', 'Daoris.Cli', 'test', 'fixtures', 'rotation-use.json'), 'utf8')) as {
    refusal: Row[];
  }).refusal;

  beforeEach(() => i18n.changeLanguage('en'));

  it('reads the table at all', () => {
    expect(ROWS.length).toBeGreaterThan(0);
  });

  for (const [why, curve, refused] of ROWS) {
    it(`${why}: said as the terminal says it`, () => {
      const problem = curveProblem(JSON.parse(curve));
      expect(problem === null ? null : curveSentence(problem)).toBe(refused);
    });
  }

  it('says it in 中文, with the window and the numbers', async () => {
    await i18n.changeLanguage('zh');
    expect(curveSentence(curveProblem({ weekly: [[0, 30], [50, 70], [100, 60]] })!))
      .toBe('`weekly` 的第 3 个点已用 60%，少于第 2 个点的 70%——已用量不会下降。');
  });

  it('says a rule this page has no form for by the terminal’s own sentence', () => {
    expect(sentence({ code: 'ACCOUNT_USE_CURVE', parameters: { context: 'newer', said: 'A newer rule.' } }))
      .toBe('That curve cannot be used: A newer rule.');
  });
});
