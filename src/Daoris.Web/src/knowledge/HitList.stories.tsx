import type { Meta, StoryObj } from '@storybook/react-vite';
import { LIST_BOUNDS, LIST_STRIP, type ListLayout } from '../work/layout';
import { ListPane } from '../work/ListPane';
import { CJK_HIT, HITS } from './fixtures';
import { HitList, type HitsAnswer } from './HitList';
import { KnowledgeModes, KnowledgeStrip } from './KnowledgeModes';

// Search's list (FRAME1f, D118 §2) on Knowledge's pane (UX6i, D150 §2.2), under the place's two-way choice, in every state
// the design names: nothing typed, the hits with one chosen, capped, found by words alone on a deployment that matches
// meaning, the first search on its way, a newer one on its way with the last hits held, no matches by each tier,
// nothing answering, an error with no answer ever, a 中文 hit, everything rather than local only, the strip, and laid
// over the main area.

const open = (width: number): ListLayout => ({ mode: 'open', width, beside: width, auto: false });
const CLOSED: ListLayout = { mode: 'strip', width: LIST_STRIP, beside: LIST_STRIP, auto: false };
const answered = (over: Partial<Extract<HitsAnswer, { state: 'answered' }>> = {}): HitsAnswer => ({
  state: 'answered', hits: HITS, more: false, byMeaning: false, nothing: false, searching: false, ...over,
});

type Args = {
  query: string;
  answer: HitsAnswer;
  localOnly?: boolean;
  semantic?: boolean;
  chosen?: string | null;
  layout?: ListLayout;
};

/** The list as the place hands it to its pane: no ＋, since Search makes nothing, the place's choice, and its body. */
function SearchListPane({ query, answer, localOnly = true, semantic = false, chosen = null, layout = open(LIST_BOUNDS.knowledge.initial) }: Args) {
  return (
    <ListPane
      name="Knowledge"
      labels={{ open: 'Show the result list', close: 'Hide the result list', resize: 'result list width' }}
      layout={layout}
      bounds={LIST_BOUNDS.knowledge}
      head={<KnowledgeModes mode="search" onMode={() => {}} />}
      strip={<KnowledgeStrip mode="search" onMode={() => {}} />}
      onOpen={() => {}}
      onClose={() => {}}
      onDismiss={() => {}}
      onResize={() => {}}
    >
      <HitList
        query={query}
        onQuery={() => {}}
        localOnly={localOnly}
        onLocalOnly={() => {}}
        answer={answer}
        marked={query}
        semantic={semantic}
        chosen={chosen}
        onChoose={() => {}}
        onConverge={() => {}}
      />
    </ListPane>
  );
}

const meta: Meta<typeof SearchListPane> = {
  title: 'Knowledge/HitList',
  component: SearchListPane,
  args: { query: 'chunk hydration', answer: answered(), chosen: HITS[0]!.id },
  // The frame's height, and a main area beside the list for it to sit beside or lie over.
  decorators: [(Story) => (
    <div className="relative flex h-[40rem] w-[60rem] max-w-full border border-line bg-page">
      <Story />
      <div className="min-w-0 flex-1 p-4 text-body text-ink-soft">The main area: the entry a hit names.</div>
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof SearchListPane>;

/** The hits for a search, the first chosen: each its title, its repository and kind, and its excerpt with the words marked. */
export const Hits: Story = {};

/** Nothing typed yet: the box and *local only*, and nothing below them. */
export const NothingTyped: Story = { args: { query: '', answer: { state: 'idle' }, chosen: null } };

/** The service capped the answer: the count says there are more, and to narrow it. */
export const Capped: Story = { args: { answer: answered({ more: true }) } };

/** Found by words alone on a deployment that matches meaning too: said beside the hits. */
export const WordsOnly: Story = { args: { semantic: true, answer: answered({ byMeaning: false }) } };

/** The first search on its way: skeleton rows, with its words on the line the count takes. */
export const FirstSearch: Story = { args: { answer: { state: 'first' }, chosen: null } };

/** A newer search on its way: the last hits held at reduced opacity, the words on the count's line (audit SR11). */
export const SearchingAgain: Story = { args: { query: 'chunk hydration order', answer: answered({ searching: true }) } };

/** No matches by words: the empty state in the lexical tier's words, with the way to Convergence. */
export const NoMatchesLexical: Story = { args: { query: 'zebra quartz', answer: answered({ hits: [] }), chosen: null } };

/** No matches by meaning too: the empty state in the semantic tier's words. */
export const NoMatchesSemantic: Story = {
  args: { query: 'zebra quartz', semantic: true, answer: answered({ hits: [], byMeaning: true }), chosen: null },
};

/** Neither half answered: not an empty answer, and said so. */
export const NothingAnswered: Story = { args: { query: 'zebra quartz', answer: answered({ hits: [], nothing: true }), chosen: null } };

/** A search that failed with no answer ever: its sentence in place, never blank. */
export const ErrorNoAnswer: Story = {
  args: { answer: { state: 'unanswered', sentence: 'Daoris could not reach this machine\'s host. Is the service running?' }, chosen: null },
};

/** A hit in 中文: content, never translated. */
export const ChineseHit: Story = { args: { query: '场景加载', answer: answered({ hits: [CJK_HIT, ...HITS] }), chosen: CJK_HIT.id } };

/** Everything rather than each repository's own: the box unticked, and canonical copies among the hits. */
export const Everything: Story = { args: { localOnly: false } };

/** Closed by the person: its strip holds its controls alone (D118 §2). */
export const Strip: Story = { args: { layout: CLOSED } };

/** A strip the window drew, opened: the list over the main area, beside its strip. */
export const LaidOver: Story = { args: { layout: { mode: 'over', width: LIST_BOUNDS.knowledge.initial, beside: LIST_STRIP, auto: true } } };
