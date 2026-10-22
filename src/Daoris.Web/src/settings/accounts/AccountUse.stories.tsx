import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { USE_DEFAULTS } from './accounts';
import {
  AHEAD, CHOICES, MACHINE_NEAR, OFFERED_AGAIN, OWN_COOLING, scopeOf, SILENT, SIX_AGENT, SIX_CHOICES, THREE,
} from './accountsFixtures';
import { AccountFactsLines, type ScopeActs, ScopeEditor, TermsLine } from './AccountUse';

// How an agent's accounts are used (TOOL4g; D125 §2.4, §3.7, §6; D130 §9, §16.6; POOL1c, D197), in every state its page shows:
// an account cooling until a stated reset, by Daoris's default, or in this machine's zone; what an agent last said; its pace
// warning, in English and 中文 (USAGEREAD3b); nothing said yet; the terms; the one set of three accounts, every workspace's,
// one over the person's own curve and one kept, its charts and points (SWITCH1d), in 中文, refused, and the switch off; six
// accounts and nothing said; one by one; an agent that says nothing of its limits; a 中文 account; and which account the next
// start takes and why (TOOL6e): offered again after a cool-off, waiting, and waiting on the tool's own sign-in.

const nothing = () => {};
const ACTS: ScopeActs = { onOrder: nothing, onUse: nothing };

const meta: Meta<typeof ScopeEditor> = {
  title: 'Settings/AccountUse',
  component: ScopeEditor,
  args: { agent: THREE, product: 'Claude Code', scope: THREE.scopes[0]!, accounts: CHOICES, acts: ACTS },
  // The main area at its wide width, as the frame gives it (the UI/UX design R6, UIFIT1).
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof ScopeEditor>;

/** This machine's three accounts, made the most of: where starts begin, one cooling, nothing kept. */
export const ThreeAccounts: Story = {};

/** The same at the half width, 630 px. */
export const ThreeAccountsHalf: Story = { decorators: [storyWidths.half] };

/** The same at the 400 px floor: each value under its words. */
export const ThreeAccountsNarrow: Story = { decorators: [storyWidths.narrow] };

/**
 * SWITCH1d: the person's own curve, its five-hour window flat at 85, with one account over it and one kept for conversations;
 * each window's chart, the five-hour window's points to edit, the week on Daoris's curve, and *Clear my own curve*.
 */
export const NearAndKept: Story = { args: { scope: MACHINE_NEAR } };

/** The same in 中文. */
export const OwnCurveChinese: Story = { args: { scope: MACHINE_NEAR }, decorators: [chinese] };

/** SWITCH1d: *Off*, which still draws Daoris's curve a start passes an account by. */
export const SwitchOff: Story = { args: { scope: { ...THREE.scopes[0]!, use: { ...USE_DEFAULTS, early: false, switch: 'off' } } } };

/**
 * SWITCH1d: a curve's points that break a rule, said in the door's own sentence before anything is saved: the five-hour
 * window's second point uses less than its first.
 */
export const OwnCurveRefused: Story = {
  args: { scope: MACHINE_NEAR },
  play: async ({ canvasElement }) => {
    const group = [...canvasElement.querySelectorAll('fieldset')][0]!;
    const add = [...group.querySelectorAll('button')].find((button) => button.textContent?.includes('Add a row'));
    add?.click();
    await new Promise((resolve) => setTimeout(resolve, 0));
    const used = group.querySelectorAll<HTMLInputElement>('input[type="number"]')[3];
    if (used) {
      const set = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!;
      set.call(used, '40');
      used.dispatchEvent(new Event('input', { bubbles: true }));
    }
    await new Promise((resolve) => setTimeout(resolve, 0));
    [...group.querySelectorAll('button')].find((button) => button.textContent?.includes('Save the curve'))?.click();
  },
};

/** Six accounts, none of which has said what it has left: the walk spreads by Daoris's own sessions, and says so. */
export const SixAccountsNothingSaid: Story = { args: { agent: SIX_AGENT, scope: SIX_AGENT.scopes[0]!, accounts: SIX_CHOICES } };

/** An order the answer has not named yet: every account of Daoris's still drawn, in the agent's order, with its settings. */
export const OrderNotNamedYet: Story = { args: { scope: scopeOf() } };

/** A hand-edited kept account the order does not hold, read with the order winning and said. */
export const KeptOutsideTheOrder: Story = {
  args: { scope: scopeOf({ list: ['account-1', 'account-2'], begins: 'account-1', problem: { kind: 'keep', account: 'account-3' } }) },
};

/** One by one, in order, and what it costs. */
export const OneByOne: Story = { args: { scope: { ...THREE.scopes[0]!, use: { ...THREE.scopes[0]!.use, use: 'order' } } } };

/** The same in 中文, at the main area's 400 px floor. */
export const OneByOneNarrowChinese: Story = { ...OneByOne, decorators: [storyWidths.narrow, chinese] };

/** An agent whose sessions say nothing of their limits: the switch is kept, and waits for that word. */
export const AgentSaysNothing: Story = {
  args: { agent: SILENT, product: 'Codex', scope: SILENT.scopes[0]!, accounts: [{ name: 'account-1', label: 'account-1' }, { name: 'account-2', label: 'account-2' }] },
};

/** A 中文 account name in the list. */
export const ChineseAccount: Story = {
  args: { accounts: [{ name: 'account-1', label: '工作账户@example.invalid', login: 'in' }, ...CHOICES.slice(1)] },
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

/** Its agent's pace warning on its week, in the agent's own words and never as near (USAGEREAD3b, D165 point 2). */
export const AccountAheadOfPace: Story = {
  render: () => <AccountFactsLines facts={AHEAD} label="home@example.invalid" onTryNow={nothing} />,
};

/** The same in 中文. */
export const AccountAheadOfPaceChinese: Story = { ...AccountAheadOfPace, decorators: [chinese] };

/** Nothing said yet, and a count of the sessions running on it. */
export const AccountNothingSaid: Story = {
  render: () => <AccountFactsLines facts={THREE.accounts[2]!} label="spare@example.invalid" onTryNow={nothing} />,
};

/** Each account's own plan and terms apply. */
export const Terms: Story = { render: () => <TermsLine /> };

/**
 * TOOL6e, the owner's case: the order's first came out of its cool-off and nothing runs, so the next start takes it; another
 * account cools; the tool's own sign-in carries none of these starts.
 */
export const NextStartOfferedAgain: Story = {
  args: { agent: OFFERED_AGAIN, scope: OFFERED_AGAIN.scopes[0]! },
};

/** The next start waits: every account of the set is held, the first offered again at its reset. */
export const NextStartWaits: Story = {
  args: {
    scope: scopeOf({
      list: ['account-1', 'account-2', 'account-3'], begins: 'account-1',
      next: {
        account: null, reason: 'waits', when: THREE.accounts[0]!.cooling!.until,
        others: [
          { account: 'account-1', hold: 'cooling', until: THREE.accounts[0]!.cooling!.until },
          { account: 'account-2', hold: 'refused' },
          { account: 'account-3', hold: 'signedOut' },
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
