/**
 * **The label track, measured** (UIFIT1; the UI/UX design R1 and §5's first trap): every setting row on the page, as the
 * browser laid it out. jsdom lays nothing out, so a class proves no alignment; this reads each row's label cell and value
 * cell by their boxes, and each label's max-content width (the width its words would take on one line), which is what sets
 * the track and what D185's UIFIT1 note records.
 *
 * Self-contained, with no import and nothing from outside its body, so Playwright can send it to the page as source.
 */
export type RowMeasure = {
  label: string;
  /** The label's line on one line, its ⓘ included, in px: what the track would be with no floor and no cap. */
  content: number;
  /** The label cell's box. */
  cell: { x: number; y: number; w: number; h: number };
  /** The value cell's box, or null for a row with no value. */
  value: { x: number; y: number; w: number; h: number } | null;
};

export type ListMeasure = {
  /** Whether the rows share a list (`SettingList`), or stand alone, each on a track of its own. */
  listed: boolean;
  /** The list's box, or the lone row's. */
  box: { x: number; y: number; w: number; h: number };
  rows: RowMeasure[];
};

export type SettingsMeasure = {
  /** The main area's width the story is drawn at, as `storyWidths` names it (`data-main-width`), and its box's width. */
  frame: string;
  width: number;
  lists: ListMeasure[];
};

export function measureSettingRows(): SettingsMeasure {
  type Box = { x: number; y: number; w: number; h: number };
  const boxOf = (element: Element): Box => {
    const rect = element.getBoundingClientRect();
    return { x: rect.left, y: rect.top, w: rect.width, h: rect.height };
  };
  const contentOf = (span: Element): number => {
    const probe = document.createElement('span');
    probe.style.cssText = 'position:absolute;left:-9999px;top:0;visibility:hidden;white-space:nowrap;';
    probe.style.font = getComputedStyle(span).font;
    probe.textContent = span.textContent;
    document.body.append(probe);
    const width = probe.getBoundingClientRect().width;
    probe.remove();
    return width;
  };
  const rowOf = (row: Element): RowMeasure => {
    const cell = row.querySelector('[data-setting-label]')!;
    const line = cell.firstElementChild!;
    const span = line.firstElementChild ?? line;
    // The line's max-content: the words on one line, then each glyph after them with the line's gap (`gap-1.5`, 6 px).
    const glyphs = [...line.children].slice(1).reduce((width, glyph) => width + 6 + boxOf(glyph).w, 0);
    const value = row.querySelector('[data-setting-value]');
    return { label: span.textContent ?? '', content: contentOf(span) + glyphs, cell: boxOf(cell), value: value ? boxOf(value) : null };
  };

  const main = document.querySelector<HTMLElement>('[data-main-width]');
  const lists: ListMeasure[] = [];
  for (const list of document.querySelectorAll('[data-setting-list]')) {
    lists.push({ listed: true, box: boxOf(list), rows: [...list.querySelectorAll(':scope > [data-setting-row]')].map(rowOf) });
  }
  for (const row of document.querySelectorAll('[data-setting-row]')) {
    if (row.closest('[data-setting-list]')) continue;
    lists.push({ listed: false, box: boxOf(row), rows: [rowOf(row)] });
  }
  return { frame: main?.dataset.mainWidth ?? '', width: main ? boxOf(main).w : 0, lists };
}
