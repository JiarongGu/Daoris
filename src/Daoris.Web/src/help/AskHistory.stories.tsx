import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../storyLanguage';
import { InTheme } from '../plugins/storyIcons';
import { DOCK } from '../work/layout';
import { AskHistory } from './AskHistory';
import type { HelpConversationRow } from './history';

// ASKHIST1, as ASKHIST1c made it: Ask Daoris's history, kept on this machine only. Each state drawn at the dock's floor
// (300 px) and at the width the owner's window gave it (430 px), side by side, in the panel's own height.

const minutesAgo = (minutes: number) => new Date(Date.now() - minutes * 60_000).toISOString();
const daysAgo = (days: number) => {
  const at = new Date();
  at.setDate(at.getDate() - days);
  at.setHours(10, 0, 0, 0);
  return at.toISOString();
};

const row = (over: Partial<HelpConversationRow> & { session: string }): HelpConversationRow => ({
  title: 'what is a workspace?', name: null, opening: 'what is a workspace?', about: 'A circle of repositories that share a remote.',
  created: daysAgo(3), last: daysAgo(3), pinned: null, live: false, resumable: true, from: null, handed: null, found: null,
  foundLine: null, ...over,
});

const URL = 'https://example.atlassian.net/browse/TK-2205?focusedCommentId=1234567&page=com.example.plugin.tabpanels%3Acomments';
const URL_TOO = 'https://example.atlassian.net/browse/TK-2206?focusedCommentId=7654321&page=com.example.plugin.tabpanels%3Acomments';
const PASTED = `to complete this ${URL} so the sprint closes`;

const ROWS: HelpConversationRow[] = [
  row({ session: 'h1a2b3c4', title: 'Landing on feature branches', name: 'Landing on feature branches', pinned: minutesAgo(60),
    opening: 'how do I make engine land on a feature branch?', last: daysAgo(9),
    about: 'Repositories → engine → Setup → **Line and landing**, or `daoris driver landing engine branch feature/{slug}`.' }),
  row({ session: 'h2b3c4d5', title: 'why is engine held?', opening: 'why is engine held?', live: true, last: minutesAgo(1),
    about: 'It is held by your press on Repositories → engine; *Resume* lets its quests start again.' }),
  row({ session: 'u1v2w3x4', title: PASTED, opening: PASTED, last: minutesAgo(50), from: 'h1a2b3c4', handed: 'transcript',
    about: `Opened ${URL}#comment-1234567 and read the ticket's whole description.` }),
  row({ session: 'u2v3w4x5', title: `to complete this ${URL_TOO} so the sprint closes`, last: minutesAgo(70),
    about: '## The accent\nSet `--accent` to **#d208d4** in both themes.' }),
  row({ session: 'h3c4d5e6', title: 'how do I make a plugin that holds quests overnight?', resumable: false, last: daysAgo(1),
    about: 'As an ask at the repository that holds your plugins: it is made with its tests.' }),
  row({ session: 'h4d5e6f7', title: 'and a remote for the team?', last: daysAgo(2), about: null }),
  row({ session: 'h5e6f7a8', title: 'what does the status bar’s index say?', last: daysAgo(40),
    about: '```text\nindex: 1,204 entries · fed 3 minutes ago\n```' }),
];

const CHINESE_ROWS: HelpConversationRow[] = [
  row({ session: 'z1a2b3c4', title: '功能分支上的落地', name: '功能分支上的落地', pinned: minutesAgo(20), last: daysAgo(8),
    opening: '怎样让 engine 落在功能分支上？', about: '仓库 → engine → 设置 → **主线与落地**，或 `daoris driver landing engine branch feature/{slug}`。' }),
  row({ session: 'z2b3c4d5', title: '为什么 engine 被暂停了？', live: true, last: minutesAgo(3), about: '是你在仓库 → engine 上按下的暂停；*继续* 会让它的委托重新开始。' }),
  row({ session: 'z3c4d5e6', title: `把 ${URL} 这个工单做完`, last: daysAgo(1), resumable: false, about: `打开了 ${URL} 并读了工单的完整描述。` }),
];

const meta = {
  title: 'Help/AskHistory',
  component: AskHistory,
  args: {
    rows: ROWS, search: '', shown: 'h2b3c4d5', onSearch: () => {}, onOpen: () => {}, onRename: () => {}, onPin: () => {},
    onStartFrom: () => {}, onDelete: () => {}, onNew: () => {}, onClose: () => {}, onRetry: () => {},
  },
  decorators: [(Story, context) => {
    // The panel's two widths, side by side, each as tall as a panel: only its rows scroll.
    const panels = <div className="flex flex-wrap items-start gap-6 bg-page">
      {[DOCK.floor, 430].map((width) => (
        <div key={width} style={{ width }} className="flex h-[36rem] flex-col border-x border-line bg-page">
          <Story />
        </div>
      ))}
    </div>;
    // One theme owner around both widths, so cleanup restores the preceding story's theme once.
    return context.parameters.historyTheme === 'dark'
      ? <InTheme theme="dark" className="p-0">{panels}</InTheme>
      : panels;
  }],
} satisfies Meta<typeof AskHistory>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Pinned first, then today, yesterday, this week and older; the open one says so; two pasted URLs read apart. */
export const Grouped: Story = {};

export const GroupedChinese: Story = { args: { rows: CHINESE_ROWS, shown: 'z2b3c4d5' }, decorators: [chinese] };

export const GroupedDark: Story = {
  parameters: { historyTheme: 'dark' },
};
export const GroupedChineseDark: Story = {
  args: { rows: CHINESE_ROWS, shown: 'z2b3c4d5' },
  parameters: { historyTheme: 'dark' },
  decorators: [chinese],
};

/**
 * A search's finds, its words marked: in what was said, in place of the line, and in a title it was found in; above them, how
 * many it found and that it searched every conversation (ASKHIST1d2).
 */
export const SearchHits: Story = {
  args: {
    search: 'remote',
    total: 3,
    rows: [
      row({ session: 'h1a2b3c4', last: minutesAgo(30), found: '…A circle of `repositories` that share a **remote**, kept in step by the feed…' }),
      row({ session: 'h4d5e6f7', title: 'and a remote for the team?', last: daysAgo(2), found: 'and a remote for the team?' }),
      row({ session: 'u1v2w3x4', title: PASTED, last: daysAgo(5), found: `…pushed to the remote at ${URL} once…` }),
    ],
  },
};

export const SearchHitsChinese: Story = {
  args: {
    search: '落地',
    total: 2,
    rows: [row({ session: 'z1a2b3c4', title: '功能分支上的落地', last: daysAgo(8), found: '功能分支上的落地' }),
      row({ session: 'z4d5e6f7', title: '主线怎么设？', last: minutesAgo(9), found: '…合并之后，**落地**会把工作放到主线上…' })],
  },
  decorators: [chinese],
};

const LONG_BEFORE = 'Each repository keeps its own line: **engine** lands on `main`, the site on `release`, and the docs on whichever '
  + 'branch the writers named last week, which the feed reads again every hour and the status bar shows as it changes; ';

/**
 * A find deep in a long line (ASKHIST1d2): the driver hands the line whole as Markdown, and the row parses it, then cuts it
 * from a little before the words, so its two lines show them rather than the line's start.
 */
export const FoundInLongLine: Story = {
  args: {
    search: 'remote',
    total: 2,
    rows: [
      row({ session: 'h1a2b3c4', last: minutesAgo(30), found: '…on whichever branch… the **remote** that…',
        foundLine: `${LONG_BEFORE}the \`origin\` **remote** that every one of them pushes to is set once, in Setup.` }),
      row({ session: 'z4d5e6f7', title: '主线怎么设？', last: minutesAgo(9), found: '…**remote**…',
        foundLine: `${'每个仓库都有自己的主线，engine 落在 `main` 上，文档落在写作者上周指定的分支上；'.repeat(4)}它们推送的 **remote** 只设一次。` }),
    ],
  },
};

/** One letter typed: the search says how many it needs, and the whole list stays. One Han character searches (ASKHIST1d2). */
export const ShortSearch: Story = { args: { search: 'a' } };

export const ShortSearchChinese: Story = { args: { search: 'a' }, decorators: [chinese] };

/** A search that found nothing: that every conversation was searched, and the way out. */
export const NoMatch: Story = { args: { rows: [], search: 'nothing like this' } };

export const NoMatchUrl: Story = { args: { rows: [], search: URL } };

export const Loading: Story = { args: { rows: [], loading: true } };

/** A newer answer on its way: the last one's rows, dimmed, never blank. */
export const Refreshing: Story = { args: { search: 'land', refreshing: true } };

export const Failed: Story = { args: { rows: [], error: 'the driver is not running, so this machine’s conversations cannot be read.' } };

export const FailedOverRows: Story = { args: { error: 'the driver stopped answering.' } };

export const FailedChinese: Story = { args: { rows: [], error: '驱动没有运行，无法读取本机的对话。' }, decorators: [chinese] };

/** Before the first conversation: what the history is, that it stays here, and the way to start one. */
export const Empty: Story = { args: { rows: [] } };

export const EmptyChinese: Story = { args: { rows: [] }, decorators: [chinese] };

/** More than a page (ASKHIST1d2): how many of how many are listed, and *Show more* for the next page. */
export const More: Story = { args: { total: 457, onMore: () => {} } };

export const MoreChinese: Story = { args: { rows: CHINESE_ROWS, shown: 'z2b3c4d5', total: 457, onMore: () => {} }, decorators: [chinese] };

/** The next page on its way: its press stays, and says it is reading. */
export const LoadingMore: Story = { args: { total: 457, onMore: () => {}, loadingMore: true } };

/** The next page could not be read: why, beside the press that asks for it again. */
export const MoreFailed: Story = { args: { total: 457, onMore: () => {}, moreError: 'the driver stopped answering.' } };

/** A pin that could not be kept, said where it was pressed. */
export const Refused: Story = { args: { refusal: 'no conversation here is `h3c4d5e6` any more.' } };

/** Opens the first row's ⋯ and chooses its `item`th act, as a person would by the keyboard. */
const choose = (item: number): Story['play'] => async ({ canvasElement }) => {
  const more = canvasElement.querySelector<HTMLButtonElement>('li button[aria-haspopup="menu"]');
  more?.focus();
  more?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
  await new Promise((resolve) => setTimeout(resolve, 0));
  canvasElement.ownerDocument.querySelectorAll<HTMLElement>('[role="menuitem"]')[item]?.click();
};

const PASTED_FIRST = [row({ session: 'u1v2w3x4', title: PASTED, opening: PASTED, last: minutesAgo(5) }), ...ROWS.slice(0, 2)];

/** A rename, asked in its row: the title shown, selected, the field the row's width above presses that wrap. */
export const Rename: Story = { args: { rows: PASTED_FIRST, shown: null }, play: choose(0) };

/** A named one's rename: *Use the original question* beside Save. */
export const RenameNamed: Story = { args: { rows: [ROWS[0]!, ...ROWS.slice(1, 3)], shown: null }, play: choose(0) };

/** Its delete, asked once in its row: the sentence says the whole title and breaks it at the edge. */
export const Delete: Story = { args: { rows: PASTED_FIRST, shown: null }, play: choose(3) };
