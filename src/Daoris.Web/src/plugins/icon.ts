// A plugin's icon on the page (PLUGUI2, D140; the catalogue design §3.2 and §3.3): which declared icon the page may
// draw, and the monogram it draws instead. Pure, so every plugin a machine could hold is an argument.

/**
 * The six identity hues a monogram wears, `--ident-<hue>` in `tokens.css`, each with a light and a dark value. **Never a
 * status**: they say which plugin, while how it stands stays on its pill (platform language §3). Their order is the
 * hash's, so it is part of the contract: a hue moved here moves every plugin's monogram.
 */
export const IDENT_HUES = ['moss', 'teal', 'slate', 'iris', 'plum', 'stone'] as const;
export type IdentHue = (typeof IDENT_HUES)[number];

/** The 32-bit FNV-1a hash of a text's UTF-16 code units: an id is ASCII (D64 §3), so its bytes. */
export function fnv1a(text: string): number {
  let hash = 0x811c9dc5;
  for (let at = 0; at < text.length; at += 1) {
    hash ^= text.charCodeAt(at);
    hash = Math.imul(hash, 0x01000193) >>> 0;
  }
  return hash >>> 0;
}

/**
 * A plugin's monogram (D140 §3.3): its glyph, the first character of its name as declared (its id's where the name is
 * blank), in capitals where the script has them; and its hue, by its id's hash. **The hue is the id's**, since an id is
 * the folder's and unique while a name may repeat and may change with an update: a plugin keeps its hue across a rename,
 * and an offer and the plugin it becomes look alike. Locale-free, so every reader sees the same.
 */
export function monogram(id: string, name: string): { glyph: string; hue: IdentHue } {
  const first = Array.from(name.trim() || id)[0] ?? '?';
  return { glyph: Array.from(first.toUpperCase())[0] ?? first, hue: IDENT_HUES[fnv1a(id) % IDENT_HUES.length]! };
}

/** The longest URI the page draws: a 32 KiB file in base64 behind the longer prefix (the driver's `PluginIcon.MaxBytes`). */
const LONGEST = 'data:image/svg+xml;base64,'.length + Math.ceil((32 * 1024) / 3) * 4;

const DRAWN = /^data:image\/(?:svg\+xml|png);base64,[A-Za-z0-9+/]+={0,2}$/;

/**
 * The declared icon the page may draw: **only an SVG's or a PNG's own bytes**, as a data URI the driver built from the
 * file it judged (D140 §3.2). Anything else is no icon, and the monogram is drawn: a path or an address would have the
 * page reach for something, and the page is handed bytes, never a place.
 */
export function iconSource(icon: unknown): string | null {
  return typeof icon === 'string' && icon.length <= LONGEST && DRAWN.test(icon) ? icon : null;
}
