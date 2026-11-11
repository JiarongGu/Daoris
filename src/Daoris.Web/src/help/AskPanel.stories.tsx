import type { ReactNode } from 'react';
import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../design/storyLanguage';
import { Button } from '../design/ui';
import { Composer } from '../work/conversation/Composer';
import { DOCK } from '../work/frame/layout';
import { Markdown } from '../work/conversation/Markdown';
import { BackToBottom } from '../work/conversation/SessionConversation';
import { AskHistory } from './AskHistory';
import { type AskConversationSlot, AskPanel } from './AskPanel';
import type { HelpConversationRow } from './history';
import { ProposalCard } from './ProposalCard';
import { publishedCounts, publishedItems } from './published/published';
import { PublishedList } from './published/PublishedList';
import { starters } from './starters';
import { TAKEN, WORKING } from '../quests/fixtures';

// Ask Daoris: its panel on a machine that lacks everything, on one that lacks nothing, and in Chinese widths by the same props.

const meta = {
  title: 'Help/AskPanel',
  component: AskPanel,
  args: { onGo: () => {}, onClose: () => {}, helper: null, starters: [] },
} satisfies Meta<typeof AskPanel>;

export default meta;
type Story = StoryObj<typeof meta>;

export const LacksEverything: Story = {
  args: {
    setup: { done: 0, of: 5 },
    starters: starters({
      repositories: [], drivable: [], waiting: 2, unnamedLines: [], helper: null,
      tools: [{
        name: 'claude-code', product: 'Claude Code', maker: 'Anthropic', doors: [], accounts: [],
        present: true, ownLogin: 'out', ownAccount: null, ownRead: null, takesRules: false,
      }],
    }),
  },
};

export const OneLine: Story = {
  args: {
    helper: 'claude-code-acp',
    starters: starters({
      repositories: ['engine', 'game'], drivable: ['engine'], tools: [], waiting: 0,
      unnamedLines: ['newbie'], helper: 'claude-code-acp',
    }),
  },
};

export const LacksNothing: Story = { args: { helper: 'claude-code-acp' } };

/** The conversation takes the panel, and the composer sits at its foot. The record's own look is the conversation's stories'. */
export const Talking: Story = {
  args: {
    helper: 'claude-code-acp',
    conversation: {
      body: (
        <div className="grid gap-3 text-small text-ink">
          <p className="m-0 rounded-control bg-raised px-3 py-2">how do I make engine land on a feature branch?</p>
          <p className="m-0">Repositories → engine → Setup → Line and landing, or `daoris place landing engine branch feature/&#123;slug&#125;`.</p>
        </div>
      ),
      composer: <div className="border-t border-line px-4 py-3 text-small text-ink-faint">the composer</div>,
      ended: false,
      onNew: () => {},
    },
  },
};

/** What the conversation proposes sits under it, for the person to apply or not. */
export const Proposing: Story = {
  args: {
    ...Talking.args,
    conversation: {
      ...Talking.args!.conversation!,
      proposals: (
        <ul className="m-0 mt-3 grid list-none gap-2.5 p-0">
          <ProposalCard
            proposal={{
              id: 'p1a2b3c4', kind: 'go',
              describe: 'Open Repositories → `web-app`, its Workflow.',
              terminal: '',
              why: 'you asked where its pull request flow is set',
            }}
            onApply={() => {}}
            onDismiss={() => {}}
          />
        </ul>
      ),
    },
  },
};

/** A card for each door built since: a go to a screen, which changes nothing and carries no command. */
export const ProposingEveryKind: Story = {
  args: {
    ...Talking.args,
    conversation: {
      ...Talking.args!.conversation!,
      proposals: (
        <ul className="m-0 mt-3 grid list-none gap-2.5 p-0">
          {[
            {
              id: 'g1', kind: 'go' as const, describe: 'Open Settings → Get started at step 2, Ask Daoris\'s agent.', terminal: '',
              why: 'you asked where to name its agent',
            },
          ].map((proposal) => <ProposalCard key={proposal.id} proposal={proposal} onApply={() => {}} onDismiss={() => {}} />)}
        </ul>
      ),
    },
  },
};

/** A published quest a session has taken: a go card saying so, *Go there* beside *Dismiss*, which only takes the card away. */
export const ProposingAGoToATakenQuest: Story = {
  args: {
    ...Talking.args,
    conversation: {
      ...Talking.args!.conversation!,
      proposals: (
        <ul className="m-0 mt-3 grid list-none gap-2.5 p-0">
          <ProposalCard
            proposal={{
              id: 'g6t7k8n9', kind: 'go', describe: 'Open Quests → quest `#q2taken0` “Stream the tiles”, taken.', terminal: '',
              why: 'you asked where the quest went',
            }}
            onApply={() => {}}
            onDismiss={() => {}}
          />
        </ul>
      ),
    },
  },
};

export const ProposingAGoToATakenQuestChinese: Story = { args: ProposingAGoToATakenQuest.args, decorators: [chinese] };

export const Ended: Story = {
  args: { ...Talking.args, conversation: { ...Talking.args!.conversation!, ended: true } },
};

// The open conversation as the dock holds it, at its 300 px floor and at the 430 px the owner's window gave it,
// side by side in a panel's height; and the whole frame at 680 px, where the dock is the frame.

const PASTED = 'to complete this https://example.atlassian.net/browse/TK-2205?focusedCommentId=1234567&page=com.example.plugin.tabpanels%3Acomments so the sprint closes';

const ANSWER = [
  'The accent is set in both themes. Run this from the repository, then reload the window:',
  '',
  '```bash',
  'curl -sS "https://example.atlassian.net/rest/api/2/issue/TK-2205?fields=summary,status,assignee,comment&expand=renderedFields" | jq ".fields.summary"',
  'daoris ask-daoris rename h1a2b3c4 "Landing on feature branches, and what the line keeps when a squash merge lands"',
  '```',
  '',
  '| key | what it holds |',
  '| --- | --- |',
  '| `--accent` | **#d208d4**, the interactive identity in both themes, never a status |',
].join('\n');

/** A conversation as its record draws it: the person's words, then the agent's answer, its code and its table. */
const record = (question: string, answer = ANSWER) => (
  <div className="grid grid-cols-[minmax(0,1fr)] gap-3">
    <p className="m-0 justify-self-end rounded-control bg-raised px-3 py-2 text-body text-ink wrap-anywhere">{question}</p>
    <Markdown text={answer} />
  </div>
);

const box = (over: Partial<Parameters<typeof Composer>[0]> = {}) => (
  <Composer live placeholder="ask about Daoris on this machine. Enter sends, Shift+Enter makes a newline." onSend={() => {}}
    onFinish={() => {}} onStop={() => {}} {...over} />
);

const said = (words: string) => <p className="m-0 text-small text-ink-soft wrap-anywhere">{words}</p>;

/** The panel in the dock, at its floor and at 430 px, each as tall as a panel. */
const inTheDock = (Story: () => ReactNode) => (
  <div className="flex flex-wrap items-start gap-6 bg-page">
    {[DOCK.floor, 430].map((width) => (
      <div key={width} style={{ width }} className="flex h-[40rem] flex-col border-x border-line bg-page"><Story /></div>
    ))}
  </div>
);

const conversation = (over: Partial<AskConversationSlot> = {}): AskConversationSlot => ({
  body: record('how do I set the accent, and the ticket’s summary?'), composer: box(), ended: false,
  title: 'how do I set the accent, and the ticket’s summary?', onNew: () => {}, onHistory: () => {}, history: null, ...over,
});

const docked = { args: { helper: 'claude-code-acp', framed: false }, decorators: [inTheDock] } satisfies Story;

/** A conversation running: its head, its words, a long fenced block wrapped at the pane, a table in its own scroll. */
export const Open: Story = { ...docked, args: { ...docked.args, conversation: conversation() } };

/**
 * What the conversation published, its strip under the head and outside the words' scroll, folded to its line and
 * opened; its rows' own states are `PublishedList`'s stories.
 */
const publishedStrip = (open: boolean) => {
  const items = publishedItems(
    [{ at: '2026-10-22T09:00:00+00:00', proposal: 'p1', kind: 'ask', workspace: 'work', ask: null, quest: TAKEN.id }],
    { quests: [TAKEN], asks: [], sessions: [{ ...WORKING, quest: TAKEN.id }] },
  );
  return <PublishedList items={items} counts={publishedCounts(items)} open={open} onToggle={() => {}} onOpen={() => {}} />;
};

export const OpenPublished: Story = { ...docked, args: { ...docked.args, conversation: conversation({ published: publishedStrip(false) }) } };

export const OpenPublishedOpened: Story = { ...docked, args: { ...docked.args, conversation: conversation({ published: publishedStrip(true) }) } };

export const OpenPublishedChinese: Story = {
  ...docked, args: { ...docked.args, conversation: conversation({ published: publishedStrip(true) }) }, decorators: [inTheDock, chinese],
};

export const OpenPastedTitle: Story = {
  ...docked, args: { ...docked.args, conversation: conversation({ title: PASTED, body: record(PASTED) }) },
};

/** The long fenced block unwrapped by its press, scrolling in its own box. */
export const OpenUnwrapped: Story = {
  ...Open,
  play: async ({ canvasElement }) => {
    canvasElement.querySelectorAll<HTMLButtonElement>('figcaption button[aria-pressed]').forEach((wrap) => wrap.click());
  },
};

/** The reader scrolled up: the way back to the tail in its own strip, above the box, covering nothing. */
export const OpenAwayFromTail: Story = {
  ...docked, args: { ...docked.args, conversation: conversation({ tail: <BackToBottom onPress={() => {}} /> }) },
};

/** One the person chose that ended and goes on in itself: what the next words do, right above the box. */
export const GoesOn: Story = {
  ...docked,
  args: {
    ...docked.args,
    conversation: conversation({
      ended: true, escapeToList: true, note: said('This conversation has ended. Write to go on in it: it remembers what was said.'),
      composer: box({ placeholder: 'write to go on in this conversation. Enter sends, Shift+Enter makes a newline.' }),
    }),
  },
};

/** One that ended and cannot go on: no box until the person chooses a new conversation from it, or a blank one. */
const readOnly = (words: { ended: string; from: string; blank: string }) => conversation({
  ended: true, escapeToList: true, composer: null,
  note: (
    <>
      {said(words.ended)}
      <div className="flex flex-wrap gap-2">
        <Button variant="primary">{words.from}</Button>
        <Button>{words.blank}</Button>
      </div>
    </>
  ),
});

export const ReadOnly: Story = {
  ...docked,
  args: {
    ...docked.args,
    conversation: readOnly({
      ended: 'This conversation has ended, and it cannot go on in itself. Start a new conversation from its words, or a blank one.',
      from: 'New conversation from it', blank: 'New conversation',
    }),
  },
};

export const ReadOnlyChinese: Story = {
  ...docked,
  args: {
    ...docked.args,
    conversation: {
      ...readOnly({
        ended: '这段对话已结束，无法在其中继续。可以由它的内容开始新对话，或开始一个空白的新对话。', from: '由此开始新对话', blank: '新对话',
      }),
      title: '怎样设置强调色，以及工单的摘要？', body: record('怎样设置强调色，以及工单的摘要？'),
    },
  },
  decorators: [inTheDock, chinese],
};

export const Checking: Story = {
  ...docked,
  args: {
    ...docked.args,
    conversation: conversation({
      ended: true, composer: null,
      note: <p role="status" className="m-0 text-small text-ink-soft">Checking whether this conversation can go on…</p>,
    }),
  },
};

/** A new conversation with nothing said yet: its head says so, and offers no new one over it. */
export const Blank: Story = {
  ...docked, args: { ...docked.args, conversation: conversation({ body: null, title: null, onNew: undefined }) },
};

const ROWS: HelpConversationRow[] = [
  {
    session: 'h1a2b3c4', title: 'Landing on feature branches', name: 'Landing on feature branches',
    opening: 'how do I make engine land on a feature branch?', about: 'Repositories → engine → Setup → **Line and landing**.',
    created: new Date(Date.now() - 86_400_000 * 9).toISOString(), last: new Date(Date.now() - 86_400_000 * 9).toISOString(),
    pinned: new Date().toISOString(), live: false, resumable: true, from: null, handed: null, found: null,
  },
  {
    session: 'h2b3c4d5', title: 'how do I set the accent, and the ticket’s summary?', name: null,
    opening: 'how do I set the accent, and the ticket’s summary?', about: 'The accent is set in both themes. Run `curl` from the repository.',
    created: new Date(Date.now() - 600_000).toISOString(), last: new Date(Date.now() - 60_000).toISOString(), pinned: null, live: true,
    resumable: true, from: null, handed: null, found: null,
  },
  {
    session: 'u1v2w3x4', title: PASTED, name: null, opening: PASTED, about: 'Opened the ticket and read its whole description.',
    created: new Date(Date.now() - 86_400_000).toISOString(), last: new Date(Date.now() - 86_400_000).toISOString(), pinned: null,
    live: false, resumable: false, from: null, handed: null, found: null,
  },
];

const history = (
  <AskHistory
    rows={ROWS} search="" shown="h2b3c4d5" onSearch={() => {}} onNew={() => {}} onClose={() => {}} onOpen={() => {}}
    onRename={() => {}} onPin={() => {}} onStartFrom={() => {}} onDelete={() => {}}
  />
);

/** The history open in the conversation's place, with its own head, and no box while it is. */
export const History: Story = {
  ...docked, args: { ...docked.args, conversation: conversation({ historyOpen: true, history }) },
};

/** At 680 px the dock is the whole frame (below 768): the same heads, laid out by the pane's own width. */
const atTheFrame = (Story: () => ReactNode) => <div className="flex h-[40rem] bg-page" style={{ width: 680 }}><Story /></div>;

export const Frame680: Story = {
  args: { helper: 'claude-code-acp', width: 680, conversation: conversation({ escapeToList: true }) },
  decorators: [atTheFrame],
};

export const Frame680History: Story = {
  args: { helper: 'claude-code-acp', width: 680, conversation: conversation({ historyOpen: true, history }) },
  decorators: [atTheFrame],
};

export const Frame680ReadOnlyChinese: Story = {
  args: { ...ReadOnlyChinese.args, framed: true, width: 680 },
  decorators: [atTheFrame, chinese],
};
