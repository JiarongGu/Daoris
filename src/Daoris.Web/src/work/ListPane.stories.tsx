import type { Meta, StoryObj } from '@storybook/react-vite';
import { StripMark } from '../ui';
import { LIST_BOUNDS, LIST_STRIP, type ListLayout } from './layout';
import { LIST_ROW } from './listKeys';
import { ListMore, ListPane } from './ListPane';

// A view's list pane (D118 §3a, §5), in every state its props reach, beside a stand-in main area so a
// list laid over it reads as it does on the window.

const open = (width: number): ListLayout => ({ mode: 'open', width, beside: width, auto: false });
const CLOSED: ListLayout = { mode: 'strip', width: LIST_STRIP, beside: LIST_STRIP, auto: false };
const DRAWN: ListLayout = { mode: 'strip', width: LIST_STRIP, beside: LIST_STRIP, auto: true };

const ROWS = ['Cap the hydration per frame', 'Expose a streaming budget', 'Read the budget from the level file'];

function Rows({ names = ROWS }: { names?: string[] }) {
  return (
    <ul className="m-0 list-none p-0">
      {names.map((name, index) => (
        <li key={name} {...{ [LIST_ROW]: '' }}>
          <button
            type="button"
            aria-current={index === 0 || undefined}
            className="block w-full truncate border-l-[3px] border-l-transparent px-2.5 py-1.5 text-left text-body text-ink aria-[current]:border-l-accent aria-[current]:bg-accent-soft"
          >
            {name}
          </button>
        </li>
      ))}
    </ul>
  );
}

const MARKS = (
  <ul className="m-0 grid list-none justify-items-center gap-1 px-0 py-1.5">
    <StripMark label="Cap the hydration per frame · engine · working" initialOf="engine" tone="live" current />
    <StripMark label="Expose a streaming budget · game · waiting on you" initialOf="game" tone="parked" />
  </ul>
);

const meta: Meta<typeof ListPane> = {
  title: 'Work/ListPane',
  component: ListPane,
  args: {
    name: 'Sessions',
    labels: { open: 'Show the session list', close: 'Hide the session list', resize: 'session list width' },
    layout: open(280),
    bounds: LIST_BOUNDS.sessions,
    make: { label: 'Start a session', onMake: () => {} },
    strip: MARKS,
    onOpen: () => {},
    onClose: () => {},
    onDismiss: () => {},
    onResize: () => {},
    children: <Rows />,
  },
  // The frame's height, and a main area beside the list for it to sit beside or lie over.
  decorators: [(Story) => (
    <div className="relative flex h-[26rem] w-[56rem] max-w-full border border-line bg-page">
      <Story />
      <div className="min-w-0 flex-1 p-4 text-body text-ink-soft">The main area: the chosen session.</div>
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof ListPane>;

/** Open beside the main area, at its width to start. */
export const Open: Story = {};

/** Closed by the person: its strip, with its open, its ＋ and the running sessions' marks. */
export const Closed: Story = { args: { layout: CLOSED } };

/** A strip the window drew for want of room: the same strip, whose open lays the list over. */
export const StripByTheWindow: Story = { args: { layout: DRAWN } };

/** A strip the window drew, opened: the list over the main area, beside its strip. */
export const LaidOver: Story = { args: { layout: { mode: 'over', width: 280, beside: LIST_STRIP, auto: true } } };

/** At the least it is dragged to. */
export const AtItsLeast: Story = { args: { layout: open(LIST_BOUNDS.sessions.min) } };

/** At the most it is dragged to. */
export const AtItsMost: Story = { args: { layout: open(LIST_BOUNDS.sessions.max) } };

/** A view that makes two kinds of thing: the ＋ offers both, its primary first (Quests, FRAME1d). */
export const TwoKinds: Story = {
  args: {
    name: 'Quests',
    labels: { open: 'Show the quest list', close: 'Hide the quest list', resize: 'quest list width' },
    make: { label: 'New', kinds: [{ id: 'ask', label: 'Ask' }, { id: 'quest', label: 'New quest' }], onMake: () => {} },
    strip: undefined,
  },
};

/** Named in 中文, with 中文 rows. */
export const ChineseName: Story = {
  args: {
    name: '会话',
    labels: { open: '展开会话列表', close: '收起会话列表', resize: '会话列表宽度' },
    make: { label: '开始会话', onMake: () => {} },
    children: <Rows names={['限制每帧的水合量', '公开流式加载预算', '从关卡文件读取预算']} />,
  },
};

/** Nothing in it: the empty state, with the ＋'s act. */
export const Empty: Story = {
  args: { empty: { headline: 'Nothing is running', body: 'Sessions appear here as the driver takes quests and as you open conversations.' } },
};

/** Nothing in it where the ＋ makes two kinds: each offered by its own name, its primary first (PLUGUI1b). */
export const EmptyTwoKinds: Story = {
  args: {
    name: 'Plugins',
    labels: { open: 'Show the plugin list', close: 'Hide the plugin list', resize: 'plugin list width' },
    make: {
      label: 'Add a plugin',
      kinds: [{ id: 'ask', label: 'Ask Daoris for a plugin' }, { id: 'make', label: 'Make a plugin…' }],
      onMake: () => {},
    },
    strip: undefined,
    empty: {
      headline: 'No plugins on this machine',
      body: 'A plugin adds an agent sessions can run on, hands every session a server, or speaks at a point the driver asks.',
    },
  },
};

/** The list's own ⋯ in its header, before its close. */
export const WithItsMenu: Story = {
  args: { more: <ListMore label="More actions" items={[{ id: 'try', label: 'Try a folder…' }]} onChoose={() => {}} /> },
};

/** The list's filters in its ⋯ (FRAME1d): one value among several, then a toggle, each ticked as it stands. */
export const WithItsFilters: Story = {
  args: {
    name: 'Quests',
    labels: { open: 'Show the quest list', close: 'Hide the quest list', resize: 'quest list width' },
    strip: undefined,
    more: (
      <ListMore
        label="Filter the list"
        choice={{
          label: 'Receiver', value: 'engine', onChoose: () => {},
          options: [{ value: '*', label: 'Everyone' }, { value: 'engine', label: 'engine' }, { value: 'game', label: 'game' }],
        }}
        items={[{ id: 'closed', label: 'Include closed', checked: true }]}
        onChoose={() => {}}
      />
    ),
  },
};

/** Its first load: skeleton rows, never the empty state. */
export const Loading: Story = { args: { loading: true } };

/** Its first load, closed: the strip keeps its controls while its marks are on their way. */
export const LoadingClosed: Story = { args: { loading: true, layout: CLOSED } };
