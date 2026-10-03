import type { Meta, StoryObj } from '@storybook/react-vite';
import { AskPanel } from './AskPanel';
import { ProposalCard } from './ProposalCard';
import { starters } from './starters';

// Ask Daoris (HELP1, D89): its panel on a machine that lacks everything, on one that lacks nothing,
// and in Chinese widths by the same props.

const meta = {
  title: 'Help/AskPanel',
  component: AskPanel,
  args: { onGo: () => {}, onClose: () => {}, helper: null, starters: [] },
} satisfies Meta<typeof AskPanel>;

export default meta;
type Story = StoryObj<typeof meta>;

export const LacksEverything: Story = {
  args: {
    // The setup guide's standing (SETUP1a): the starters lead to it while it is not done.
    setup: { done: 0, of: 5 },
    starters: starters({
      repositories: [], drivable: [], waiting: 2, unnamedLines: [], helper: null,
      tools: [{
        name: 'claude-code', product: 'Claude Code', maker: 'Anthropic', doors: [], accounts: [], machineDefault: null,
        present: true, ownLogin: 'out', ownAccount: null, workspaceDefaults: [],
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

/** HELP1a: the conversation takes the panel, and the composer sits at its foot. The record's own look is the conversation's stories'. */
export const Talking: Story = {
  args: {
    helper: 'claude-code-acp',
    conversation: {
      body: (
        <div className="grid gap-3 text-small text-ink">
          <p className="m-0 rounded-control bg-raised px-3 py-2">how do I make engine land on a feature branch?</p>
          <p className="m-0">Settings → Workspace → How work lands, or `daoris driver landing engine branch feature/&#123;slug&#125;`.</p>
        </div>
      ),
      composer: <div className="border-t border-line px-4 py-3 text-small text-ink-faint">the composer</div>,
      ended: false,
      onNew: () => {},
    },
  },
};

/** HELP1c: what the conversation proposes sits under it, for the person to apply or not. */
export const Proposing: Story = {
  args: {
    ...Talking.args,
    conversation: {
      ...Talking.args!.conversation!,
      proposals: (
        <ul className="m-0 mt-3 grid list-none gap-2.5 p-0">
          <ProposalCard
            proposal={{
              id: 'p1a2b3c4', kind: 'setting',
              describe: 'Land `engine`\'s accepted work on a branch `feature/{quest}-{slug}`, its tree removed once landed.',
              terminal: 'daoris driver landing engine branch feature/{quest}-{slug} --tidy',
              why: 'you asked for its work on feature branches',
            }}
            onApply={() => {}}
            onDismiss={() => {}}
          />
        </ul>
      ),
    },
  },
};

/**
 * HELP6: a card for each door built since — an agent's update, an account's model and effort, a delete of
 * a record made by mistake, and a go to a screen, which changes nothing and carries no command.
 */
export const ProposingEveryKind: Story = {
  args: {
    ...Talking.args,
    conversation: {
      ...Talking.args!.conversation!,
      proposals: (
        <ul className="m-0 mt-3 grid list-none gap-2.5 p-0">
          {[
            {
              id: 'u1', kind: 'agent' as const,
              describe: 'Update `claude-code-acp`: move its pin from 0.84.0 to the newest release, installed before the pin moves.',
              terminal: 'daoris agent update claude-code-acp', why: 'you asked for the newest',
            },
            {
              id: 's1', kind: 'account' as const,
              describe: 'Set `claude-code` account `work`\'s model to `opus` and its effort to `high`.',
              terminal: 'daoris agent settings claude-code --account work model opus effort high', why: 'you want it to think harder',
            },
            {
              id: 'd1', kind: 'delete' as const,
              describe: 'Delete ask `#a1b2c3d4` “a test ask”, with the quest it became: `#q1a2b3c4` “Cap the chunk budget”.',
              terminal: 'daoris-driver ask --delete a1b2c3d4', why: 'it was a test',
            },
            {
              id: 'g1', kind: 'go' as const, describe: 'Open Settings → Setup at step 2, Ask Daoris\'s agent.', terminal: '',
              why: 'you asked where to name its agent',
            },
            {
              id: 'x1', kind: 'plugin' as const,
              describe: 'Add plugin `acme.quiet-hours` (Quiet hours 1.0.0) from `quiet-hours` in `house-plugins`, copied into Daoris\'s home under its id; the driver starts what it runs at its next look.',
              terminal: 'daoris plugin add /checkouts/house-plugins/quiet-hours', why: 'you want quests held overnight',
              plugin: {
                id: 'acme.quiet-hours', name: 'Quiet hours', version: '1.0.0',
                command: ['node', '${plugin}/hooks.mjs'], points: ['quest/consider'],
                harnesses: [], servers: [{ name: 'browser', command: ['npx', '-y', '@playwright/mcp@latest'] }], copied: true,
              },
            },
            {
              id: 'h1', kind: 'hand' as const,
              describe: 'Hand `feature/0fda18-fix-the-api-gap` (in `engine`) to plugin `example.github-pull-request`: it pushes the branch and opens the pull request against the line, signed in as you, for the work landed from session `s1a2b3c4` “Fix the API gap”.',
              terminal: 'daoris-driver trees hand feature/0fda18-fix-the-api-gap --repository engine', why: 'you want its pull request opened',
            },
            // HELP10: the browser's settings, and bringing up to date before and after the person looked.
            {
              id: 'b1', kind: 'browser' as const,
              describe: 'Links on the page open in Daoris\'s browser, from the next click. A sign-in link always opens in the system\'s browser.',
              terminal: 'daoris browser links daoris', why: 'you want tickets opened where your sign-ins are',
            },
            {
              id: 'y1', kind: 'sync' as const,
              describe: 'Bring `engine` up to date after a pull request merged. Look for updates first: Daoris fetches each line from `origin`, as you — only origin\'s own refs move — and this card then lists what the press would do.',
              terminal: 'daoris-driver trees sync --repository engine', why: 'your pull request merged',
              sync: { looked: false, rows: [] },
            },
            {
              id: 'y2', kind: 'sync' as const,
              describe: 'Bring `engine` up to date: 3 thing(s) change, only the rows below that move, each judged again right before it acts. Daoris fetches nothing more, and never pushes.',
              terminal: 'daoris-driver trees sync --repository engine --yes', why: 'your pull request merged',
              sync: {
                looked: true,
                rows: [
                  { key: 'engine:main', step: 'line' as const, moves: true, says: 'engine  main  fast-forwards 1 commit(s) to `origin/main`' },
                  { key: 'engine:daoris/s-step', step: 'replay' as const, moves: true, says: 'engine  daoris/s-step  replays 2 commit(s) of its own onto `main`, after `feature/q2-first`\'s work, which reached the line' },
                  { key: 'engine:daoris/s-busy', step: 'replay' as const, moves: false, says: 'engine  daoris/s-busy  a session still running or waiting holds its tree' },
                  { key: 'engine:feature/q2-first', step: 'delete' as const, moves: true, says: 'engine  feature/q2-first  its work is on the line' },
                ],
              },
            },
            // LEFT3 b: a look where a line could not be fetched and other repositories were left apart, said on the card.
            {
              id: 'y3', kind: 'sync' as const,
              describe: 'Bring every repository with a checkout here up to date: 1 thing(s) change, only the rows below that move, each judged again right before it acts. Daoris fetches nothing more, and never pushes.',
              terminal: 'daoris-driver trees sync --yes', why: 'your pull request merged',
              sync: {
                looked: true,
                rows: [
                  { key: 'engine:main', step: 'line' as const, moves: true, says: 'engine  main  fast-forwards 1 commit(s) to `origin/main`' },
                  { key: 'game:main', step: 'line' as const, moves: false, says: 'game  main  up to date with `origin/main` (not fetched)' },
                ],
                notFetched: [{
                  repository: 'game', fetch: 'fatal: Could not read from remote repository.',
                  lastFetch: new Date(Date.now() - 2 * 86_400_000).toISOString(), reach: 'ssh',
                }],
                apart: ['docs', 'site', 'tools'],
              },
            },
            // DRIFT1d2: the person's yes to a done's departure, each departure shown before the press.
            {
              id: 'a1', kind: 'accept' as const,
              describe: 'Accept the departure on quest `#he0he0` “Stream the tiles from the cold cache”, for `engine`: it departed from requirement 2, and what it held goes on — its next step, “Verify he0he0 in a playtest” to `game`, is published.',
              terminal: 'daoris-driver quest accept he0he0', why: 'you said the first frame may go over',
              accept: {
                quest: 'he0he0', title: 'Stream the tiles from the cold cache',
                departures: [{
                  requirement: 2, quote: 'keep the budget under the ceiling the engine sets',
                  check: 'no frame hydrates more than the ceiling in a playtest',
                  departed: 'a cold cache cannot hold the ceiling on its first frame, so that frame may hydrate twice the ceiling',
                  words: 'keep the budget under the ceiling the engine sets',
                }],
              },
            },
          ].map((proposal) => <ProposalCard key={proposal.id} proposal={proposal} onApply={() => {}} onDismiss={() => {}} />)}
        </ul>
      ),
    },
  },
};

export const Ended: Story = {
  args: { ...Talking.args, conversation: { ...Talking.args!.conversation!, ended: true } },
};
