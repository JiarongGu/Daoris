import { useState } from 'react';
import { cn } from '../lib/cn';
import { iconSource, type IdentHue, monogram } from './icon';

/** Each size the icon is drawn at (D140 §2): the list's strip, a row of the catalogue, a page's header. */
const SIZE = {
  strip: 'size-5 rounded-control text-meta',
  row: 'size-8 rounded-control text-title',
  page: 'size-12 rounded-card text-view',
} as const;

/** A monogram's hue: its glyph in the hue, on a 15% field of it (the field `icon.test.ts` computes the contrast on). */
const HUE: Record<IdentHue, string> = {
  moss: 'bg-ident-moss/15 text-ident-moss',
  teal: 'bg-ident-teal/15 text-ident-teal',
  slate: 'bg-ident-slate/15 text-ident-slate',
  iris: 'bg-ident-iris/15 text-ident-iris',
  plum: 'bg-ident-plum/15 text-ident-plum',
  stone: 'bg-ident-stone/15 text-ident-stone',
};

/**
 * **A plugin's icon** (PLUGUI2, D140 §3): its own, declared in its manifest and handed as its bytes, or its monogram.
 *
 * @remarks
 * **A molecule**: everything arrives as props. **Decoration**, hidden from a reader, since the plugin's name is always
 * beside it.
 *
 * - **Its own icon is drawn as an image**, never inlined as markup: in an `<img>` none of an SVG's scripts run, nothing
 *   outside it loads and none of its styles reach the page (D64 §7). Only an SVG's or a PNG's bytes are drawn
 *   (`iconSource`); a path or an address is no icon. One that will not load gives way to the monogram.
 * - **The monogram** is the name's first character on the id's hue (`monogram`): the same on every machine and reload.
 *   It is identity and never status: how a plugin stands stays on its pill, with its word.
 */
export function PluginIcon({ id, name, icon, size = 'row', dimmed = false }: {
  id: string;
  /** Its name as declared, content: the monogram's glyph is its first character. */
  name: string;
  /** Its declared icon as the driver handed it: a data URI of its bytes, or nothing. */
  icon?: string | null;
  size?: keyof typeof SIZE;
  /** Drawn faint: a plugin the person switched off. */
  dimmed?: boolean;
}) {
  const source = iconSource(icon);
  // The source that would not load, so a newer icon (an update) is tried afresh.
  const [failed, setFailed] = useState<string | null>(null);

  if (source && source !== failed) {
    return (
      <img
        src={source}
        alt=""
        aria-hidden="true"
        draggable={false}
        onError={() => setFailed(source)}
        className={cn(SIZE[size], 'shrink-0 object-contain', dimmed && 'opacity-55')}
      />
    );
  }

  const { glyph, hue } = monogram(id, name);
  return (
    <span
      aria-hidden="true"
      data-hue={hue}
      className={cn(
        SIZE[size], HUE[hue],
        'inline-flex shrink-0 select-none items-center justify-center font-semibold leading-none',
        dimmed && 'opacity-55',
      )}
    >
      {glyph}
    </span>
  );
}
