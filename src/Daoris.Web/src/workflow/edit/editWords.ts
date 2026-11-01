import type { TFunction, i18n as I18n } from 'i18next';
import { authoredText } from '../../locales/authored';
import { kindTitle } from '../graphFlow';
import type { GraphProblem, WorkflowPaletteAnswer } from './answers';
import type { DraftNode, DraftWire, WayRowKey } from './graphDraft';

// The editor's words (FLOWCHART2b): a step's title, a row of *What happens next*, a way on with its target, and a refusal, each from the
// catalogues, with a plugin's step titled by its manifest's authored words where it declares them (D222). Pure.

/** Words with a code's backticks taken out: a name, or an option's label. */
export const plain = (text: string) => text.replace(/`/g, '');

/** A step's title: a plugin's step by the title its manifest gives it, in the viewer's language; else its kind in the page's words. */
export function stepTitle(t: TFunction, i18n: I18n, palette: WorkflowPaletteAnswer | null, node: Pick<DraftNode, 'kind'>): string {
  const declared = palette?.plugins.flatMap((plugin) => plugin.steps).find((step) => step.kind === node.kind);
  const authored = authoredText(declared?.title ?? null, i18n.language);
  return authored || plain(kindTitle(t, i18n, node.kind));
}

/** What a plugin's step says it does, as its manifest authored it, or none. */
export function stepAbout(i18n: I18n, palette: WorkflowPaletteAnswer | null, kind: string): string {
  const declared = palette?.plugins.flatMap((plugin) => plugin.steps).find((step) => step.kind === kind);
  return authoredText(declared?.about ?? null, i18n.language);
}

/** A row of *What happens next* named: *Once done*, *If you ask for changes*, *If it fails*, or the outcome as its step declares it. */
export function rowSaid(t: TFunction, row: WayRowKey): string {
  if (row === 'done' || row === 'changes' || row === 'fails') return t(`workflow.edit.row.${row}`);
  return row.slice('outcome:'.length);
}

/** What a row comes to with no way on: no further step, the run waiting on the person, or the run stopped. */
export const rowNone = (t: TFunction, row: WayRowKey): string =>
  t(row === 'changes' ? 'workflow.edit.target.waits' : row === 'fails' ? 'workflow.edit.target.stops' : 'workflow.graph.noFurther');

/** A row's target named: a later step by its title, an earlier one as *Back to* it. */
export const targetSaid = (t: TFunction, kind: string, title: string): string => (kind === 'back' ? t('workflow.edit.next.back', { step: title }) : title);

/** How a wire goes on, said without its target: the drawing's edge cause. */
export function howSaid(t: TFunction, wire: Pick<DraftWire, 'kind' | 'outcome' | 'on'>): string {
  switch (wire.kind) {
    case 'when': return t('workflow.edit.on.outcome', { outcome: wire.outcome ?? '' });
    case 'back': return wire.on === 'rejected' ? t('workflow.edit.on.rejected') : wire.on === 'failed' ? t('workflow.edit.on.failed')
      : t('workflow.edit.on.outcome', { outcome: wire.on ?? '' });
    case 'after': case 'trigger': case 'data': case 'failed': case 'rejected': return t(`workflow.edit.how.${wire.kind}`);
    default: return t('workflow.edit.how.recorded', { kind: wire.kind });
  }
}

/** A way on, said with its target, as *More ways* lists one: *The landing, each time it is reached*. */
export function waySaid(t: TFunction, wire: DraftWire, to: string): string {
  switch (wire.kind) {
    case 'after': case 'trigger': case 'failed': case 'rejected': return t(`workflow.edit.way.${wire.kind}`, { to });
    case 'when': return t('workflow.edit.way.when', { to, outcome: wire.outcome ?? '' });
    case 'data': return t('workflow.edit.way.data', { to, from: wire.fromPort ?? '', input: wire.toPort ?? '' });
    case 'back': return wire.on === 'rejected' || wire.on === 'failed'
      ? t(`workflow.edit.way.back.${wire.on}`, { to }) : t('workflow.edit.way.back.outcome', { to, outcome: wire.on ?? '' });
    default: return t('workflow.edit.way.recorded', { to, kind: wire.kind });
  }
}

/** A refusal in the reader's words, or the driver's where this page has none for its code (a rule newer than the page). */
export const refusalSaid = (t: TFunction, i18n: I18n, problem: GraphProblem) =>
  (i18n.exists(`workflow.edit.refusal.${problem.code}`) ? t(`workflow.edit.refusal.${problem.code}`) : problem.words);
