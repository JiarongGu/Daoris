import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { TYPE_STEPS } from '../lib/cn';
import { contrast, hueAngle, hueApart, palettes } from '../test/census/palette';

/**
 * **The role set reads in both themes** (D330, the design §7.1). Components name roles; `tokens.css` holds the values, and
 * this holds the contrast table on them: every text pair at 4.5:1 on every surface and every role's soft field, every mark,
 * focus ring and control edge at 3:1 on the grounds and on its own field, and each label on its solid fill at 4.5:1. The
 * design's worst cases are pinned, so a value moved is a diff here as well as there.
 */
const tokensCss = readFileSync(join(process.cwd(), 'src', 'tokens.css'), 'utf8');

const TEXT = ['--ink', '--ink-soft', '--ink-faint', '--accent-ink', '--live-ink', '--you-ink', '--fail-ink', '--ok-ink'];
const GROUNDS = ['--page', '--raised', '--sunken', '--overlay', '--hover'];
const FIELDS = ['--accent-soft', '--live-soft', '--you-soft', '--fail-soft', '--ok-soft'];
const MARKS: [mark: string, field: string][] = [
  ['--accent', '--accent-soft'], ['--live', '--live-soft'], ['--you', '--you-soft'], ['--fail', '--fail-soft'], ['--ok', '--ok-soft'],
  ['--line-strong', '--raised'],
];
const LABELS: [label: string, fill: string][] = [['--on-accent', '--accent'], ['--on-you', '--you'], ['--on-fail', '--fail']];
const ROLES = [...TEXT, ...GROUNDS, ...FIELDS, ...MARKS.map(([mark]) => mark), ...LABELS.map(([label]) => label),
  '--line-subtle', '--line', '--ink-disabled'];

type Pair = { on: string; ratio: number; label: string };

/** Each pair a theme draws, by kind: words, marks and labels. */
export function pairs(tokens: Record<string, string>): Record<'text' | 'marks' | 'labels', Pair[]> {
  const pair = (fore: string, back: string): Pair => ({ on: fore, ratio: contrast(tokens[fore]!, tokens[back]!), label: `${fore} on ${back}` });
  return {
    text: TEXT.flatMap((ink) => [...GROUNDS, ...FIELDS].map((surface) => pair(ink, surface))),
    marks: MARKS.flatMap(([mark, field]) => [...new Set([...GROUNDS, field])].map((surface) => pair(mark, surface))),
    labels: LABELS.map(([label, fill]) => pair(label, fill)),
  };
}

const below = (list: Pair[], floor: number) => list.filter((each) => each.ratio < floor).map((each) => `${each.label}: ${each.ratio.toFixed(2)}`);
const worst = (list: Pair[], fore?: string) =>
  Math.min(...list.filter((each) => fore === undefined || each.on === fore).map((each) => each.ratio)).toFixed(1);

const themes = (css = tokensCss) => {
  const read = palettes(css);
  return { light: read['system-light'], dark: read['system-dark'] };
};

describe('the colour roles', () => {
  it('declares every role as a colour in both themes', () => {
    for (const [theme, tokens] of Object.entries(themes())) {
      for (const role of ROLES) expect(tokens[role], `${theme} ${role}`).toMatch(/^#[0-9a-f]{6}$/);
    }
  });

  it('catches a pair under its floor — the check itself, with the faint ink the role set raised', () => {
    const before = tokensCss.replace(/^(:root\s*\{[^}]*--ink-faint:\s*)#[0-9a-fA-F]{6}/m, '$1#8a887f');
    expect(before).not.toBe(tokensCss);
    expect(below(pairs(themes(before).light).text, 4.5)).toContain('--ink-faint on --page: 3.29');
    // And the control edge the role set darkened, a line under every field.
    const faded = tokensCss.replace(/^(:root\s*\{[^}]*--line-strong:\s*)#[0-9a-fA-F]{6}/m, '$1#cfcabe');
    expect(below(pairs(themes(faded).light).marks, 3).filter((each) => each.startsWith('--line-strong on --raised'))).toHaveLength(1);
  });

  it('holds: every text pair reads at 4.5:1, every mark and edge at 3:1, every label on its fill at 4.5:1, in both themes', () => {
    for (const [theme, tokens] of Object.entries(themes())) {
      const drawn = pairs(tokens);
      expect(below(drawn.text, 4.5), `${theme} words`).toEqual([]);
      expect(below(drawn.marks, 3), `${theme} marks`).toEqual([]);
      expect(below(drawn.labels, 4.5), `${theme} labels`).toEqual([]);
    }
  });

  it("holds the design's table: its worst pairs, as §7.1 states them", () => {
    const { light, dark } = themes();
    expect([worst(pairs(light).text, '--ink-faint'), worst(pairs(dark).text, '--ink-faint')]).toEqual(['4.9', '4.7']);
    expect([worst(pairs(light).marks, '--line-strong'), worst(pairs(dark).marks, '--line-strong')]).toEqual(['3.1', '3.0']);
    expect(worst(pairs(light).marks, '--you')).toBe('3.2');
    expect(worst(pairs(light).labels, '--on-you')).toBe('4.6');
  });

  it("holds: the owner's amber is no kin of the accent, so a wait is told from a button at a glance", () => {
    for (const [theme, tokens] of Object.entries(themes())) {
      const apart = hueApart(hueAngle(tokens['--you']!), hueAngle(tokens['--accent']!));
      expect(apart, `${theme} --you against --accent: ${apart.toFixed(0)}°`).toBeGreaterThan(90);
    }
  });

  it('holds: the moving mark is the accent under a second name', () => {
    for (const tokens of Object.values(themes())) {
      expect([tokens['--live'], tokens['--live-ink']]).toEqual([tokens['--accent'], tokens['--accent-ink']]);
    }
  });
});

/** Each `--text-<step>` the theme declares, with its size in px and its line height as written. */
export function steps(css: string): { step: string; px: number; leading: string }[] {
  return [...css.matchAll(/^\s*--text-([a-z]+)\s*:\s*([\d.]+)rem\s*;/gm)].map((match) => ({
    step: match[1]!,
    px: Number(match[2]) * 16,
    leading: new RegExp(String.raw`--text-${match[1]}--line-height\s*:\s*([^;]+);`).exec(css)?.[1]?.trim() ?? '',
  }));
}

/** The line heights a selector's block sets, by step, in px. */
function leadings(css: string, selector: RegExp): Record<string, number> {
  const body = selector.exec(css)?.[1] ?? '';
  return Object.fromEntries([...body.matchAll(/--leading-([a-z]+)\s*:\s*([\d.]+)rem\s*;/g)].map((m) => [m[1]!, Number(m[2]) * 16]));
}

const LATIN = /^:root,\s*\[lang\]:not\(:lang\(zh\)\)\s*\{([^}]*)\}/m;
const HAN = /^:lang\(zh\)\s*\{([^}]*)\}/m;

describe('the type scale', () => {
  it('declares the steps the class merge knows, each in rem', () => {
    expect(steps(tokensCss).map((each) => each.step).sort()).toEqual([...TYPE_STEPS].sort());
  });

  it('catches a step under 12 px — the check itself, with the 11 px step the role set removed', () => {
    const before = tokensCss.replace(/(--text-caption\s*:\s*)[\d.]+rem/, '$10.6875rem');
    expect(steps(before).filter((each) => each.px < 12).map((each) => each.step)).toEqual(['caption']);
  });

  it('holds: no step is under 12 px, so Han is never drawn smaller', () => {
    expect(steps(tokensCss).filter((each) => each.px < 12)).toEqual([]);
  });

  it("holds: each step's line height is a variable, and Chinese sets it at least as tall as Latin", () => {
    const latin = leadings(tokensCss, LATIN);
    const han = leadings(tokensCss, HAN);
    for (const { step, leading } of steps(tokensCss)) {
      expect(leading, `--text-${step}'s line height`).toBe(`var(--leading-${step})`);
      expect(latin[step], `the Latin leading of ${step}`).toBeGreaterThan(0);
      expect(han[step], `the Han leading of ${step}`).toBeGreaterThanOrEqual(latin[step]!);
    }
    // The body step, where the difference shows: 20 px in Latin, 22 in Han.
    expect([latin.body, han.body]).toEqual([20, 22]);
  });

  it('holds: under Chinese the line breaks strictly, the face sets full-width punctuation, and no Han is set italic', () => {
    const han = HAN.exec(tokensCss)?.[1] ?? '';
    expect(han).toMatch(/line-break:\s*strict/);
    expect(han).toMatch(/--font-ui:\s*"Daoris Han Punct"/);
    expect(tokensCss).toMatch(/@font-face\s*\{[^}]*font-family:\s*"Daoris Han Punct"[^}]*unicode-range:\s*U\+2018-201D, U\+2026, U\+00B7, U\+2014/);
    expect(tokensCss).not.toMatch(/--font-serif/);
  });
});
