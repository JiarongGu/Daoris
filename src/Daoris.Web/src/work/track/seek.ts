import { rowMark } from './passage';

/** Brings a step's first row in the timeline into view: a press on its chip in the track. A step with no row yet brings nothing. */
export function toStepRows(run: string, node: string): void {
  const mark = rowMark(run, node);
  const row = [...document.querySelectorAll<HTMLElement>('[data-step-row]')].find((each) => each.dataset.stepRow === mark);
  row?.scrollIntoView?.({ block: 'center' });
}

/** Brings the dock's line for `key` into view and its first press to focus: a failed step's row, or a request waiting on the person, leads to its press there. */
export function toDockLine(key: string): void {
  const line = [...document.querySelectorAll<HTMLElement>('[data-dock-line]')].find((each) => each.dataset.dockLine === key);
  line?.scrollIntoView?.({ block: 'nearest' });
  line?.querySelector<HTMLElement>('button')?.focus();
}
