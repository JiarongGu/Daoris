import { type ReactNode, useState } from 'react';
import type { Meta, StoryObj } from '@storybook/react-vite';
import * as Tooltip from '@radix-ui/react-tooltip';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AddProjectDrawer } from '../../projects/ProjectManage';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { type CheckoutReader, NewConversation, type StartRepository } from './NewConversation';

// *New conversation* (NEW1b, D162 point 1, the entry design §1): Sessions' main area while nothing is attended. Drawn in the
// main area's own width: about 1,200 px of a 1546 px window with the list open, and about 630 px of a 680 px one, where the
// list is a strip and the chips wrap under the prompt.

const REPOSITORIES: StartRepository[] = [
  { name: 'engine', workspace: 'aurora', checkout: true },
  { name: 'game', workspace: 'aurora', checkout: true },
  { name: '世界流式加载引擎', workspace: 'aurora', checkout: true },
  { name: 'tools', workspace: 'default', checkout: true },
  { name: 'website', workspace: 'default', checkout: false },
];

const ACCOUNTS = {
  'claude-code': [
    { name: 'personal', login: 'in' as const, account: 'you@home.example' },
    { name: 'work', login: 'out' as const, account: 'you@work.example' },
  ],
  codex: [{ name: 'team', login: 'in' as const, account: 'team@example.com' }],
};

const DRAFT = 'Fix the flaky retry in the sync loop: the second attempt reads a stale cursor, so a page is fetched twice.\n'
  + 'Keep the backoff as it is.';

/** The main area at a window's width, its gutters the session's. */
const inMain = (width: number) => (Story: () => ReactNode) => (
  <Tooltip.Provider>
    <div style={{ width }} className="@container/main flex min-h-[34rem] flex-col border border-line bg-page px-4 py-3">
      <Story />
    </div>
  </Tooltip.Provider>
);
const wide = inMain(1200);
const narrow = inMain(632);

/** The composer's *Add repository…* drawer drawn over it: Projects' own, which reads the registry through a client. */
function withAddDrawer(Story: () => ReactNode) {
  const [client] = useState(() => new QueryClient());
  return (
    <QueryClientProvider client={client}>
      <Story />
      <AddProjectDrawer onClose={() => {}} notify={() => {}} />
    </QueryClientProvider>
  );
}

/**
 * The checkout's branch as the shell would have answered (NEW1e-b). A story handing none is the read still pending, refused or
 * unavailable: the checkout is said plain. Module-level, as the molecule's prop is a hook and must stay stable.
 */
const onMain: CheckoutReader = () => ({ read: { branch: 'main' }, refresh: () => {} });
const detached: CheckoutReader = () => ({ read: { branch: null }, refresh: () => {} });

const meta: Meta<typeof NewConversation> = {
  title: 'Work/New conversation',
  component: NewConversation,
  decorators: [wide],
  args: {
    repositories: REPOSITORIES,
    harnesses: ['claude-code', 'codex'],
    defaultHarness: 'claude-code',
    names: { 'claude-code': 'Claude Code', codex: 'Codex' },
    labels: { 'claude-code': 'Claude Code — direct (claude-code)', codex: 'Codex — protocol (codex-acp)' },
    accounts: ACCOUNTS,
    recent: ['tools', 'engine'],
    onStart: () => {},
    onOpenSession: () => {},
    onAdd: () => {},
    onImport: () => {},
    onMakeTask: () => {},
  },
};
export default meta;
type Story = StoryObj<typeof NewConversation>;

/** Nothing typed: Start waits for words; the chips say what a start would use, each the driver's until chosen. */
export const Empty: Story = {};

/** Words typed, a file with them, and the repository's checkout held by a session: its own tree, or that session. */
export const Typed: Story = {
  args: {
    draft: DRAFT,
    remembered: 'engine',
    busy: { engine: 's1a2b3c4' },
    carried: { links: '', files: [new File(['exit 3'], 'run.log', { type: 'text/plain' })] },
  },
};

/** The repository chip open: Recent first with each one's workspace, then each workspace's; one without a checkout says why. */
export const PickerOpen: Story = {
  args: { draft: DRAFT, busy: { game: 's1a2b3c4' }, pickerOpen: true },
};

/** A refused start: the driver's sentence, whole, under Start, the words and the choices kept (UX5 U68). */
export const Refused: Story = {
  args: {
    draft: DRAFT,
    refusal: '`engine` already has an active session — `669b2930` (awaiting-person, a chat). '
      + 'One session per working tree: two agents in one tree corrupt each other’s git state.',
  },
};

/** A start on its way: Start presses once. */
export const Starting: Story = { args: { draft: DRAFT, pending: true } };

/** No repository with a checkout here: the prompt takes words, Start waits, and *Add repository…* is the primary press (NEW1c). */
export const FirstRun: Story = {
  args: { repositories: [{ name: 'website', workspace: 'default', checkout: false }], recent: [], draft: 'Look around' },
};

/** One drawer over Sessions with the words still in the prompt (NEW1c): the composer's own, opened from the picker's foot. */
export const AddOver: Story = {
  args: { draft: DRAFT },
  decorators: [withAddDrawer],
};

/** A browser has no shell: no composer, and the place says where a conversation starts. */
export const Browser: Story = { args: { attached: false } };

/** At 680 px the chips wrap under the prompt, and Start keeps the row's end. */
export const Narrow: Story = { args: Typed.args, decorators: [narrow] };

export const NarrowPickerOpen: Story = { args: PickerOpen.args, decorators: [narrow] };

export const NarrowFirstRun: Story = { args: FirstRun.args, decorators: [narrow] };

export const NarrowAddOver: Story = { args: AddOver.args, decorators: [narrow, withAddDrawer] };

export const ChineseFirstRun: Story = { args: FirstRun.args, decorators: [chinese] };

export const ChinesePickerOpen: Story = { args: PickerOpen.args, decorators: [chinese] };

export const ChineseAddOver: Story = { args: AddOver.args, decorators: [chinese, withAddDrawer] };

export const Dark: Story = {
  args: Typed.args,
  decorators: [(Story) => <InTheme theme="dark" className="p-0"><Story /></InTheme>],
};

export const Chinese: Story = { args: Typed.args, decorators: [chinese] };

export const ChineseNarrow: Story = { args: Refused.args, decorators: [narrow, chinese] };

/**
 * Opened by Ask Daoris's go (NEW1d-b): the repository chosen and the words in, nothing sent; *Make it a task with Daoris…* sits
 * quiet beside *Start* and hands the draft back, with a line naming the repository.
 */
export const OpenedByAGo: Story = {
  args: { draft: 'Fix the flaky retry in the sync loop.', pick: { repository: 'game', n: 1 } },
};

export const NarrowOpenedByAGo: Story = { args: OpenedByAGo.args, decorators: [narrow] };

export const ChineseOpenedByAGo: Story = { args: { draft: '修复同步循环里不稳定的重试。', pick: { repository: 'game', n: 1 } }, decorators: [chinese] };

/** The checkout chosen, on a branch (NEW1e-b): the chip says *The checkout · main*, and the menu's row says it too. */
export const CheckoutOnABranch: Story = { args: { draft: DRAFT, openingTree: 'checkout', readCheckout: onMain } };

/** The checkout chosen, HEAD detached. */
export const CheckoutDetached: Story = { args: { ...CheckoutOnABranch.args, readCheckout: detached } };

/** The checkout chosen, the read not answered (pending, refused or no shell): the checkout, plain. */
export const CheckoutUnread: Story = { args: { draft: DRAFT, openingTree: 'checkout' } };

export const NarrowCheckoutOnABranch: Story = { args: CheckoutOnABranch.args, decorators: [narrow] };

export const NarrowCheckoutDetached: Story = { args: CheckoutDetached.args, decorators: [narrow] };

export const ChineseCheckoutOnABranch: Story = { args: CheckoutOnABranch.args, decorators: [chinese] };

export const ChineseCheckoutDetached: Story = { args: CheckoutDetached.args, decorators: [chinese] };

export const ChineseBrowser: Story = { args: { attached: false }, decorators: [chinese] };
