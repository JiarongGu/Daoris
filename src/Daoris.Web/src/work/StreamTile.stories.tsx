import type { Meta, StoryObj } from '@storybook/react-vite';
import type { Quest, Session } from '../api';
import { MonoWell } from '../ui';
import { StreamTile } from './StreamTile';

// The monitor's unit, in every state it has (SURF8). A tile is a molecule, so each of these is
// reached by passing props — no driver, no service, no arranged world (components plan §2).

/** Relative to now, so a story reads as a duration rather than as the age of this file. */
const at = (minutesAgo: number) => new Date(Date.now() - minutesAgo * 60_000).toISOString();

const SESSION: Session = {
  id: 's1a2b3c4',
  quest: '7a82cc',
  repository: 'engine',
  adapter: 'claude-code',
  state: 'working',
  kind: 'driven',
  created: at(134),
  updated: at(4),
};

const QUEST: Quest = {
  id: '7a82cc',
  from: 'platform',
  to: 'engine',
  title: 'Expose a streaming budget on the chunk API',
  body: 'World streaming needs to cap hydration work per frame.',
  status: 'Taken',
  filed: at(10_000),
  updated: at(240),
};

const LOG = [
  'running 41 tests in src/streaming',
  '  chunk budget respected under load ......... ok',
  '  hydration yields at the frame boundary .... ok',
  'writing src/streaming/budget.ts',
].join('\n');

function Stream({ text, live = true }: { text: string; live?: boolean }) {
  return <MonoWell text={text} live={live} dropped={0} fill />;
}

const meta: Meta<typeof StreamTile> = {
  title: 'Work/StreamTile',
  component: StreamTile,
  args: { session: SESSION, quest: QUEST, children: <Stream text={LOG} /> },
  // The monitor's own column width and a bounded height — a tile never grows to fit its stream.
  decorators: [(Story) => <div className="flex h-[16rem] w-[34rem] flex-col"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof StreamTile>;

export const Working: Story = {};

/** The one priority rule: what needs a person outranks what is busy (components plan §3a). */
export const AwaitingPerson: Story = {
  args: {
    session: { ...SESSION, state: 'awaiting-person', updated: at(11) },
    children: <Stream text={`${LOG}\n\nI can take either route here — which do you want?`} />,
  },
};

/** A conversation has no quest, so its identity is derived rather than invented (design §3). */
export const Conversation: Story = {
  args: {
    session: { ...SESSION, kind: 'chat', quest: undefined, repository: 'platform' },
    quest: null,
    children: <Stream text="> why does the budget cap at 4ms?" />,
  },
};

/** Ended: the span is a lifetime rather than an age, and the stream stopped. */
export const Ended: Story = {
  args: {
    session: { ...SESSION, state: 'completed', updated: at(2) },
    children: <Stream text={`${LOG}\n\ndone.`} live={false} />,
  },
};

/** Nothing said yet — a session that has started and not spoken is not a broken tile. */
export const Silent: Story = {
  args: { session: { ...SESSION, state: 'starting', created: at(0) }, children: <Stream text="" /> },
};

/**
 * Somebody else's machine. The record travelled here and the console did not (D47 §4), so the tile
 * says where it runs rather than showing an empty well that would read as silence.
 */
export const Elsewhere: Story = {
  args: { session: { ...SESSION, id: 'laptop/s1a2b3c4' } },
};

/** A long title and 中文, at the tile's real width — the two ways a header stops fitting. */
export const LongAndChinese: Story = {
  args: {
    quest: {
      ...QUEST,
      title: '把区块 API 的流式预算暴露出来，并且在每一帧的边界上让出主线程，不要一次性水合整张地图',
    },
    session: { ...SESSION, repository: 'world-streaming-prototype' },
  },
};

/** With a shell to open one, a tile offers the session a window of its own. */
export const Detachable: Story = {
  args: { onDetach: () => {} },
};
