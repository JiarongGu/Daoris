import { rowMark } from './passage';

/** Brings a step's first row in the timeline into view: a press on its chip in the track. A step with no row yet brings nothing. */
export function toStepRows(run: string, node: string): void {
  const mark = rowMark(run, node);
  const row = [...document.querySelectorAll<HTMLElement>('[data-step-row]')].find((each) => each.dataset.stepRow === mark);
  row?.scrollIntoView?.({ block: 'center' });
}

/** Brings the dock's card for `key` (its `data-wait`) into view and its first press to focus: a failed step or a waiting request leads there. */
export function toDockLine(key: string): void {
  const line = [...document.querySelectorAll<HTMLElement>('[data-region="dock"] [data-wait]')].find((each) => each.dataset.wait === key);
  line?.scrollIntoView?.({ block: 'nearest' });
  line?.querySelector<HTMLElement>('button')?.focus();
}
