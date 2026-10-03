import type { Meta, StoryObj } from '@storybook/react-vite';
import { USE_DEFAULTS } from './accounts';
import {
  CHOICES, MACHINE_NEAR, OFFERED_AGAIN, OWN_COOLING, scopeOf, SILENT, SIX_AGENT, SIX_CHOICES, THREE,
} from './accountsFixtures';
import { AccountFactsLines, OwnSignInLine, type ScopeActs, ScopeEditor, TermsLine, WorkspaceScope } from './AccountUse';

// How an agent's accounts are used (TOOL4g; D125 §2.4, §3.7, §6; D130 §3.2, §9, §16.6), in every state Settings → Agents
// shows: an account cooling until a stated reset, by Daoris's default, or in this machine's zone; what an agent last said;
// nothing said yet; the tool's own sign-in shared with the person; the terms; a scope with three accounts, one near and one
// kept; six accounts and nothing said; no list yet; a default outside the list; one by one; an agent that says nothing of
// its limits; a workspace on this machine's accounts and on its own; a 中文 account; and which account the next start takes
// and why (TOOL6e): offered again after a cool-off, waiting, and waiting on the tool's own sign-in.

const nothing = () => {};
const ACTS: ScopeActs = { onOrder: nothing, onUse: nothing, onInherit: nothing };

const meta: Meta<typeof ScopeEditor> = {
  title: 'Settings/AccountUse',
  component: ScopeEditor,
  args: { agent: THREE, product: 'Claude Code', scope: THREE.scopes[0]!, accounts: CHOICES, acts: ACTS },
  // A settings card's width, between the side bar's two states.
  decorators: [(Story) => <div className="max-w-3xl rounded-card border border-line bg-page/60 p-3"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof ScopeEditor>;

/** This machine's three accounts, made the most of: where starts begin, one cooling, nothing kept. */
export const ThreeAccounts: Story = {};

/** One near its limit by the scope's own near of 85, and one kept for conversations. */
export const NearAndKept: Story = { args: { scope: MACHINE_NEAR } };

/** Six accounts, none of which has said what it has left: the walk spreads by Daoris's own sessions, and says so. */
export const SixAccountsNothingSaid: Story = { args: { agent: SIX_AGENT, scope: SIX_AGENT.scopes[0]!, accounts: SIX_CHOICES } };

/** No account used yet: no settings, and where its starts run instead. */
export const NoListYet: Story = { args: { scope: scopeOf() } };

/** A hand-edited default outside the list, read with the list winning and said. */
export const DefaultOutsideTheList: Story = {
  args: { scope: scopeOf({ default: 'account-3', list: ['account-1'], begins: 'account-1', problem: { kind: 'default', account: 'account-3' } }) },
};

/** One by one, in order, and what it costs. */
export const OneByOne: Story = { args: { scope: THREE.scopes[1]!, workspace: 'work' } };

/** An agent whose sessions say nothing of their limits: the switch is kept, and waits for that word. */
export const AgentSaysNothing: Story = {
  args: { agent: SILENT, product: 'Codex', scope: SILENT.scopes[0]!, accounts: [{ name: 'account-1', label: 'account-1' }, { name: 'account-2', label: 'account-2' }] },
};

/** A 中文 account name in the list. */
export const ChineseAccount: Story = {
  args: { accounts: [{ name: 'account-1', label: '工作账户@example.invalid', login: 'in' }, ...CHOICES.slice(1)] },
};

/** A workspace on this machine's accounts: which those are. */
export const WorkspaceOnThisMachine: Story = {
  render: () => <WorkspaceScope agent={THREE} product="Claude Code" workspace="lab" scope={null} machine={THREE.scopes[0]!} accounts={CHOICES} acts={ACTS} />,
};

/** A workspace on its own accounts, one by one in its own order. */
export const WorkspaceOwnList: Story = {
  render: () => <WorkspaceScope agent={THREE} product="Claude Code" workspace="work" scope={THREE.scopes[1]!} machine={THREE.scopes[0]!} accounts={CHOICES} acts={ACTS} />,
};

/** An account cooling until the reset its agent named, with *Try now*, its sessions and its learned week. */
export const AccountCooling: Story = {
  render: () => <AccountFactsLines facts={THREE.accounts[0]!} label="work@example.invalid" onTryNow={nothing} />,
};

/** Cooling by Daoris's default: the agent named no time. */
export const AccountCoolingByDefault: Story = {
  render: () => (
    <AccountFactsLines
      facts={{ name: 'account-1', cooling: { ...THREE.accounts[0]!.cooling!, stated: false } }}
      label="work@example.invalid"
      onTryNow={nothing}
    />
  ),
};

/** Cooling until a time read in this machine's zone, the agent's zone not being one this machine names. */
export const AccountCoolingInThisZone: Story = {
  render: () => (
    <AccountFactsLines
      facts={{ name: 'account-1', cooling: { ...THREE.accounts[0]!.cooling!, assumedZone: true } }}
      label="work@example.invalid"
      onTryNow={nothing}
    />
  ),
};

/** What its agent last said: each window's use and reset, how long ago. */
export const AccountSaid: Story = {
  render: () => <AccountFactsLines facts={THREE.accounts[1]!} label="home@example.invalid" onTryNow={nothing} />,
};

/** Nothing said yet, and a count of the sessions running on it. */
export const AccountNothingSaid: Story = {
  render: () => <AccountFactsLines facts={THREE.accounts[2]!} label="spare@example.invalid" onTryNow={nothing} />,
};

/** The tool's own sign-in, who it is, cooling by Daoris's default, with *Sign in to another account*. */
export const OwnSignInShared: Story = {
  render: () => <OwnSignInLine who="someone@example.invalid" cooling={OWN_COOLING} signsIn onSignIn={nothing} />,
};

/** The tool's own sign-in where the tool does not say who. */
export const OwnSignInUnnamed: Story = { render: () => <OwnSignInLine signsIn onSignIn={nothing} /> };

/** Each account's own plan and terms apply. */
export const Terms: Story = { render: () => <TermsLine /> };

/**
 * TOOL6e, the owner's case: the workspace's default came out of its cool-off and nothing runs, so the next start takes it,
 * Daoris having started on it least recently; another account cools; the tool's own sign-in carries none of these starts.
 */
export const NextStartOfferedAgain: Story = {
  args: { agent: OFFERED_AGAIN, scope: OFFERED_AGAIN.scopes[1]!, workspace: 'work' },
};

/** The next start waits: every account the list uses is held, the first offered again at its reset. */
export const NextStartWaits: Story = {
  args: {
    scope: scopeOf({
      list: ['account-1', 'account-2'], begins: 'account-1',
      next: {
        account: null, reason: 'waits', when: THREE.accounts[0]!.cooling!.until,
        others: [
          { account: 'account-1', hold: 'cooling', until: THREE.accounts[0]!.cooling!.until },
          { account: 'account-2', hold: 'refused' },
          { account: 'account-3', hold: 'outside' },
        ],
      },
    }),
  },
};

/** Nothing here names an account and the tool's own sign-in cools: the next start waits for it. */
export const NextStartOwnSignInWaits: Story = {
  args: {
    scope: scopeOf({
      next: { account: null, reason: 'waits', when: OWN_COOLING.until, others: [{ account: null, hold: 'cooling', until: OWN_COOLING.until }] },
    }),
  },
};

/** An account's row, offered again since its cool-off ended within the day. */
export const AccountOfferedAgain: Story = {
  render: () => <AccountFactsLines facts={OFFERED_AGAIN.accounts[0]!} label="work@example.invalid" onTryNow={nothing} />,
};

/** Today's defaults on a scope with a list, as a file that sets nothing reads. */
export const TodaysDefaults: Story = {
  args: { scope: scopeOf({ list: ['account-2', 'account-1'], begins: 'account-2', use: USE_DEFAULTS }) },
};
