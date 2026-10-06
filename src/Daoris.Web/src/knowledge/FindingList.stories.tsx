import type { Meta, StoryObj } from '@storybook/react-vite';
import { LIST_BOUNDS, LIST_STRIP, type ListLayout } from '../work/layout';
import { ListPane } from '../work/ListPane';
import { CJK_FINDING, CONVERGENT, FINDINGS } from './fixtures';
import { FindingList, type FindingsAnswer } from './FindingList';
import { KnowledgeModes, KnowledgeStrip } from './KnowledgeModes';
import { findingId } from './records';

// Convergence's list (FRAME1f, D118 §2) on Knowledge's pane (UX6i, D150 §2.2), under the place's two-way choice, in every
// state the design names: the findings with one chosen, each kind of likeness, capped, the first comparison on its way, a
// moved similarity on its way with the last findings held, nothing converging, at the floor, an error with no answer
// ever, the semantic tier's note, a 中文 finding, the strip, and laid over the main area.

const open = (width: number): ListLayout => ({ mode: 'open', width, beside: width, auto: false });
const CLOSED: ListLayout = { mode: 'strip', width: LIST_STRIP, beside: LIST_STRIP, auto: false };
const answered = (over: Partial<Extract<FindingsAnswer, { state: 'answered' }>> = {}): FindingsAnswer => ({
  state: 'answered', findings: FINDINGS, at: 0.75, more: false, comparing: false, ...over,
});

type Args = {
  threshold?: number;
  answer: FindingsAnswer;
  semantic?: boolean;
  chosen?: string | null;
  layout?: ListLayout;
};

/** The list as the place hands it to its pane: no ＋, since Convergence makes nothing, the place's choice, and its body. */
function ConvergenceListPane({ threshold = 0.75, answer, semantic = false, chosen = null, layout = open(LIST_BOUNDS.knowledge.initial) }: Args) {
  return (
    <ListPane
      name="Knowledge"
      labels={{ open: 'Show the finding list', close: 'Hide the finding list', resize: 'finding list width' }}
      layout={layout}
      bounds={LIST_BOUNDS.knowledge}
      head={<KnowledgeModes mode="convergence" onMode={() => {}} />}
      strip={<KnowledgeStrip mode="convergence" onMode={() => {}} />}
      onOpen={() => {}}
      onClose={() => {}}
      onDismiss={() => {}}
      onResize={() => {}}
    >
      <FindingList threshold={threshold} onThreshold={() => {}} semantic={semantic} answer={answer} chosen={chosen} onChoose={() => {}} />
    </ListPane>
  );
}

const meta: Meta<typeof ConvergenceListPane> = {
  title: 'Knowledge/FindingList',
  component: ConvergenceListPane,
  args: { answer: answered(), chosen: findingId(CONVERGENT) },
  // The frame's height, and a main area beside the list for it to sit beside or lie over.
  decorators: [(Story) => (
    <div className="relative flex h-[40rem] w-[60rem] max-w-full border border-line bg-page">
      <Story />
      <div className="min-w-0 flex-1 p-4 text-body text-ink-soft">The main area: the finding chosen.</div>
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof ConvergenceListPane>;

/**
 * The findings, the first chosen: each its entries' titles, its kind of likeness and how alike, and the repositories
 * that reached it. A likeness in different words wears the accent; then a pasted copy in three places, then a drifted one.
 */
export const Findings: Story = {};

/** Nothing chosen: the list as a relaunch with no finding remembered opens it. */
export const NothingChosen: Story = { args: { chosen: null } };

/** The service capped the answer: the count says there are more, and to raise the similarity. */
export const Capped: Story = { args: { answer: answered({ more: true }) } };

/** The first comparison on its way: skeleton rows, with its words on the line the count takes. */
export const Comparing: Story = { args: { answer: { state: 'first' }, chosen: null } };

/** The similarity moved: the last findings held at reduced opacity until the new ones land. */
export const ComparingAgain: Story = { args: { threshold: 0.68, answer: answered({ comparing: true }) } };

/** Nothing converges: the empty state, whose act lowers the similarity by a tenth. */
export const NothingConverges: Story = { args: { answer: answered({ findings: [] }), chosen: null } };

/** At the floor: nothing lower is offered, and the empty state says it is the floor (UX5 U42). */
export const AtTheFloor: Story = { args: { threshold: 0.5, answer: answered({ findings: [], at: 0.5 }), chosen: null } };

/** A comparison that failed with no answer ever: its sentence in place, never blank. */
export const ErrorNoAnswer: Story = {
  args: { answer: { state: 'unanswered', sentence: 'Daoris could not reach this machine\'s host. Is the service running?' }, chosen: null },
};

/** On a deployment that matches meaning: the note under the similarity is the sweeping advice, not the limit. */
export const Semantic: Story = { args: { semantic: true } };

/** A finding with a 中文 entry: content, never translated. */
export const ChineseFinding: Story = { args: { answer: answered({ findings: [CJK_FINDING, ...FINDINGS] }), chosen: findingId(CJK_FINDING) } };

/** Closed by the person: its strip holds its controls alone (D118 §2). */
export const Strip: Story = { args: { layout: CLOSED } };

/** A strip the window drew, opened: the list over the main area, beside its strip. */
export const LaidOver: Story = { args: { layout: { mode: 'over', width: LIST_BOUNDS.knowledge.initial, beside: LIST_STRIP, auto: true } } };
