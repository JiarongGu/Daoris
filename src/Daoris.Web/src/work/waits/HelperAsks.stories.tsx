import type { Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { sessionWidths } from '../../design/storyWidths';
import { type HelperAskRow, HelperAsks } from './HelperAsks';

// The helpers a session asked for, under its head (MIXAGENT2b, D163 points 2 and 3): each ask's question as written, who asked and
// where the driver's look left it; an agent's ask that waits on the person with *Let it ask* and *Not now* (HELPERNO1), and a
// started helper's door. The head keeps those still on their way; the list draws whatever it is handed, so every state is here.
// In English, 中文 and dark, in a session's page at the frame's widths (the UI/UX design R6): its head and its foot carry it.

const row = (over: Partial<HelperAskRow> & Pick<HelperAskRow, 'id' | 'question'>): HelperAskRow => ({
  agent: null, askedBy: 'agent', at: '2026-10-16T09:00:00Z', state: null, code: null, helper: null, startedOn: null, ...over,
});

const NAMES: Record<string, string> = { 'codex-acp': 'Codex', 'claude-code': 'Claude Code' };

const ASKS: HelperAskRow[] = [
  row({ id: 'a1', question: 'Is the per-frame cap in the streamer the one the chunk API documents, or a second number?', state: 'waiting' }),
  row({ id: 'a2', question: 'Which test covers the loader’s first chunk?', askedBy: 'person', agent: 'codex-acp' }),
  row({ id: 'a3', question: 'Does the hydrator read the budget before or after the queue drains?', state: 'started', helper: 'h1', startedOn: 'codex-acp' }),
  row({ id: 'a4', question: 'Is the save format versioned anywhere else?', state: 'waiting', code: 'cap' }),
];

const meta: Meta<typeof HelperAsks> = {
  title: 'Work/HelperAsks',
  component: HelperAsks,
  args: { asks: ASKS, nameOf: (agent) => NAMES[agent] ?? agent, onApprove: () => {}, onDecline: () => {}, onOpen: () => {} },
  decorators: [sessionWidths.half],
};
export default meta;

type Story = StoryObj<typeof HelperAsks>;

/** An agent's ask waiting on the person's yes, the person's own, one being answered with its door, and one waiting on a slot. */
export const OnTheirWay: Story = {};

/** The same at a 1546 px window. */
export const OnTheirWayWide: Story = { decorators: [sessionWidths.wide] };

/** The same at the main area's 400 px floor. */
export const OnTheirWayNarrow: Story = { decorators: [sessionWidths.narrow] };

/** The same in 中文. */
export const OnTheirWayChinese: Story = {
  args: {
    asks: [
      row({ id: 'a1', question: '流式加载里的每帧上限，和区块 API 文档里写的是同一个数吗？', state: 'waiting' }),
      row({ id: 'a2', question: '哪个测试覆盖了加载器的第一个区块？', askedBy: 'person', agent: 'codex-acp' }),
      row({ id: 'a3', question: '水合器是在队列清空之前还是之后读取预算？', state: 'started', helper: 'h1', startedOn: 'codex-acp' }),
    ],
  },
  decorators: [chinese],
};

/** The same in dark. */
export const OnTheirWayDark: Story = {
  decorators: [(Story) => <InTheme theme="dark"><Story /></InTheme>],
};

/** Read only, as a browser shows them: no yes, no no and no door. */
export const NothingToPress: Story = { args: { onApprove: undefined, onDecline: undefined, onOpen: undefined } };
