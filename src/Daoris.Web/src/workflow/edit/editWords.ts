import type { TFunction, i18n as I18n } from 'i18next';
import { authoredText } from '../../locales/authored';
import { kindTitle, outcomeSaid } from '../graphFlow';
import type { GraphProblem, WorkflowPaletteAnswer } from './answers';
import { askedInputs, type Carried, type DraftNode, type DraftWire, type GraphDraft, type WayRowKey } from './graphDraft';

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

/** The kind of the step `id` in a draft, for the words of the outcomes it declares. */
export const kindIn = (draft: GraphDraft, id: string): string | undefined => draft.nodes.find((node) => node.id === id)?.kind;

/**
 * A row of *What happens next* named: *Once done*, *If you ask for changes*, *If it fails*, or the outcome as its step's kind words it
 * (`kind`, the step's).
 */
export function rowSaid(t: TFunction, row: WayRowKey, kind?: string): string {
  if (row === 'done' || row === 'changes' || row === 'fails') return t(`workflow.edit.row.${row}`);
  return outcomeSaid(t, kind, row.slice('outcome:'.length));
}

/** What a row comes to with no way on: no further step, the run waiting on the person, or the run stopped. */
export const rowNone = (t: TFunction, row: WayRowKey): string =>
  t(row === 'changes' ? 'workflow.edit.target.waits' : row === 'fails' ? 'workflow.edit.target.stops' : 'workflow.graph.noFurther');

/** A row's target named: a later step by its title, an earlier one as *Back to* it. */
export const targetSaid = (t: TFunction, kind: string, title: string): string => (kind === 'back' ? t('workflow.edit.next.back', { step: title }) : title);

/** How a wire goes on, said without its target: the drawing's edge cause. `from` is the kind of the step it leaves. */
export function howSaid(t: TFunction, wire: Pick<DraftWire, 'kind' | 'outcome' | 'on'>, from?: string): string {
  switch (wire.kind) {
    case 'when': return t('workflow.edit.on.outcome', { outcome: outcomeSaid(t, from, wire.outcome ?? '') });
    case 'back': return wire.on === 'rejected' ? t('workflow.edit.on.rejected') : wire.on === 'failed' ? t('workflow.edit.on.failed')
      : t('workflow.edit.on.outcome', { outcome: outcomeSaid(t, from, wire.on ?? '') });
    case 'after': case 'trigger': case 'data': case 'failed': case 'rejected': return t(`workflow.edit.how.${wire.kind}`);
    default: return t('workflow.edit.how.recorded', { kind: wire.kind });
  }
}

/** A way on, said with its target, as *More ways* lists one: *The landing, each time it is reached*. `from` is the kind of the step it leaves. */
export function waySaid(t: TFunction, wire: DraftWire, to: string, from?: string): string {
  switch (wire.kind) {
    case 'after': case 'trigger': case 'failed': case 'rejected': return t(`workflow.edit.way.${wire.kind}`, { to });
    case 'when': return t('workflow.edit.way.when', { to, outcome: outcomeSaid(t, from, wire.outcome ?? '') });
    case 'data': return t('workflow.edit.way.data', { to, from: wire.fromPort ?? '', input: wire.toPort ?? '' });
    case 'back': return wire.on === 'rejected' || wire.on === 'failed'
      ? t(`workflow.edit.way.back.${wire.on}`, { to }) : t('workflow.edit.way.back.outcome', { to, outcome: outcomeSaid(t, from, wire.on ?? '') });
    default: return t('workflow.edit.way.recorded', { to, kind: wire.kind });
  }
}

/** A refusal in the reader's words, or the driver's where this page has none for its code (a rule newer than the page). */
export const refusalSaid = (t: TFunction, i18n: I18n, problem: GraphProblem) =>
  (i18n.exists(`workflow.edit.refusal.${problem.code}`) ? t(`workflow.edit.refusal.${problem.code}`) : problem.words);

/** An option in the page's words (`workflow.edit.option.‹kind›.‹name›`), its raw name where the page has none. */
export const optionLabel = (t: TFunction, kind: string, name: string): string =>
  t(`workflow.edit.option.${kind}.${name}`, { defaultValue: name });

/** One of an option's choices in the page's words, its raw value where the page has none. */
export const choiceLabel = (t: TFunction, kind: string, name: string, choice: string): string =>
  t(`workflow.edit.option.${kind}.${name}.${choice}`, { defaultValue: choice });

/** What a branch pattern is tried against in the settings (`LandingKey.Sample`): a quest like the one in the preview. */
const SAMPLE: Record<string, string> = { quest: '0fda18', session: 's1a2b3c4', ticket: 'ABC-123', slug: 'sample-work', repository: 'engine' };

/** A branch pattern expanded over a sample quest, and any placeholder it says that a pattern cannot (`LandingKey.Placeholders`). */
export function previewPattern(pattern: string): { name: string; unknown: string[] } {
  const unknown: string[] = [];
  const name = pattern.replace(/\{([^{}]*)\}/g, (used, word: string) => {
    if (word in SAMPLE) return SAMPLE[word]!;
    unknown.push(used);
    return used;
  });
  return { name, unknown };
}

/**
 * What a change that made ways in made of a step's inputs, said once: each input proposed from the step before, and each input two steps
 * could feed, which its *Carries* asks about. `steps` are the steps whose inputs the change reached.
 */
export function carriedSaid(t: TFunction, i18n: I18n, palette: WorkflowPaletteAnswer | null, draft: GraphDraft, carried: Carried[], steps: string[]): string {
  const title = (id: string) => {
    const node = draft.nodes.find((each) => each.id === id);
    return node ? stepTitle(t, i18n, palette, node) : id;
  };
  const proposed = carried.map((each) => t('workflow.edit.said.proposed', { step: title(each.step), input: each.input, from: title(each.from), output: each.port }));
  const asked = [...new Set(steps)].flatMap((id) => askedInputs(draft, id).map((each) => t('workflow.edit.said.asks', { step: title(id), input: each.input })));
  return [...proposed, ...asked].join(' ');
}
