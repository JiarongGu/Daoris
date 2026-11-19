import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { TYPE_STEPS } from '../lib/cn';
import { rooted } from '../test/sources';
import { palettes } from '../test/census/palette';

/**
 * **The type scale is a set of named steps, and a raw size is not one of them**.
 *
 * The rule is held by a test because the literal form can drift, silently and one
 * component at a time: code once carried **fifteen** distinct `text-[…rem]` values where the scale named
 * eight, and four of them — 0.7, 0.78, 0.82, 0.85 — belonged to no scale at all. Nothing reported
 * it, because a hardcoded size is valid Tailwind and renders perfectly.
 *
 * A named step cannot drift without someone adding one to `tokens.css`, which is a diff a reviewer
 * sees. That is the whole difference.
 */
const RAW_SIZE = /\btext-\[[0-9.]+(?:rem|px|em)\]/g;

export function rawSizes(files: [path: string, source: string][]): string[] {
  return files.flatMap(([path, source]) =>
    (source.match(RAW_SIZE) ?? []).map((hit) => `${path} hardcodes ${hit}`));
}

/** The type steps `tokens.css` declares: each `--text-<step>`, its line height aside. */
export function declaredSteps(css: string): string[] {
  return [...css.matchAll(/^\s*--text-([a-z]+)\s*:/gm)].map((match) => match[1]!);
}

/** The colours it declares (`--color-<name>`), which share the `text-` prefix with the steps. */
export function declaredColours(css: string): string[] {
  return [...css.matchAll(/^\s*--color-([a-z][a-z-]*)\s*:/gm)].map((match) => match[1]!);
}

/** Tailwind's `text-` utilities that are no size and no colour: alignment, wrapping and overflow. */
const LAYOUTS = ['left', 'center', 'right', 'justify', 'start', 'end', 'wrap', 'nowrap', 'balance', 'pretty', 'ellipsis', 'clip'];

/**
 * A `text-` class, read whole: `text-h3` is `h3`. A scan that read letters alone up to a word's end takes
 * `text-h3` for nothing at all, so a title wears a step no token defines and is drawn at the
 * body's size, with every check green.
 */
const TEXT_CLASS = /(?<![\w-])text-([a-z][a-z0-9]*(?:-[a-z0-9]+)*)/g;

/**
 * Tailwind's colour keywords that need no theme. `tokens.css` clears Tailwind's palette (`--color-*: initial`), so
 * `bg-white` or `border-red-500` draws nothing, while these three are values of their own and still draw.
 */
const BUILT_IN_COLOURS = ['transparent', 'current', 'inherit'];

/** Every `text-` class that names no step, colour or layout `tokens.css` and Tailwind declare. */
export function undeclaredSteps(files: [path: string, source: string][], css: string): string[] {
  const known = new Set([...declaredSteps(css), ...declaredColours(css), ...LAYOUTS, ...BUILT_IN_COLOURS]);
  return files.flatMap(([path, source]) => [...source.matchAll(TEXT_CLASS)]
    .filter((match) => !known.has(match[1]!))
    .map((match) => `${path} wears text-${match[1]}, which tokens.css does not declare`));
}

/**
 * 🔴 **A colour no token defines draws nothing, and says nothing**. Thirteen files asked for `bg-sunken` for
 * the box a destructive move asks in, and `tokens.css` declared no such colour: Tailwind emits no rule for a name
 * its theme lacks, so every one of those boxes had no background in either theme, with every check green. The
 * `text-` scan above already held the type steps; a `bg-` or `border-` colour had nothing.
 *
 * A `bg-` or `border-` class is read whole, its opacity aside (`bg-ok/40` is `ok`), and what is not a
 * colour is set aside by its shape: a background's attachment, clip, origin, repeat, size, position, image or
 * blend; a border's width, side, style or table layout. A side may carry a colour (`border-l-st-open`). A class
 * built from a variable (`bg-st-${state}`) and an arbitrary value (`bg-[…]`) are not names, so neither is read.
 */
const COLOUR_CLASS = /(?<![\w-])(bg|border)-([a-z][a-z0-9]*(?:-[a-z0-9]+)*)(?![a-z0-9]|-[a-z0-9]|-?\$\{)/g;

const NOT_A_BACKGROUND_COLOUR = /^(?:fixed|local|scroll|none|auto|cover|contain|center|top|bottom|left|right|repeat|no-repeat)$|^(?:clip|origin|repeat|size|position|linear|radial|conic|gradient|blend|top|bottom|left|right)-/;
const NOT_A_BORDER_COLOUR = /^(?:\d+|solid|dashed|dotted|double|hidden|none|collapse|separate)$|^spacing(?:-|$)/;
const BORDER_SIDE = /^(?:x|y|t|r|b|l|s|e|bs|be)(?:-(.+))?$/;

/** The colour a `bg-` or `border-` class names, or null where it names none. */
function colourNamed(utility: string, value: string): string | null {
  if (utility === 'bg') return NOT_A_BACKGROUND_COLOUR.test(value) ? null : value;
  const side = BORDER_SIDE.exec(value);
  const rest = side ? side[1] : value;
  return rest === undefined || NOT_A_BORDER_COLOUR.test(rest) ? null : rest;
}

/** Every `bg-` and `border-` class whose colour is neither a token `tokens.css` declares nor a built-in that draws. */
export function undeclaredColours(files: [path: string, source: string][], css: string): string[] {
  const known = new Set([...declaredColours(css), ...BUILT_IN_COLOURS]);
  return files.flatMap(([path, source]) => [...source.matchAll(COLOUR_CLASS)]
    .filter((match) => {
      const colour = colourNamed(match[1]!, match[2]!);
      return colour !== null && !known.has(colour);
    })
    .map((match) => `${path} wears ${match[0]}, a colour tokens.css does not declare`));
}

/**
 * 🔴 **A scrim never covers the app strip**, where the window's three buttons are.
 *
 * The strip reserves three 44px slots that the **window** paints natively. A page-level
 * backdrop dims everything the page draws and cannot touch what the window draws — so opening the
 * palette dimmed the whole title bar and left the caption buttons as a **bright white block punched
 * through it**. Seen in a screenshot; invisible in every unit test, because there are no native
 * buttons in jsdom and no window in a browser.
 *
 * It is also the behaviour VS Code has: its title bar stays live while quick-open is up.
 *
 * The rule is the position, so the check is on the position: an overlay starts at `top-9` — the
 * strip's own height — not at `inset-0`.
 */
const FULL_BLEED_SCRIM = /fixed inset-0[^"'`]*bg-scrim|bg-scrim[^"'`]*fixed inset-0/g;

export function scrimsOverTheStrip(files: [path: string, source: string][]): string[] {
  return files.flatMap(([path, source]) =>
    (source.match(FULL_BLEED_SCRIM) ?? []).map((hit) => `${path} scrims the app strip: ${hit}`));
}

/**
 * 🔴 **And neither does a PANEL.** The scrim learned to start below the strip; the drawer beside it
 * did not, and stayed `inset-y-0` — so on the deployed application its header, close button
 * included, sat exactly where the window paints its caption buttons. The × sat under the ✕: not
 * dimmed, not clickable, not visible. Seen in a screenshot of a quest drawer, on the second
 * deployment; in a browser there is no caption to hide behind.
 */
const FULL_HEIGHT_PANEL = /fixed inset-y-0[^"'`]*bg-overlay|bg-overlay[^"'`]*fixed inset-y-0/g;

export function panelsOverTheStrip(files: [path: string, source: string][]): string[] {
  return files.flatMap(([path, source]) =>
    (source.match(FULL_HEIGHT_PANEL) ?? []).map((hit) => `${path} raises a panel over the app strip: ${hit}`));
}

/**
 * 🔴 **Nor does either reach the status bar.** The frame is three bars, the strip, the activity bar
 * and the status bar, and an overlay belongs to the content between them. The scrim left the first
 * two alone and ran to `bottom-0`, so behind an open drawer the status bar's first 48px stayed
 * bright and the rest went grey, cut at the activity bar's edge. Seen on the installed window.
 * The rule is the position again: an overlay ends at `bottom-6`, the bar's own height.
 */
const OVER_THE_STATUS_BAR = /fixed[^"'`]*\bbottom-0\b[^"'`]*bg-(?:scrim|overlay)|bg-(?:scrim|overlay)[^"'`]*fixed[^"'`]*\bbottom-0\b/g;

export function overlaysOverTheStatusBar(files: [path: string, source: string][]): string[] {
  return files.flatMap(([path, source]) =>
    (source.match(OVER_THE_STATUS_BAR) ?? []).map((hit) => `${path} covers the status bar: ${hit}`));
}

const sources = rooted(import.meta.glob('/src/**/*.tsx', {
  eager: true, query: '?raw', import: 'default',
}) as Record<string, string>);

const components = Object.entries(sources).filter(([path]) => !/\.test\.tsx$/.test(path));

describe('the type scale', () => {
  it('catches a raw size — the check itself, sabotaged in the shapes a regression takes', () => {
    // The shape the sweep replaced.
    expect(rawSizes([['./ui.tsx', 'className="text-[0.85rem] text-ink"']]))
      .toEqual(['./ui.tsx hardcodes text-[0.85rem]']);
    // The shapes someone would reach for NEXT, which a rem-only check would wave through.
    expect(rawSizes([['./ui.tsx', 'className="text-[13px]"']])).toHaveLength(1);
    expect(rawSizes([['./ui.tsx', 'className="text-[1.2em]"']])).toHaveLength(1);
    // And what must keep passing.
    expect(rawSizes([['./ui.tsx', 'className="text-body text-ink-soft"']])).toEqual([]);
    expect(rawSizes([['./ui.tsx', 'className="w-[18rem] leading-[1.35]"']])).toEqual([]);
  });

  it('is looking at files at all — a vacuous check is a check that has stopped working', () => {
    expect(components.length).toBeGreaterThan(10);
  });

  it('holds: every size in the platform is a named step', () => {
    expect(rawSizes(components)).toEqual([]);
  });

  it('declares the seven steps the class merge knows, and reads its colours', () => {
    expect(declaredSteps(tokensCss).sort()).toEqual([...TYPE_STEPS].sort());
    expect(declaredColours(tokensCss)).toEqual(expect.arrayContaining(['ink', 'ink-faint', 'accent-ink', 'fail']));
  });

  it('catches a class that names no step — the check itself, in the shape the monitor\'s title took', () => {
    const wears = (source: string) => undeclaredSteps([['./work/MonitorWindow.tsx', source]], tokensCss);
    expect(wears('<h1 className="m-0 text-h3">')).toEqual(['./work/MonitorWindow.tsx wears text-h3, which tokens.css does not declare']);
    // Tailwind's own scale renders and is not the platform's.
    expect(wears('className="text-sm"')).toHaveLength(1);
    expect(wears('className="hover:text-xl"')).toHaveLength(1);
    // And what must keep passing: a step, a colour with and without its opacity, a layout.
    expect(wears('className="text-display text-ink-soft hover:text-fail text-ink/70 text-pretty text-left"')).toEqual([]);
    // Tailwind's colour keywords draw though its palette is cleared.
    expect(wears('className="text-current text-inherit text-transparent"')).toEqual([]);
    // A raw size is the other check's, and a word that merely contains the prefix is no class.
    expect(wears('className="text-[0.85rem] context-text-h3"')).toEqual([]);
  });

  it('holds: every text- class names a step, a colour or a layout tokens.css or Tailwind declares', () => {
    expect(undeclaredSteps([...components, ...modules], tokensCss)).toEqual([]);
  });
});

describe('the colours', () => {
  const wears = (source: string, css = tokensCss) => undeclaredColours([['./asks/AskPage.tsx', source]], css);

  it('catches a colour no token defines — the check itself, in the shape bg-sunken took', () => {
    // tokens.css without a sunken colour: the box asked for a colour the theme did not have.
    const before = tokensCss.replace(/^\s*--color-sunken\s*:.*$/m, '');
    expect(before).not.toBe(tokensCss);
    expect(wears('className="mb-4 flex rounded-control border border-line bg-sunken px-2.5 py-2"', before))
      .toEqual(['./asks/AskPage.tsx wears bg-sunken, a colour tokens.css does not declare']);
    // Tailwind's own palette, which tokens.css clears, behind a variant, with an opacity, and on one side.
    expect(wears('className="hover:bg-white"')).toHaveLength(1);
    expect(wears('className="border-red-500/40"')).toHaveLength(1);
    expect(wears('className="border-l-muted"')).toHaveLength(1);
    expect(wears("cn('text-ink', busy && 'bg-muted')")).toHaveLength(1);
  });

  it('leaves what is a token, a built-in or no colour at all', () => {
    expect(wears([
      'className="bg-raised bg-page/60 hover:bg-accent-soft data-[state=checked]:bg-accent bg-ok-soft bg-scrim',
      'bg-transparent bg-inherit border-current border-transparent bg-ident-moss/15',
      'border border-0 border-2 border-x border-t border-b-0 border-l-[3px] border-l-st-open border-t-line',
      'border-dashed border-none border-collapse border-spacing-2 border-line-strong hover:enabled:border-accent',
      'bg-cover bg-center bg-left-top bg-no-repeat bg-clip-text bg-linear-to-r bg-[url(x)] border-[#cfcabe]"',
      // A class built from a variable is not a name, and a word that merely holds the prefix is no class.
      'className={`bg-st-${state} border-st-${state}`} data-bg-sunken="x" box-border',
    ].join(' '))).toEqual([]);
  });

  it('holds: every bg- and border- colour a web source names is one tokens.css declares', () => {
    expect(undeclaredColours([...components, ...modules], tokensCss)).toEqual([]);
  });
});

describe('the scrim', () => {
  it('catches a full-bleed scrim — the check itself, in the shape the regression took', () => {
    expect(scrimsOverTheStrip([['./ui.tsx', '<Dialog.Overlay className="fixed inset-0 z-10 bg-scrim" />']]))
      .toHaveLength(1);
    // And what must keep passing: a scrim that starts below the strip.
    expect(scrimsOverTheStrip([['./ui.tsx', 'className="fixed inset-x-0 bottom-0 top-9 z-10 bg-scrim"']]))
      .toEqual([]);
  });

  it('holds: no overlay covers the strip the window paints its buttons into', () => {
    expect(scrimsOverTheStrip(components)).toEqual([]);
  });

  it('catches a full-height panel — the shape the drawer had', () => {
    expect(panelsOverTheStrip([['./ui.tsx',
      '<Dialog.Content className="fixed inset-y-0 right-0 z-10 flex w-[min(32rem,100%)] flex-col bg-overlay">']]))
      .toHaveLength(1);
    // And what must keep passing: a panel that starts below the strip.
    expect(panelsOverTheStrip([['./ui.tsx', 'className="fixed bottom-0 right-0 top-9 z-10 flex bg-overlay"']]))
      .toEqual([]);
  });

  it('holds: no panel puts its own header under the caption buttons', () => {
    expect(panelsOverTheStrip(components)).toEqual([]);
  });

  it('catches a scrim or a panel that runs over the status bar, in the shape both had', () => {
    expect(overlaysOverTheStatusBar([['./ui.tsx',
      '<Dialog.Overlay className="fixed inset-y-0 bottom-0 left-12 right-0 top-9 z-10 bg-scrim" />']]))
      .toHaveLength(1);
    expect(overlaysOverTheStatusBar([['./ui.tsx',
      'className="fixed bottom-0 right-0 top-9 z-10 flex w-[min(32rem,100%)] flex-col bg-overlay"']]))
      .toHaveLength(1);
    // And what must keep passing: both ending above the bar.
    expect(overlaysOverTheStatusBar([['./ui.tsx', 'className="fixed bottom-6 left-12 right-0 top-9 z-10 bg-scrim"']]))
      .toEqual([]);
  });

  it('holds: no overlay covers the status bar', () => {
    expect(overlaysOverTheStatusBar(components)).toEqual([]);
  });
});

/**
 * 🔴 **A chosen theme is the same palette as the system's**. `tokens.css` carries each theme
 * twice — once for the OS's media query, once for `data-theme` — because a chosen theme has to
 * outrank the query and CSS has no way to say "this block, again, under another selector". Two
 * copies are two palettes the first time one is edited alone, and the edit would look right in
 * whichever the person happens to be using. So every value is held equal here (`palettes`, `test/census/palette.ts`).
 */

// From disk, not `?raw`: the CSS pipeline answers a raw import of a stylesheet with an empty string,
// which is how the first version of this check came to hold nothing equal to nothing.
const tokensCss = readFileSync(join(process.cwd(), 'src', 'tokens.css'), 'utf8');

describe('the chosen themes', () => {
  it('reads every block — a parser that stopped reading would hold nothing equal to nothing', () => {
    const read = palettes(tokensCss);
    for (const [name, values] of Object.entries(read)) {
      expect(Object.keys(values).length, `${name} parsed ${Object.keys(values).length} tokens`).toBeGreaterThanOrEqual(15);
    }
    expect(read['system-light']['--page']).not.toBe(read['system-dark']['--page']);
  });

  it('catches a chosen block that drifted from its system twin', () => {
    const drifted = tokensCss.replace(/(:root\[data-theme="dark"\]\s*\{[^}]*--page:\s*)#131312/, '$1#000000');
    expect(drifted).not.toBe(tokensCss);
    expect(palettes(drifted)['chosen-dark']).not.toEqual(palettes(drifted)['system-dark']);
  });

  it('holds: chosen light is the system light, chosen dark is the system dark', () => {
    const read = palettes(tokensCss);
    expect(read['chosen-light']).toEqual(read['system-light']);
    expect(read['chosen-dark']).toEqual(read['system-dark']);
  });
});

/**
 * 🔴 **A status word wears its role's ink, never the mark's colour**. The hues are drawn as marks, edges and fills; as
 * words they fell under the 4.5:1 text floor (declined's red at 3.18:1 on an overlay, open's and done's at 2.8 to 3.8:1 in
 * light, UXFIX5), and the role set's marks still do on some surfaces (`fail` on `sunken`, `live` on `hover` in dark). Each
 * role's `-ink` is the colour its word wears, which `roles.test.ts` holds at 4.5:1 everywhere; an alias of the owner's
 * amber is read the same way until UIDS18.
 */
const HUES = '(?:live|you|fail|ok|st-open)';
const STATUS_AS_TEXT = new RegExp(String.raw`(?<![\w-])text-${HUES}(?![\w-])`, 'g');
const STATUS_AS_CSS_TEXT = new RegExp(String.raw`(?<![\w-])color\s*:\s*var\(--${HUES}\)`, 'g');
/** An SVG word takes its colour from `fill`, as the map's *waiting on you* does; a shape's fill is a mark and is left. */
const STATUS_AS_SVG_TEXT = new RegExp(String.raw`<text\b[^>]*?(?<![\w-])(fill-${HUES})(?![\w-])`, 'g');

/**
 * Where a source may still draw a status hue through its colour, each hit with its reason and each allowed once, so a
 * second hit of the same class in that file still fails. A mark keeps its hue (platform-ux §3), and an icon or a glyph
 * takes its colour as a word does, so the scan is told which they are. A word in a file a branch in flight holds waits
 * for that branch, and its row goes when the swap lands. A red word is allowed nowhere.
 */
const HUE_AS_COLOUR: Record<string, { hits: string[]; reason: string }> = {
  './work/frame/frame.tsx': { hits: ['text-st-open'], reason: "a mark: the status bar's setup icon, beside its words in the ink" },
  './work/conversation/BlockLines.tsx': {
    hits: ['text-ok'], reason: "a mark: a finished plan entry's check, aria-hidden beside its words",
  },
};

/** Each status hue a source draws through its colour: a `text-` class, a stylesheet's `color`, or an SVG word's `fill`. */
function huesAsColour(source: string): string[] {
  return [
    ...(source.match(STATUS_AS_TEXT) ?? []),
    ...(source.match(STATUS_AS_CSS_TEXT) ?? []),
    ...[...source.matchAll(STATUS_AS_SVG_TEXT)].map((match) => match[1]!),
  ];
}

/** Every status word a source draws in its fill's colour, less the hits `HUE_AS_COLOUR` allows that file, each once. */
export function statusAsText(files: [path: string, source: string][]): string[] {
  return files.flatMap(([path, source]) => {
    const allowed = [...(HUE_AS_COLOUR[path]?.hits ?? [])];
    return huesAsColour(source)
      .filter((hit) => {
        const at = allowed.indexOf(hit);
        if (at >= 0) allowed.splice(at, 1);
        return at < 0;
      })
      .map((hit) => `${path} draws a status word in its fill's colour: ${hit}`);
  });
}

/** The stylesheets beside the sources, read from disk: a raw import of a stylesheet answers an empty string. */
const stylesheets = Object.keys(rooted(import.meta.glob('/src/**/*.css')))
  .map((path) => [path, readFileSync(join(process.cwd(), 'src', path), 'utf8')] as [string, string]);

describe('the status inks', () => {
  it("catches a status word in its fill's colour, behind a variant, in a stylesheet and on the map, and leaves a fill and a border", () => {
    expect(statusAsText([['./work/Composer.tsx', '<p className="m-0 text-small text-fail">']]))
      .toEqual(["./work/Composer.tsx draws a status word in its fill's colour: text-fail"]);
    expect(statusAsText([['./ui.tsx', "'border-st-open text-st-open bg-st-open/10' 'text-live' 'text-ok'"]]))
      .toHaveLength(3);
    expect(statusAsText([['./ui.tsx', "'border-fail hover:text-fail text-fail/80'"]])).toHaveLength(2);
    expect(statusAsText([['./work/code.css', '.hljs-string {\n  color: var(--ok);\n}']])).toHaveLength(1);
    // The shape the map's word took: an SVG word wears `fill`, on one line and over several.
    expect(statusAsText([['./map/LayeredMap.tsx',
      "<text x={left + 14} y={card.y + 14} className={cn('text-caption', there === 'parked' ? 'fill-st-open' : 'fill-ink-soft')}>"]]))
      .toEqual(["./map/LayeredMap.tsx draws a status word in its fill's colour: fill-st-open"]);
    expect(statusAsText([['./map/MapCanvas.tsx', "<text\n  x={x}\n  className={cn('text-caption', parked ? 'fill-st-open' : 'fill-ink-soft')}\n>"]]))
      .toHaveLength(1);
    // A mark is allowed once in its file: a word beside it in the same hue still fails.
    expect(statusAsText([['./work/frame/frame.tsx', '<Icon name="plan" size={12} className="text-st-open" />']])).toEqual([]);
    expect(statusAsText([['./work/frame/frame.tsx', '<Icon className="text-st-open" /><span className="text-st-open">2</span>']]))
      .toHaveLength(1);
    // And what must keep passing: the inks themselves, a fill, a border, a mark's stroke or a shape's fill, and a word
    // that holds the prefix.
    expect(statusAsText([['./ui.tsx', [
      "'border border-fail text-fail-ink hover:enabled:bg-fail-soft border-l-fail'",
      "'text-ink-open text-live-ink text-ok-ink fill-ink-open bg-st-open stroke-live border-l-st-open'",
      '<circle className="fill-ok" /> <rect className="fill-st-open" />',
      "'bg-fail context-text-st-done' background-color: var(--st-open); border-color: var(--live);",
      '--color-st-done: var(--ok);',
    ].join(' ')]])).toEqual([]);
  });

  it('is reading the stylesheets too — a scan of none would hold nothing', () => {
    expect(stylesheets.map(([path]) => path)).toEqual(expect.arrayContaining(['./tokens.css', './work/review/code.css']));
  });

  it('holds: every status word in the platform wears its hue\'s ink', () => {
    expect(statusAsText([...components, ...modules, ...stylesheets])).toEqual([]);
  });

  it('holds: no file is allowed a red word, so a new one cannot be waved through (UXFIX5b)', () => {
    for (const [path, { hits }] of Object.entries(HUE_AS_COLOUR)) {
      expect(hits.filter((hit) => hit.endsWith('-fail') || hit.includes('--fail')), path).toEqual([]);
    }
  });

  it('holds: each hit still allowed is still drawn there, so a row goes when its swap lands', () => {
    for (const [path, { hits }] of Object.entries(HUE_AS_COLOUR)) {
      const drawn = huesAsColour(components.find(([each]) => each === path)?.[1] ?? '');
      for (const hit of hits) expect(drawn, `${path} no longer draws ${hit}: drop it from its row`).toContain(hit);
    }
  });
});

/**
 * An ideograph has no italic. The Chinese system face carries none, so the browser slants it by
 * synthesis, and that is what the installed window showed under every italic hint in 中文. The
 * Latin face has a true italic and keeps it: only the synthesis is refused.
 */
describe('the page body', () => {
  it('never synthesises an oblique, so a Chinese sentence set italic stays upright', () => {
    const body = /@layer base\s*\{\s*body\s*\{([^}]*)\}/.exec(tokensCss)?.[1] ?? '';
    expect(body, 'the base body rule was not found').toContain('font-family');
    expect(body).toMatch(/font-synthesis-style:\s*none/);
  });
});

/**
 * 🔴 **A choice is the platform's own control, never the OS's**. The start form's three
 * selects and checkbox, and a review file's *viewed*, as native controls beside the atoms' wear
 * the OS's look and its accent in a palette that draws its own.
 * `SelectField`, `CheckField` and `Segmented` are the controls; only `FormControls` builds one.
 */
const NATIVE_CHOICE = /<select\b|type=["']checkbox["']|type=["']radio["']/g;

export function nativeChoices(files: [path: string, source: string][]): string[] {
  return files
    .filter(([path]) => path !== './design/atoms/FormControls.tsx')
    .flatMap(([path, source]) =>
      (source.match(NATIVE_CHOICE) ?? []).map((hit) => `${path} builds a native control: ${hit}`));
}

/**
 * 🔴 **A date and a count are in the reader's language**. A quest's drawer read *发起
 * 23/09/2026, 1:58:49 pm* in 中文: `toLocaleString()` with no locale takes the machine's, not the
 * page's. `format.ts` holds the helpers that name `i18n.language`, and only it formats.
 */
const LANGUAGE_BLIND = /\.toLocale(?:String|DateString|TimeString)\(\s*\)/g;

export function languageBlind(files: [path: string, source: string][]): string[] {
  return files
    .filter(([path]) => path !== './locales/format.ts')
    .flatMap(([path, source]) =>
      (source.match(LANGUAGE_BLIND) ?? []).map((hit) => `${path} leaves the locale to the machine: ${hit}`));
}

const modules = Object.entries(rooted(import.meta.glob('/src/**/*.ts', {
  eager: true, query: '?raw', import: 'default',
}) as Record<string, string>)).filter(([path]) => !/\.test\.ts$/.test(path));

describe('dates and counts', () => {
  it("catches a locale left to the machine, and leaves format.ts its own", () => {
    expect(languageBlind([['./QuestsView.tsx', '{new Date(detail.filed).toLocaleString()}']])).toHaveLength(1);
    expect(languageBlind([['./App.tsx', 'entries: indexed.toLocaleString( ),']])).toHaveLength(1);
    expect(languageBlind([['./x.tsx', 'd.toLocaleDateString()']])).toHaveLength(1);
    expect(languageBlind([['./x.tsx', 'n.toLocaleString(i18n.language)']])).toEqual([]);
    expect(languageBlind([['./locales/format.ts', 'value.toLocaleString()']])).toEqual([]);
  });

  it("holds: every date and count on the page is in the reader's language", () => {
    expect(languageBlind([...components, ...modules])).toEqual([]);
  });
});

/**
 * 🔴 **A page is one column, and the column is its pane**. A quest's body wore `max-w-prose`, 65ch:
 * on the install at 1600 px its title ran the pane while its body stopped at 456 px, a plugin's detail wrapped at two
 * edges, and in 中文 the 65 Latin digits held about 32 glyphs. Every block of a page now takes the column's width, and
 * the column (`ViewMain`) is the one place a line's length could be set; it sets none.
 *
 * So no source names a line's measure: Tailwind's `max-w-prose`, or a width counted in `ch` or `em`. A cap, if one is
 * ever set, goes on the column once, in `em`, and `work/frame/ViewMain.tsx` is then the one file this check lets
 * name it. A named width (`max-w-3xl`, 48rem) capped five blocks of a quest's and an ask's pages beside blocks that ran
 * the pane, so no product source names one either; a story's frame stands in for the column a part is drawn in, and
 * is not read. That the column itself sets none is `ViewMain.test.tsx`'s to hold, on what it renders.
 */
const LINE_MEASURE = /(?<![\w-])max-w-(?:prose|\[[\d.]+(?:ch|em)\])(?![\w-])/g;
const NAMED_WIDTH = /(?<![\w-])max-w-(?:xs|sm|md|lg|xl|[2-7]xl)(?![\w-])/g;

/** A named width that is no page's block, each with its reason. */
const NAMED_WIDTH_HOMES: Record<string, string> = {
  './work/strip/CommandCenter.tsx': "the strip's command center, a control in the title bar, never a page's block",
};

export function lineMeasures(files: [path: string, source: string][]): string[] {
  return files.flatMap(([path, source]) =>
    (source.match(LINE_MEASURE) ?? []).map((hit) => `${path} sets a line's measure: ${hit}`));
}

export function namedWidths(files: [path: string, source: string][]): string[] {
  return files
    .filter(([path]) => !/\.stories\.tsx$/.test(path) && !(path in NAMED_WIDTH_HOMES))
    .flatMap(([path, source]) =>
      (source.match(NAMED_WIDTH) ?? []).map((hit) => `${path} caps a block at a named width: ${hit}`));
}

describe("the page's measure", () => {
  it("catches a line's measure — the check itself, in the shapes the quest's body and the setup card's note took", () => {
    expect(lineMeasures([['./quests/QuestPage.tsx', '<p className="m-0 max-w-prose whitespace-pre-wrap">']]))
      .toEqual(["./quests/QuestPage.tsx sets a line's measure: max-w-prose"]);
    expect(lineMeasures([['./settings/GetStarted.tsx', '<Prose className="max-w-[28ch] text-small">']])).toHaveLength(1);
    // Behind a variant, and in the unit a future cap would take, which belongs on the column alone.
    expect(lineMeasures([['./x.tsx', 'className="md:max-w-prose"']])).toHaveLength(1);
    expect(lineMeasures([['./x.tsx', "cn('max-w-[72em]')"]])).toHaveLength(1);
    // And what must keep passing: a form's own size in rem, a width relative to the column, a word holding the prefix.
    expect(lineMeasures([['./x.tsx', 'className="max-w-[48rem] max-w-full max-w-none max-w-[min(36rem,100%)] data-max-w-prose"']]))
      .toEqual([]);
  });

  it('catches a block capped at a named width, and leaves a story its frame', () => {
    expect(namedWidths([['./quests/QuestPage.tsx', '<div className="mt-4 max-w-3xl">']]))
      .toEqual(['./quests/QuestPage.tsx caps a block at a named width: max-w-3xl']);
    expect(namedWidths([['./asks/GoAheadList.tsx', 'className="m-0 grid max-w-xl list-none"']])).toHaveLength(1);
    expect(namedWidths([['./work/SessionHead.stories.tsx', '<div className="max-w-3xl"><Story /></div>']])).toEqual([]);
    expect(namedWidths([['./work/strip/CommandCenter.tsx', "'flex w-full min-w-0 max-w-md'"]])).toEqual([]);
    expect(namedWidths([['./x.tsx', 'className="max-w-[48rem] text-xl"']])).toEqual([]);
  });

  it("holds: no source names a line's measure, so every block of a page wraps at the column's edge", () => {
    expect(lineMeasures([...components, ...modules])).toEqual([]);
  });

  it('holds: no product source caps a block at a named width', () => {
    expect(namedWidths([...components, ...modules])).toEqual([]);
  });
});

/**
 * 🔴 **A card sets no outer margin of its own**: cards are 16 px apart, set once by the
 * domain's container (`CardStack`). An `mt-3.5` on a card is 14 px, off the scale's step, and a domain's first card
 * would need `[&>*:first-child]:mt-0` to undo it; two cards that forgot it would touch.
 */
const CARD_MARGIN = /<Card\b[^>]*?className=(?:"[^"]*"|'[^']*'|\{[^}]*\})/g;
const OUTER_MARGIN = /(?:^|[\s"'`])-?m[tbxylr]?-(?:\d|\[|px|auto)/;

export function cardMargins(files: [path: string, source: string][]): string[] {
  return files
    .filter(([path]) => !/\.stories\.tsx$/.test(path))
    .flatMap(([path, source]) =>
      (source.match(CARD_MARGIN) ?? [])
        .filter((card) => OUTER_MARGIN.test(card.slice(card.indexOf('className='))))
        .map((card) => `${path} gives a card an outer margin: ${card.replace(/\s+/g, ' ')}`));
}

describe('the cards', () => {
  it('catches a card with an outer margin — the check itself, in the shape the settings cards had', () => {
    expect(cardMargins([['./settings/Tools.tsx', '<Card id="settings-tool-locations" className="mt-3.5 scroll-mt-3">']]))
      .toHaveLength(1);
    expect(cardMargins([['./settings/AiJobs.tsx', "<Card className={cn('mt-3.5', warn && 'x')}>"]])).toHaveLength(1);
    expect(cardMargins([['./x.tsx', '<Card className="mb-4">']])).toHaveLength(1);
    // And what must keep passing: a scroll margin, no class, a card accented, a story's own frame.
    expect(cardMargins([['./settings/machine/Update.tsx', '<Card id="settings-update" className="scroll-mt-3">']])).toEqual([]);
    expect(cardMargins([['./x.tsx', '<Card accent warn>']])).toEqual([]);
    expect(cardMargins([['./x.stories.tsx', '<Card className="mt-3.5">']])).toEqual([]);
  });

  it('holds: no card sets an outer margin; the domain\'s container spaces them', () => {
    expect(cardMargins(components)).toEqual([]);
  });
});

/**
 * 🔴 **A form is sized to its content up to the pane**: nothing caps it below the pane.
 * The 40rem and 48rem caps settings' forms wore left half of a wide pane to a form that wanted it, and every one of
 * them passed its stories, which were drawn narrower than the cap. A field's own width (a number's 4.5rem, a name's
 * 16rem) is what it holds, and stays; a cap on a form or a grid of fields goes, and `FormGrid` lays the form out.
 */
const FORM_CAP = /^(?=.*\bmax-w-\[\d+(?:\.\d+)?rem\])(?=.*(?:\bgrid\b|<form\b)).*$/gm;

/** The one form kept at a measure, by component, with its reason. */
const CAPPED_BY_DESIGN: Record<string, string> = {
  // The start prompt is a reading column centred in the main area, not a card's form: its measure is the design's.
  './work/start/NewConversation.tsx': 'NewConversation, the prompt centred in the main area',
};

export function formCaps(files: [path: string, source: string][]): string[] {
  return files
    .filter(([path]) => !/\.(?:stories|test)\.tsx$/.test(path) && !(path in CAPPED_BY_DESIGN))
    .flatMap(([path, source]) =>
      (source.match(FORM_CAP) ?? []).map((line) => `${path} caps a form below the pane: ${line.trim()}`));
}

describe('the forms', () => {
  it('catches a form or a grid of fields capped in rem — the check itself, in the shapes the settings forms had', () => {
    expect(formCaps([['./settings/repository/Commits.tsx', 'className="grid max-w-[40rem] gap-2"']])).toHaveLength(1);
    expect(formCaps([['./settings/BrowserDomain.tsx', '<div className="grid max-w-[48rem] items-end gap-2 md:grid-cols-[minmax(0,1fr)_12rem_auto]">']]))
      .toHaveLength(1);
    expect(formCaps([['./x.tsx', '<form className="max-w-[40rem]">']])).toHaveLength(1);
    // And what must keep passing: a field's own width, a tooltip's, the start prompt's column, a story's frame.
    expect(formCaps([['./settings/repository/Reviews.tsx', 'className={`${FIELD} max-w-[12rem]`}']])).toEqual([]);
    expect(formCaps([['./design/ui.tsx', 'className="pointer-events-none z-30 max-w-[22rem] rounded-card"']])).toEqual([]);
    expect(formCaps([['./work/start/NewConversation.tsx', 'className="mx-auto grid w-full max-w-[48rem]"']])).toEqual([]);
    expect(formCaps([['./x.stories.tsx', '<div className="grid max-w-[48rem]">']])).toEqual([]);
  });

  it('holds: no form is capped below the pane but the one the design keeps at a measure', () => {
    expect(formCaps(components)).toEqual([]);
    for (const path of Object.keys(CAPPED_BY_DESIGN)) {
      expect(sources[path], `${path} is named by the allow-list and is there`).toBeDefined();
      expect(sources[path]!.match(FORM_CAP) ?? [], `${path} still carries the cap the allow-list names, or leaves the list`).not.toEqual([]);
    }
  });
});

/**
 * 🔴 **A region lays out by its own width, never the viewport's**: no `sm:`, `md:`, `lg:`, `max-sm:`, `min-[…]:` or their kin inside a region, which is a container query on
 * the region or the component. The side bar and the list narrow a region without the window changing: a form at 630 px
 * of pane kept its three columns because the window measured 1546. What is placed against the window keeps its viewport
 * variants, by its responsibility, not its file.
 */
const VIEWPORT_VARIANT = /(?<![@\w/-])(?:(?:max-)?(?:sm|md|lg|xl|2xl)|(?:min|max)-\[[^\]]+\]):(?=[\w[-])/g;

/** What is placed against the window, by component, each with its reason; and what a branch in flight holds. */
const WINDOW_PARTS: Record<string, string> = {
  // The strip's toggles: the strip is the window's, drawn under its caption buttons.
  './app/App.tsx': 'the strip (TOGGLES_ROOM)',
  // The status bar's items, placed against the window's bottom edge.
  './work/frame/frame.tsx': 'the status bar (sync, remote, indexed)',
  // The toast viewport beside an open drawer: the corner is the window's.
  './design/atoms/Toasts.tsx': 'the toast viewport beside a drawer (Toasts)',
  // The main area is the container named `main` itself, and a container cannot query itself: its gutters follow the
  // window until the padding moves inside it.
  './work/frame/ViewMain.tsx': "the main area's gutters (ViewMain)",
  // The dock is the frame's part, sized against the window by `layout.ts`.
  './help/AskPanel.tsx': 'the dock (AskPanel, framed)',
};

export function viewportVariants(files: [path: string, source: string][]): string[] {
  return files
    .filter(([path]) => !/\.(?:stories|test)\.tsx$/.test(path) && !(path in WINDOW_PARTS))
    .flatMap(([path, source]) =>
      [...new Set(source.match(VIEWPORT_VARIANT) ?? [])].map((hit) => `${path} lays out by the viewport: ${hit}`));
}

describe('the regions', () => {
  it('catches a viewport variant inside a region — the check itself, in the shapes the forms had', () => {
    expect(viewportVariants([['./settings/BrowserDomain.tsx', 'className="grid gap-2 md:grid-cols-[minmax(0,1fr)_12rem_auto]"']]))
      .toEqual(['./settings/BrowserDomain.tsx lays out by the viewport: md:']);
    expect(viewportVariants([['./help/access/x.tsx', 'className="grid max-sm:grid-cols-1"']])).toHaveLength(1);
    expect(viewportVariants([['./x.tsx', "'flex items-center max-[54rem]:hidden'"]])).toHaveLength(1);
    expect(viewportVariants([['./x.tsx', "className={cn('a', beside && 'min-[56rem]:right-4')}"]])).toHaveLength(1);
    // And what must keep passing: a container query, named or not; a window part; a story.
    expect(viewportVariants([['./x.tsx', 'className="@max-[26rem]:grid-cols-1 @max-md:pt-6 @4xl/main:grid-cols-2 @max-3xl/main:px-3"']])).toEqual([]);
    expect(viewportVariants([['./work/frame/frame.tsx', 'className="hidden sm:flex"']])).toEqual([]);
    expect(viewportVariants([['./x.stories.tsx', 'className="md:flex"']])).toEqual([]);
  });

  it("holds: no region lays out by the viewport but the window's parts, each named with its reason", () => {
    expect(viewportVariants(components)).toEqual([]);
    for (const path of Object.keys(WINDOW_PARTS)) {
      expect(sources[path], `${path} is named by the allow-list and is there`).toBeDefined();
      expect(sources[path]!.match(VIEWPORT_VARIANT) ?? [], `${path} still carries a viewport variant, or leaves the list`).not.toEqual([]);
    }
  });
});

/**
 * **An edge is ink and never a status hue**. A flowchart's edge is content, its cause said in the source's
 * foot or port and to a reader; a hue on it would say a state the record may not hold. A way the driver found a problem in wears a ⚠
 * chip in the danger ink beside its ordinary line, so the line itself is read from what draws it: the canvas's paths and markers and
 * the chip a way wears.
 */
const EDGE_DRAWING = ['./design/FlowCanvas.tsx', './workflow/chart/EdgeChip.tsx'];
const STATUS_HUE_CLASS = new RegExp(String.raw`(?<![\w-])(?:stroke|fill|text|bg|border|ring|outline|decoration)-${HUES}(?:-soft)?(?![\w-])|var\(--${HUES}(?:-soft)?\)`, 'g');

export function edgeHues(files: [path: string, source: string][]): string[] {
  return files.flatMap(([path, source]) => (source.match(STATUS_HUE_CLASS) ?? []).map((hit) => `${path} draws an edge in a status hue: ${hit}`));
}

describe('an edge wears no status hue', () => {
  it('catches a path, a marker and a chip in one, and leaves the inks', () => {
    expect(edgeHues([['./a.tsx', 'className="stroke-ok"'], ['./b.tsx', 'fill="var(--fail)"'], ['./c.tsx', 'className="text-st-open"']])).toHaveLength(3);
    expect(edgeHues([['./a.tsx', 'className="stroke-ink-faint stroke-ink-soft text-fail-ink fill-ink-faint"']])).toEqual([]);
  });

  it('holds: what draws the canvas\'s edges and their chips uses the inks alone', () => {
    const drawing = EDGE_DRAWING.map((path) => [path, sources[path]] as [string, string]);
    for (const [path, source] of drawing) expect(source, `${path} is there`).toBeDefined();
    expect(edgeHues(drawing)).toEqual([]);
  });
});

describe('the form controls', () => {
  it('catches a native select, checkbox or radio, and leaves ui.tsx its own', () => {
    expect(nativeChoices([['./work/Start.tsx', '<select value={x}>']])).toHaveLength(1);
    expect(nativeChoices([['./work/Row.tsx', '<input type="checkbox" checked />']])).toHaveLength(1);
    expect(nativeChoices([['./work/Row.tsx', "<input type='radio' />"]])).toHaveLength(1);
    expect(nativeChoices([['./design/atoms/FormControls.tsx', '<select value={x}>']])).toEqual([]);
    expect(nativeChoices([['./work/Row.tsx', '<SelectField value={x} />']])).toEqual([]);
  });

  it('holds: every choice in the platform is the platform\'s own control', () => {
    expect(nativeChoices(components)).toEqual([]);
  });
});
