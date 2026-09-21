import type { Meta, StoryObj } from '@storybook/react-vite';
import { type Attention, AttentionRow } from './AttentionRow';
import { AwaitingPerson } from './AwaitingPerson';

// The two surfaces of attention (design §4): the row Overview's band is made of, and the parked
// session's own answer. What is NOT here is a "resume" button — the ledger allows that move and a
// person makes it by answering, which the surface says rather than offering twice.

const at = (minutesAgo: number) => new Date(Date.now() - minutesAgo * 60_000).toISOString();

const PARKED: Attention = {
  id: 's1a2b3c4',
  kind: 'parked',
  title: 'Expose a streaming budget on the chunk API',
  repository: 'engine',
  since: at(38),
  detail: 'Two ways forward; I recommend capping on the chunk API, which is what the quest asks for.',
};

const meta: Meta = { title: 'Work/Attention' };
export default meta;

/** The band's rows: both kinds, a long title, and one that said nothing. */
export const Rows: StoryObj = {
  render: () => (
    <ul className="m-0 max-w-2xl list-none border border-line bg-raised p-0">
      <AttentionRow item={PARKED} />
      <AttentionRow
        item={{
          id: '7a82cc',
          kind: 'unanswerable',
          title: 'Expose a streaming budget on the chunk API',
          repository: 'retired',
          since: at(19_000),
          detail: '`retired` is not on this deployment\'s register, so no agent will ever pull this quest.',
        }}
      />
      <AttentionRow item={{ ...PARKED, id: 'quiet', detail: null, since: at(4) }} />
      <AttentionRow
        item={{
          ...PARKED,
          id: 'cjk',
          title: '让世界流式加载在每一帧内限制水合工作量，并把预算暴露在区块 API 上，供上层调度器读取',
          repository: '世界流式加载引擎',
          detail: '有两条路可走；我建议在区块 API 上限流，这正是委托所要求的。',
        }}
      />
    </ul>
  ),
};

const ANALYSIS = 'Two ways forward.\n\n1. Cap hydration in the scheduler — smaller change, but the budget then lives away from the API that spends it.\n2. Cap it on the chunk API itself — touches more call sites, and the budget ends up where the work is.\n\nI recommend the second: the quest asks for the budget on the chunk API, and option 1 would leave that promise half kept.';

/** The parked session's own surface: the analysis, then exactly three moves. */
export const Parked: StoryObj = {
  render: () => (
    <div className="max-w-3xl">
      <AwaitingPerson note={ANALYSIS} onResolve={() => {}} />
    </div>
  ),
};

/** A park that said nothing. Rarer, and still only a person can clear it. */
export const ParkedWithNothingSaid: StoryObj = {
  render: () => (
    <div className="max-w-3xl">
      <AwaitingPerson note={null} onResolve={() => {}} />
    </div>
  ),
};

/** A move in flight: every one of them is held, so nobody resolves the same session twice. */
export const Resolving: StoryObj = {
  render: () => (
    <div className="max-w-3xl">
      <AwaitingPerson note={ANALYSIS} pending onResolve={() => {}} />
    </div>
  ),
};
