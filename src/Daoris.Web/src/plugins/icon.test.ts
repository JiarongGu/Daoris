import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { fnv1a, IDENT_HUES, iconSource, monogram } from './icon';

/**
 * A plugin's icon on the page (PLUGUI2, D140 §3.2, §3.3): which declared icon is drawn, and the monogram drawn instead.
 * The monogram is **deterministic** (the same plugin wears the same one on every machine and every reload) and its hues
 * are **computed, not eyeballed** (platform language §3), both held here.
 */

describe('the monogram', () => {
  it('hashes by FNV-1a, so a hue is the same on every machine — the published test vectors', () => {
    expect(fnv1a('')).toBe(0x811c9dc5);
    expect(fnv1a('a')).toBe(0xe40c292c);
    expect(fnv1a('foobar')).toBe(0xbf9cf968);
  });

  /** [id, name as declared, glyph, hue]: pinned, so a change to the hash, the hues' order or the glyph's rule is seen. */
  const PINNED: [string, string, string, string][] = [
    ['acme.gate', 'Acme gate', 'A', 'plum'],
    ['acme.agents', 'Acme agents', 'A', 'teal'],
    ['acme.notes', 'Session notes', 'S', 'moss'],
    ['quiet-hours', 'Quiet hours', 'Q', 'stone'],
    ['in-app-browser', 'In-app browser', 'I', 'stone'],
    ['land-github', 'Land on GitHub', 'L', 'slate'],
    ['acme.quiet', '夜间暂停委托', '夜', 'teal'],
    ['future', '', 'F', 'moss'],
    ['browser', '  ', 'B', 'teal'],
    ['acme.lower', 'élan', 'É', 'plum'],
  ];

  it('is the name\'s first character on the id\'s hue, pinned', () => {
    for (const [id, name, glyph, hue] of PINNED) {
      expect(monogram(id, name), `${id} named ${JSON.stringify(name)}`).toEqual({ glyph, hue });
    }
  });

  it('keeps a plugin\'s hue across a rename, and tells two plugins of one name apart by their ids', () => {
    expect(monogram('acme.gate', 'Acme gate').hue).toBe(monogram('acme.gate', 'Quiet gate').hue);
    expect(monogram('acme.gate', 'Acme').hue).not.toBe(monogram('acme.agents', 'Acme').hue);
  });

  it('uses every hue over the ids a machine might hold, so the palette is not one colour in practice', () => {
    const seen = new Set(Array.from({ length: 60 }, (_, at) => monogram(`acme.plugin-${at}`, 'x').hue));
    expect([...seen].sort()).toEqual([...IDENT_HUES].sort());
  });

  it('is the same whatever language the reader chose: the glyph is never cased by a locale', () => {
    // Turkish casing would make `i` a dotted capital; the monogram is the same for every reader.
    expect(monogram('acme.index', 'index').glyph).toBe('I');
  });
});

describe('the declared icon the page draws', () => {
  const PNG = `data:image/png;base64,${btoa('\u0089PNG\r\n\u001a\n')}`;
  const SVG = `data:image/svg+xml;base64,${btoa('<svg xmlns="http://www.w3.org/2000/svg"/>')}`;

  it('draws an SVG\'s or a PNG\'s own bytes', () => {
    expect(iconSource(SVG)).toBe(SVG);
    expect(iconSource(PNG)).toBe(PNG);
  });

  it('draws nothing it would have to reach for, nor any other kind: a path, an address, markup, another type', () => {
    for (const refused of [
      'C:/somewhere/data/plugins/acme.gate/icon.svg',
      '/srv/daoris/data/plugins/acme.gate/icon.png',
      'file:///C:/somewhere/icon.svg',
      'https://example.com/icon.svg',
      '<svg xmlns="http://www.w3.org/2000/svg"/>',
      'data:image/svg+xml,<svg xmlns="http://www.w3.org/2000/svg"/>',
      'data:text/html;base64,PHNjcmlwdD4=',
      'data:image/gif;base64,R0lGODlh',
      'data:image/png;base64,not base64!',
      '',
      null,
      undefined,
      42,
      { src: SVG },
    ]) {
      expect(iconSource(refused), String(refused)).toBeNull();
    }
  });

  it('draws nothing larger than a 32 KiB file could be', () => {
    const most = `data:image/svg+xml;base64,${'A'.repeat(Math.ceil((32 * 1024) / 3) * 4)}`;
    expect(iconSource(most)).toBe(most);
    expect(iconSource(`${most}AAAA`)).toBeNull();
  });
});

// ——— The identity hues, computed from tokens.css (platform language §3: a palette is computed, not tasted).

const toLinear = (c: number) => (c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4);
const rgb = (hex: string) => [1, 3, 5].map((at) => parseInt(hex.slice(at, at + 2), 16) / 255);
const luminance = (color: number[]) => {
  const [r, g, b] = color.map(toLinear) as [number, number, number];
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
};
const contrast = (a: number[], b: number[]) => {
  const [light, dark] = [luminance(a), luminance(b)].sort((x, y) => y - x) as [number, number];
  return (light + 0.05) / (dark + 0.05);
};
/** A colour at a share over a surface, composited as the page composites a translucent field. */
const over = (color: number[], surface: number[], share: number) => color.map((c, at) => c * share + surface[at]! * (1 - share));
const lab = (color: number[]) => {
  const [r, g, b] = color.map(toLinear) as [number, number, number];
  const f = (t: number) => (t > 216 / 24389 ? Math.cbrt(t) : ((24389 / 27) * t + 16) / 116);
  const x = f((0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047);
  const y = f(0.2126 * r + 0.7152 * g + 0.0722 * b);
  const z = f((0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883);
  return [116 * y - 16, 500 * (x - y), 200 * (y - z)];
};
const deltaE = (a: number[], b: number[]) => {
  const [p, q] = [lab(a), lab(b)];
  return Math.hypot(p[0]! - q[0]!, p[1]! - q[1]!, p[2]! - q[2]!);
};

// From disk, not `?raw`, as tokens.test.ts reads it: the CSS pipeline answers a raw stylesheet with an empty string.
const css = readFileSync(join(process.cwd(), 'src', 'tokens.css'), 'utf8');
const block = (pattern: RegExp) => Object.fromEntries(
  [...(pattern.exec(css)?.[1] ?? '').matchAll(/(--[a-z-]+)\s*:\s*(#[0-9a-fA-F]{6})\s*;/g)].map((m) => [m[1]!, m[2]!.toLowerCase()]),
);
const THEMES = {
  light: block(/^:root\s*\{([^}]*)\}/m),
  dark: block(/@media \(prefers-color-scheme: dark\)\s*\{\s*:root\s*\{([^}]*)\}/),
};

/** The field a monogram's glyph sits on: its hue at this share (`PluginIcon`'s `/15`). */
const FIELD = 0.15;

describe('the identity hues', () => {
  it('are declared for each hue in both themes, and read', () => {
    for (const [theme, tokens] of Object.entries(THEMES)) {
      for (const hue of IDENT_HUES) expect(tokens[`--ident-${hue}`], `${theme} --ident-${hue}`).toMatch(/^#[0-9a-f]{6}$/);
    }
  });

  it('give every glyph at least 4.5:1 on its field, over the page and a raised surface, in both themes', () => {
    for (const [theme, tokens] of Object.entries(THEMES)) {
      for (const hue of IDENT_HUES) {
        const ink = rgb(tokens[`--ident-${hue}`]!);
        for (const surface of ['--page', '--raised']) {
          const ratio = contrast(ink, over(ink, rgb(tokens[surface]!), FIELD));
          expect(ratio, `${theme} ${hue} on ${surface}: ${ratio.toFixed(2)}`).toBeGreaterThanOrEqual(4.5);
        }
      }
    }
  });

  it('stand at least ΔE 20 from every status hue, its ink and the accent, so a monogram never reads as a state', () => {
    // A state's word wears its hue's ink (UXFIX5c), and a monogram is a glyph beside such words, so the inks count too.
    const states = ['--st-open', '--st-taken', '--st-done', '--st-declined', '--ink-open', '--ink-taken', '--ink-done', '--ink-danger'];
    for (const [theme, tokens] of Object.entries(THEMES)) {
      for (const hue of IDENT_HUES) {
        for (const status of [...states, '--accent']) {
          const apart = deltaE(rgb(tokens[`--ident-${hue}`]!), rgb(tokens[status]!));
          expect(apart, `${theme} ${hue} against ${status}: ${apart.toFixed(1)}`).toBeGreaterThanOrEqual(20);
        }
      }
    }
  });

  it('catches a hue too near a state — the check itself, with the done green as a hue', () => {
    const ink = rgb(THEMES.light['--st-done']!);
    expect(deltaE(ink, rgb(THEMES.light['--st-done']!))).toBeLessThan(20);
    expect(contrast(rgb('#c8c4bb'), over(rgb('#c8c4bb'), rgb(THEMES.light['--page']!), FIELD))).toBeLessThan(4.5);
  });
});
