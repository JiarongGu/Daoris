import { type ReactNode, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { clockOf, figure } from '../format';
import type { Account, AccountSettingsChange, Tool } from '../tools';
import {
  Button, Chip, Icon, Inline, Menu, type MenuAct, Pill, Prose, SectionTitle, Tip,
} from '../ui';
import { type Answered, InlineConfirm } from '../work/InlineConfirm';
import { PageHead, ViewMain } from '../work/ViewMain';
import {
  type AccountScope, type AgentAccounts, cannotLeave, machineScope, ownLine, workspaceScope,
} from '../settings/accounts';
import { AccountSettingsForm, AccountSettingsSummary } from '../settings/AccountSettings';
import { type AccountChoice, type ScopeActs, ScopeEditor, WorkspaceScope } from '../settings/AccountUse';
import { AgentRules, type AgentRulesState, type RuleAddition, type RuleScopeName } from '../settings/AgentRules';
import { ACCOUNT_COLUMNS, AccountColumnsHead, AccountRow } from './AccountRow';
import { type BackAccount, PlaceAccount, RenameAccount, ReSignIn } from './AddAccount';
import {
  type AccountAct, type AccountState, type AgentPart, type AgentUsage, accountAct, accountName, accountStates, accountWho,
  doorsSummary, joinChoices, latestRead, ownRunsFor, ownState, rulesSummary, runsFor, runsForLine, settingsSummary,
  usageLine, usageSummary, useSummary, versionOnly, workspacesSummary,
} from './agents';

/** The sections that fold (D150 §1 rule 4): the accounts never do. */
export type AgentSection = 'use' | 'workspaces' | 'doors' | 'rules' | 'settings' | 'usage';

/** A door's acts that run its own mechanism (D49 §4): its installer, its updater, its pin. */
export type DoorAction = 'install' | 'update' | 'unpin';

/** What the rules section presses, the screen's half of `daoris agent rules` (PERM1, D50). */
export type RuleActs = {
  onSwitchDefault: (id: string, on: boolean) => void;
  onRemove: (target: { scope: RuleScopeName; name: string | undefined; rule: string }) => void;
  onAdd: (addition: RuleAddition, added: () => void) => void;
  onAnswer: (id: string, accept: boolean) => void;
};

/** The add flow's last step answered (D152 §4.5): the name in its field (empty is none), and the lists ticked, null for this machine's. */
export type AddedAnswer = { name?: string; join: (string | null)[] };

/**
 * How a question asked in the page hears how its act ended (ACCTEDIT1): `done` once it landed, and `refused` with the
 * sentence the person reads, said in the question with what they entered kept. One type, the inline confirmation's, since
 * UXFIX2 made it every inline ask's contract.
 */
export type { Answered };

/** The questions that wait on their act's answer: a row's rename, a row's *Use in a workspace…*, the add flow's last step. */
type Question = 'rename' | 'place' | 'added';

/** Every press on an agent's page, each the terminal's twin (D50). */
export type AgentActs = {
  /** *Read again*: this agent's accounts, one at a time, and the tool's own sign-in (§5.3). */
  onReadAgain: () => void;
  /** An account's own *Read*, from its row or its ⋯: that account alone (ROSTER1). */
  onReadOne: (account: string) => void;
  /** A sign-in that makes a new account (D66 §3), step 2 of *Add an account…*. */
  onSignInNew: () => void;
  onAddKey: (key: string) => void;
  /** A sign-in to the account a row names, by its id (ACCT1). */
  onSignIn: (account: string) => void;
  /** *Try now*: an account's cool-off ended early, or the tool's own sign-in's with null. */
  onTryNow: (account: string | null, label: string) => void;
  /** A default set (an account) or cleared (null): the machine's, or a workspace's. */
  onDefault: (account: string | null, workspace?: string) => void;
  /** An account removed, from its *Remove…*'s ask, told back to it (UXFIX2). */
  onRemove: (account: string, answered: Answered) => void;
  /** An account named by the person, or its name taken back with null (ACCT2, `daoris agent profile rename`). */
  onRename: (account: string, name: string | null, answered: Answered) => void;
  /**
   * An account put into the lists named, a workspace's or null for this machine's (ACCT1, `daoris agent profile join`): from a
   * row's question, which hears the answer, or from its ⋯, which has no question to say a refusal in.
   */
  onJoin: (account: string, join: (string | null)[], answered?: Answered) => void;
  /** The new account's name and lists, at the add flow's end; *Not now* ticks none, and the account says *no workspace*. */
  onAddedAnswer: (answer: AddedAnswer, answered: Answered) => void;
  onSaveSettings: (account: string, label: string, change: AccountSettingsChange) => void;
  onDoor: (door: string, action: DoorAction) => void;
  onPin: (door: string, version: string) => void;
  scope: ScopeActs;
  rules: RuleActs;
};

/**
 * **An agent's page** (UX7b, D152 §4; first built by UX6e, D150 §5.2): its header is one line, the product, its maker, its
 * version and whether it is installed, with *Add an account…* and a ⋯; then its accounts, one row each in columns that line
 * up, each with the one act its state asks for, and your own sign-in last; then its sections, each folded to a line naming
 * its values: how accounts are used, its workspaces, its ways in, what it may do, model and effort, usage.
 *
 * @remarks
 * **A molecule**: every state arrives as props, the panels a press opens arrive as nodes (a sign-in's, a door's console),
 * and every press goes out. It holds only what the person has open: a section, *Add an account…*'s first step, a row's
 * question, *Remove…*'s ask, a form, and why a question's act was refused (ACCTEDIT1).
 *
 * - **A section appears only where the agent has that concept** (§5.1): what it may do only where Daoris hands its agent
 *   the rules file, model and effort only where Daoris knows the tool's settings, how accounts are used only where it has
 *   accounts of Daoris's own. Adding an agent adds a row to the list and this page to it, never a screen.
 * - **A door is a property, never a second roster** (D53, D57 §c): *Ways in* lists each door and its version; the accounts
 *   are the agent's.
 * - **Nothing is read when it opens** (§5.3): each account says its last known state and when it was read, and *Read* or
 *   *Read again* asks on the press.
 * - **Explanation folds** (D152 §4.4): your own sign-in's paragraph is its row's ⓘ, the plans and terms one line that opens.
 */
export function AgentPage({
  tool, use, rules, usage, workspaces, adapter, part, reading = false, busy = false, settingsBusy = false, signingIn = null,
  signInPanel, signInNewPanel, added = null, doorRunning = null, doorConsole, acts, onAnchored, now,
}: {
  tool: Tool;
  /** How its accounts are used (TOOL4g): null from a shell older than that, and the sections it feeds are absent. */
  use: AgentAccounts | null;
  /** The rules file's answer (PERM1): null where it is not read, and *What it may do* is absent. */
  rules: AgentRulesState | null;
  /** What each of its accounts has carried (TOOL3), by its doors. */
  usage: AgentUsage[];
  /** Every workspace this machine has, the unnamed `default` included: the CLI sets that one's accounts too. */
  workspaces: string[];
  /** The adapter driven work starts on (`driver.json`'s). */
  adapter?: string;
  /** The part a door named, opened and brought into view. */
  part?: AgentPart | null;
  /** *Read again* is on its way. */
  reading?: boolean;
  /** An act on the agent is on its way: one at a time, by construction. */
  busy?: boolean;
  settingsBusy?: boolean;
  /** The account a sign-in is running for, whose row holds `signInPanel`. */
  signingIn?: string | null;
  signInPanel?: ReactNode;
  /** A sign-in to a new account, running: the add flow's step 2, under the header. */
  signInNewPanel?: ReactNode;
  /** The new account a sign-in kept, by its id and who signed in, waiting on the add flow's step 3. */
  added?: { account: string; who: string | null } | null;
  /** The door whose action runs, whose block holds `doorConsole`. */
  doorRunning?: string | null;
  doorConsole?: ReactNode;
  acts: AgentActs;
  /** Told once the part a door named is in view. */
  onAnchored?: () => void;
  /** The moment the spans are measured to; now, unless a story's. */
  now?: Date;
}) {
  const { t } = useTranslation();
  const door = tool.doors[0]!;
  const signsIn = door.signsIn !== false;
  const product = tool.product ?? tool.name;
  const machine: AccountScope | null = use ? machineScope(use) : null;
  const states = accountStates(tool, use);
  const own = ownState(tool, use);
  // When any of its accounts was last read, said once at the head of the list beside *Read again*.
  const latest = latestRead([own, ...states.values()]);
  const ownLabel = t('agents.account.own');
  const accountOf = (name: string) => tool.accounts.find((each) => each.name === name);
  const labelOf = (name: string) => {
    const account = accountOf(name);
    return account ? accountName(account) : name;
  };
  const choices: AccountChoice[] = tool.accounts.map((account) => ({
    name: account.name, label: labelOf(account.name), login: account.login, keyed: Boolean(account.key),
  }));
  // D130 §3.1: a scope with a list of its own takes a default only from it, so a press the list would refuse is not offered.
  const mayDefault = (name: string, workspace?: string) => {
    const scope = use ? (workspace ? workspaceScope(use, workspace) : machine) : null;
    return !scope || scope.list.length === 0 || scope.list.includes(name);
  };
  const scopeWorkspaces = [...new Set([
    ...workspaces, ...(use?.scopes.map((scope) => scope.workspace).filter((name): name is string => Boolean(name)) ?? []),
  ])].sort();
  const sessions = usage.reduce((sum, account) => sum + account.sessions, 0);
  const rulesShown = tool.takesRules && rules !== null;
  const settingsShown = Boolean(tool.settingsChoices) && tool.accounts.some((account) => account.settings);
  const joinable = (name: string) => joinChoices(tool, use, scopeWorkspaces, labelOf, name);

  // What the person has open: a section, the add flow's first step, a row's question or rename, *Remove…*'s ask, the model
  // and effort form, a key's field, a pin's version, the plans and terms.
  const [open, setOpen] = useState<ReadonlySet<AgentSection>>(() => new Set<AgentSection>([
    ...(part === 'rules' ? ['rules' as const] : []),
    ...(part === 'usage' ? ['usage' as const] : []),
    // An agent not installed waits on the person's install, which is in its ways in: shown, never folded away.
    ...(!tool.present ? ['doors' as const] : []),
  ]));
  // A proposal waiting is the one thing in *What it may do* that waits on the person, so it opens there, once: the person's
  // closing it again stands.
  const waiting = rules?.proposals?.some((proposal) => proposal.state === 'waiting' || proposal.state === 'proposed') ?? false;
  const opened = useRef(false);
  useEffect(() => {
    if (!waiting || opened.current) return;
    opened.current = true;
    setOpen((was) => new Set([...was, 'rules' as const]));
  }, [waiting]);
  const [goingBack, setGoingBack] = useState(false);
  const [placing, setPlacing] = useState<string | null>(null);
  const [renaming, setRenaming] = useState<string | null>(null);
  const [removing, setRemoving] = useState<string | null>(null);
  const [tuning, setTuning] = useState<string | null>(null);
  const [keying, setKeying] = useState(false);
  const [keyDraft, setKeyDraft] = useState('');
  const [pinning, setPinning] = useState<Record<string, string>>({});
  const [pinOpen, setPinOpen] = useState<string | null>(null);
  const [termsOpen, setTermsOpen] = useState(false);
  // ACCTEDIT1: a question closes once its act lands, never on the press, so a refused one keeps what the person entered and
  // says why in it; the refusal is the question's, by its kind and its account, and goes when it is asked again or closed.
  const [refused, setRefused] = useState<{ question: Question; account: string; sentence: string } | null>(null);
  const answered = (question: Question, account: string, close: () => void = () => {}): Answered => {
    setRefused(null);
    return { done: close, refused: (sentence) => setRefused({ question, account, sentence }) };
  };
  const refusalOf = (question: Question, account: string) =>
    (refused?.question === question && refused.account === account ? refused.sentence : null);
  const ask = (question: 'rename' | 'place', account: string | null) => {
    setRefused(null);
    (question === 'rename' ? setRenaming : setPlacing)(account);
  };
  const toggle = (section: AgentSection) => setOpen((was) => {
    const next = new Set(was);
    if (next.has(section)) next.delete(section);
    else next.add(section);
    return next;
  });

  // A door that names a part opens it and brings it into view once it is drawn (D150 §2.4: the Agents menu's *Usage* and
  // *What agents may do*), as Settings brings its parts (UX5 U72); then it is told, so a later visit opens at the top.
  const anchored = useRef(onAnchored);
  anchored.current = onAnchored;
  useEffect(() => {
    if (!part) return undefined;
    if (part === 'rules' || part === 'usage') setOpen((was) => new Set([...was, part]));
    const bring = () => {
      const found = document.getElementById(`agents-${part}`);
      if (!found) return false;
      found.scrollIntoView?.({ block: 'start' });
      anchored.current?.();
      return true;
    };
    if (bring()) return undefined;
    const watch = new MutationObserver(() => { if (bring()) watch.disconnect(); });
    watch.observe(document.body, { childList: true, subtree: true });
    return () => watch.disconnect();
  }, [part]);

  const addKey = () => {
    const key = keyDraft.trim();
    if (!key) return;
    setKeying(false);
    setKeyDraft('');
    acts.onAddKey(key);
  };

  // *Add an account…*'s first step (D152 §4.5): the accounts a person signing one back in means, each signed out or unknown.
  // With none, it starts the sign-in at once.
  const back: BackAccount[] = tool.accounts
    .filter((account) => !account.key && ['out', 'unknown'].includes(states.get(account.name)!.state))
    .map((account) => ({
      id: account.name, name: accountName(account), state: states.get(account.name)!,
      runs: runsForLine(runsFor(tool, use, account.name)),
    }));
  const startAdding = () => {
    if (back.length > 0) setGoingBack(true);
    else acts.onSignInNew();
  };

  // The header's ⋯ (D152 §4.1): a door's own acts, which *Ways in* keeps too, and the agent's id, which left the head.
  const owning = tool.doors.find((each) => each.present) ?? door;
  const pinnable = tool.doors.find((each) => each.pinnable);
  const headMenu: MenuAct[] = [
    ...(tool.present && (owning.updates === 'pin' || owning.updates === 'tool')
      ? [{ id: 'update', label: t('harness.update'), disabled: busy, onSelect: () => acts.onDoor(owning.harness, 'update') }] : []),
    ...(pinnable && !pinnable.pinned
      ? [{
        id: 'pin', label: t('agents.head.pin'), disabled: busy,
        onSelect: () => { setOpen((was) => new Set([...was, 'doors' as const])); setPinOpen(pinnable.harness); },
      }] : []),
    ...(pinnable?.pinned
      ? [{ id: 'unpin', label: t('harness.pin.unpin'), disabled: busy, onSelect: () => acts.onDoor(pinnable.harness, 'unpin') }] : []),
    { id: 'copy', label: t('contextMenu.act.copyAgent'), icon: 'copy' as const, copy: tool.name },
  ];
  const version = tool.doors.find((each) => each.present)?.version;
  const facts = [tool.maker, version ? versionOnly(version) : null, t(tool.present ? 'harness.installed' : 'harness.absent')]
    .filter((fact): fact is string => Boolean(fact));

  const header = (
    <PageHead
      title={product}
      pills={(
        <span className="flex min-w-0 flex-wrap items-baseline gap-x-1.5 text-small text-ink-faint">
          {facts.map((fact, at) => (
            <span key={at} className="flex items-baseline gap-x-1.5">
              {at > 0 && <span aria-hidden>·</span>}
              <span>{fact}</span>
            </span>
          ))}
        </span>
      )}
      acts={(
        <>
          {tool.present && signsIn && (
            <Button disabled={busy} onClick={startAdding}>
              <Icon name="plus" size={13} />
              {t('agents.add.open')}
            </Button>
          )}
          {tool.present && door.takesKey && !keying && (
            <Button variant="ghost" disabled={busy} onClick={() => setKeying(true)}>
              <Icon name="account" size={13} />
              {t('harness.profile.addKey')}
            </Button>
          )}
          {/* An agent not installed heads with its own installer (D152 §4.6), the same one *Ways in* runs. */}
          {!tool.present && (
            <Button variant="primary" disabled={busy} onClick={() => acts.onDoor(door.harness, 'install')}>{t('harness.install')}</Button>
          )}
          <Menu.Root>
            <Tip content={t('agents.head.more', { agent: product })}>
              <Menu.Trigger asChild>
                <Button variant="ghost" aria-label={t('agents.head.more', { agent: product })} className="h-[1.9rem] w-[1.9rem] justify-center px-0">
                  <Icon name="more" size={15} />
                </Button>
              </Menu.Trigger>
            </Tip>
            <Menu.Content side="bottom" align="end" className="min-w-48">
              <Menu.Acts acts={headMenu} onCopy={(text) => { void navigator.clipboard?.writeText(text).catch(() => {}); }} />
            </Menu.Content>
          </Menu.Root>
        </>
      )}
    />
  );

  const ownMenu: MenuAct[] = [
    // Naming no account clears the default: the tool's own sign-in again (D49 §4).
    ...(tool.machineDefault !== null ? [{ id: 'default', label: t('agents.account.useDefault'), onSelect: () => acts.onDefault(null) }] : []),
    ...tool.workspaceDefaults.map((circle) => ({
      id: `clear:${circle.workspace}`, label: t('agents.account.clearIn', { workspace: circle.workspace }),
      onSelect: () => acts.onDefault(null, circle.workspace),
    })),
  ];

  // The lists an account is in, the machine's and each workspace's own, for its ⋯'s *Remove from …'s list* (D152 §4.2).
  const listsHolding = (name: string): { workspace: string | null; scope: AccountScope }[] => (use?.scopes ?? [])
    .filter((scope) => scope.list.includes(name))
    .map((scope) => ({ workspace: scope.workspace ?? null, scope }));

  const accountMenu = (account: Account, state: AccountState): MenuAct[] => {
    const name = account.name;
    return [
      ...(tool.machineDefault !== name && mayDefault(name)
        ? [{ id: 'default', label: t('agents.account.useDefault'), disabled: busy, onSelect: () => acts.onDefault(name) }] : []),
      ...workspaces.filter((workspace) => mayDefault(name, workspace)
        && !tool.workspaceDefaults.some((circle) => circle.workspace === workspace && circle.profile === name))
        .map((workspace) => ({
          id: `default:${workspace}`, label: t('agents.account.useDefaultIn', { workspace }), disabled: busy,
          onSelect: () => acts.onDefault(name, workspace),
        })),
      ...joinable(name).map((choice) => ({
        id: `join:${choice.workspace ?? ''}`, disabled: busy,
        label: choice.workspace ? t('agents.account.addTo', { workspace: choice.workspace }) : t('agents.account.addToMachine'),
        onSelect: () => acts.onJoin(name, [choice.workspace]),
      })),
      // Taken out as `profile order` writes the list without it; a press the terminal would refuse is offered disabled.
      ...listsHolding(name).map(({ workspace, scope }) => ({
        id: `leave:${workspace ?? ''}`, disabled: busy || cannotLeave(scope, name) !== null,
        label: workspace ? t('agents.account.removeFrom', { workspace }) : t('agents.account.removeFromMachine'),
        onSelect: () => acts.scope.onOrder(workspace, scope.list.filter((each) => each !== name)),
      })),
      ...(settingsShown && account.settings && tool.settingsChoices
        ? [{ id: 'settings', label: t('agents.account.settings'), onSelect: () => { setOpen((was) => new Set([...was, 'settings' as const])); setTuning(name); } }]
        : []),
      { id: 'rename', label: t('agents.account.rename'), disabled: busy, onSelect: () => ask('rename', name) },
      ...(signsIn && !account.key && state.state !== 'out'
        ? [{ id: 'signIn', label: t('harness.login.again'), disabled: busy || !tool.present, onSelect: () => acts.onSignIn(name) }]
        : []),
      // That one account asked again, on the press (§5.3): an agent not installed has nobody to ask.
      ...(tool.present
        ? [{ id: 'read', label: t('agents.account.readAgain'), disabled: reading || busy, onSelect: () => acts.onReadOne(name) }]
        : []),
      { id: 'remove', label: t('agents.account.remove'), icon: 'remove' as const, disabled: busy, onSelect: () => setRemoving(name) },
    ];
  };

  /** A row's one act, worded and wired (D152 §4.2's table). */
  const actOf = (act: AccountAct | null, id: string | null, label: string) => {
    if (!act) return undefined;
    switch (act.act) {
      case 'signIn':
        return {
          label: t('harness.login.action'), ariaLabel: t('agents.act.signInTo', { account: label }), loud: act.loud,
          onPress: () => acts.onSignIn(id!), disabled: busy,
        };
      case 'read':
        return {
          label: t('agents.act.read'), ariaLabel: t('agents.act.readFor', { account: label }), loud: act.loud,
          // Your own sign-in has no read of its own: *Read again* asks it with the agent's accounts (§5.3).
          onPress: () => (id ? acts.onReadOne(id) : acts.onReadAgain()), disabled: reading || busy,
        };
      case 'tryNow':
        return {
          label: t('harness.cooling.tryNow'), ariaLabel: t('harness.cooling.tryNowFor', { account: label }),
          onPress: () => acts.onTryNow(id, label), disabled: busy,
        };
      // A key its provider refused (ACCTUX1): the header's own key field, opened under the list, since no door yet puts a
      // new key into the same account; the pill's tip says the rest of the repair.
      case 'newKey':
        return {
          label: t('harness.profile.addKey'), ariaLabel: t('agents.act.newKeyFor', { account: label }), loud: act.loud,
          onPress: () => setKeying(true), disabled: busy,
        };
      default:
        return {
          label: t('agents.act.place'), ariaLabel: t('agents.act.placeFor', { account: label }),
          onPress: () => ask('place', id), disabled: busy,
        };
    }
  };

  const accountRow = (account: Account) => {
    const name = accountName(account);
    const state = states.get(account.name)!;
    const runs = runsFor(tool, use, account.name);
    const facts = use?.accounts.find((each) => each.name === account.name);
    // *Use in a workspace…* only where there is a list to join; an older shell's page names none.
    const act = accountAct(state, {
      signsIn: signsIn && !account.key, present: tool.present, runs: runs.length, takesKey: Boolean(door.takesKey),
    });
    const offered = act?.act === 'place' && joinable(account.name).length === 0 ? null : act;
    // What its agent last said, and since when it is offered again, under that row alone; a cool-off's column says its hold's
    // end, so each reset the agent reported is said here beside its window, and one Daoris chose the length of says the
    // reset is unknown (ACCTUX1).
    const said = usageLine(facts, state);
    return (
      <AccountRow
        key={account.home}
        name={name}
        who={accountWho(account)}
        id={account.name}
        home={account.home}
        state={state}
        runs={runsForLine(runs)}
        current={typeof facts?.running === 'number' && facts.running > 0 ? t('agents.now.sessions', { count: facts.running }) : null}
        said={said}
        act={actOf(offered, account.name, name)}
        menu={accountMenu(account, state)}
        now={now}
      >
        {/* The one inline confirmation (UXFIX2): open until the removal answers, a refusal said inside it. */}
        {removing === account.name && (
          <InlineConfirm
            label={t('harness.profile.removeTitle', { account: name })}
            says={t('harness.profile.removeConfirm')}
            meanIt={t('harness.profile.removeMeanIt')}
            busy={busy}
            onConfirm={(answered) => acts.onRemove(account.name, answered)}
            onClose={() => setRemoving((was) => (was === account.name ? null : was))}
          />
        )}
        {placing === account.name && (
          <PlaceAccount
            agent={tool.name}
            account={account.name}
            title={t('agents.place.title', { account: name })}
            choices={joinable(account.name)}
            busy={busy}
            refusal={refusalOf('place', account.name)}
            cancelLabel={t('common.cancel')}
            onDone={({ join }) => acts.onJoin(account.name, join, answered('place', account.name, () => {
              setPlacing((was) => (was === account.name ? null : was));
            }))}
            onCancel={() => ask('place', null)}
          />
        )}
        {renaming === account.name && (
          <RenameAccount
            agent={tool.name}
            account={account.name}
            current={account.displayName?.trim() || null}
            fallback={accountName({ ...account, displayName: null })}
            busy={busy}
            refusal={refusalOf('rename', account.name)}
            onSave={(named) => acts.onRename(account.name, named, answered('rename', account.name, () => {
              setRenaming((was) => (was === account.name ? null : was));
            }))}
            onCancel={() => ask('rename', null)}
          />
        )}
        {signingIn === account.name && signInPanel}
      </AccountRow>
    );
  };

  // The add flow's step 3 offers the name the person gave at the sign-in, else who signed in (ACCT2), else none; and says
  // what it is called left empty, which is the row's own namer's answer with no name given (ACCTUX1).
  const addedAccount = added ? accountOf(added.account) : undefined;
  const offeredName = addedAccount?.displayName?.trim() || added?.who || '';
  const addedFallback = added
    ? accountName({ name: added.account, key: addedAccount?.key, account: added.who ?? addedAccount?.account })
    : '';
  const adding = (goingBack && !signInNewPanel && !added) || Boolean(signInNewPanel) || Boolean(added);

  return (
    <ViewMain header={header}>
      {/* *Add an account…* (D152 §4.5), under the header where it was started: step 1, the sign-in, or step 3. */}
      {adding && (
        <div className="mb-4 grid gap-2">
          {goingBack && !signInNewPanel && !added && (
            <ReSignIn
              accounts={back}
              busy={busy}
              now={now}
              onSignIn={(id) => { setGoingBack(false); acts.onSignIn(id); }}
              onNew={() => { setGoingBack(false); acts.onSignInNew(); }}
              onClose={() => setGoingBack(false)}
            />
          )}
          {signInNewPanel}
          {added && (
            <PlaceAccount
              key={added.account}
              agent={tool.name}
              account={added.account}
              title={t('agents.add.title')}
              signedIn={added.who}
              offered={offeredName}
              fallback={addedFallback}
              choices={joinable(added.account)}
              busy={busy}
              refusal={refusalOf('added', added.account)}
              cancelLabel={t('agents.add.notNow')}
              // The organism closes this step once both acts land (`added` goes); a refusal is said in it.
              onDone={(answer) => acts.onAddedAnswer(answer, answered('added', added.account))}
              onCancel={(answer) => acts.onAddedAnswer({ ...answer, join: [] }, answered('added', added.account))}
            />
          )}
        </div>
      )}

      {/* The accounts: one list, whatever doors reach the agent (D150 §5.2, TOOL6g), a row each (D152 §4.2). Never folded;
          an agent not installed has none until it is (§4.6), and no list head stands over nothing. */}
      {(tool.present || tool.accounts.length > 0) && (
        <section id="agents-accounts" aria-label={t('harness.accounts')} className="scroll-mt-3">
          <div className="mb-1 flex flex-wrap items-baseline gap-x-3 gap-y-1">
            <SectionTitle>{t('harness.accounts')}</SectionTitle>
            <span className="ml-auto flex items-baseline gap-2 text-meta text-ink-faint">
              {latest && <span>{t('agents.read.at', { when: clockOf(latest, now) })}</span>}
              {tool.present && (
                <Tip content={t('agents.read.againTip', { agent: product })}>
                  <Button variant="ghost" disabled={reading || busy} aria-busy={reading || undefined} onClick={acts.onReadAgain}>
                    <Icon name="refresh" size={13} />
                    {reading ? t('agents.read.reading') : t('agents.read.again')}
                  </Button>
                </Tip>
              )}
            </span>
          </div>
          <ul className={ACCOUNT_COLUMNS}>
            {(tool.accounts.length > 0 || tool.present) && <AccountColumnsHead />}
            {tool.accounts.map(accountRow)}
            {/* Your own sign-in last (D152 §4.3), its explanation on its name's ⓘ: the one sign-in no list holds, which an agent
                not installed has none of. */}
            {tool.present && (
              <AccountRow
                name={ownLabel}
                who={tool.ownAccount}
                why={ownLine(tool.ownAccount, use?.own.cooling)}
                state={own}
                runs={runsForLine(ownRunsFor(tool, use, scopeWorkspaces))}
                // No window of the tool's own sign-in is read apart from the accounts' (TOOL6c), so its line says only that a
                // cool-off Daoris chose the length of has no known reset (ACCTUX1).
                said={usageLine(null, own)}
                act={actOf(own.state === 'cooling' || own.state === 'unknown' ? accountAct(own, { signsIn: false, present: tool.present, runs: 1 }) : null, null, ownLabel)}
                menu={ownMenu}
                now={now}
              />
            )}
          </ul>
          {/* Each account's own plan and terms apply (D130 point 12): one line at the list's foot that opens the paragraph. */}
          {tool.accounts.length > 0 && (
            <div className="mt-2">
              <button
                type="button"
                aria-expanded={termsOpen}
                onClick={() => setTermsOpen((was) => !was)}
                className="flex items-center gap-1 rounded-control text-meta text-ink-faint hover:text-ink"
              >
                {t('agents.terms.show')}
                <Icon name={termsOpen ? 'chevronDown' : 'chevronRight'} size={12} />
              </button>
              {termsOpen && <p className="m-0 mt-1 text-meta text-ink-faint">{t('harness.terms')}</p>}
            </div>
          )}
          {keying && (
            <form className="mt-2 flex flex-wrap items-center gap-2" onSubmit={(event) => { event.preventDefault(); addKey(); }}>
              <input
                autoFocus
                type="password"
                autoComplete="off"
                spellCheck={false}
                value={keyDraft}
                onChange={(event) => setKeyDraft(event.target.value)}
                aria-label={t('harness.profile.keyLabel', { tool: product })}
                placeholder={t('harness.profile.keyPlaceholder')}
                className="min-w-72 flex-1 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink outline-none placeholder:text-ink-faint"
              />
              <Button type="submit" variant="primary" disabled={busy || !keyDraft.trim()}>{t('harness.profile.keySave')}</Button>
              <Button variant="ghost" onClick={() => { setKeying(false); setKeyDraft(''); }}>{t('common.cancel')}</Button>
              <span className="basis-full text-meta text-ink-faint">{t('harness.profile.keyHint')}</span>
            </form>
          )}
        </section>
      )}

      {/* How the accounts are used (TOOL4g; D130 §3.2, §16.6): this machine's list and settings, folded to the scopes. */}
      {use && machine && tool.accounts.length > 0 && (
        <Fold section="use" title={t('harness.use.title')} summary={useSummary(use, labelOf)} open={open.has('use')} onToggle={toggle}>
          <ScopeEditor agent={use} product={product} scope={machine} accounts={choices} busy={busy} acts={acts.scope} />
        </Fold>
      )}

      {/* Each workspace on this machine's accounts or its own (D130 §3.2), moved here from Settings (D150 §3.1). */}
      {use && machine && tool.accounts.length > 0 && (
        <Fold
          section="workspaces"
          title={t('agents.section.workspaces')}
          summary={workspacesSummary(use, scopeWorkspaces, labelOf)}
          open={open.has('workspaces')}
          onToggle={toggle}
        >
          {scopeWorkspaces.length === 0 && <p className="m-0 text-small text-ink-faint">{t('agents.workspaces.none')}</p>}
          {scopeWorkspaces.map((workspace) => (
            <WorkspaceScope
              key={workspace}
              agent={use}
              product={product}
              workspace={workspace}
              scope={workspaceScope(use, workspace)}
              machine={machine}
              accounts={choices}
              busy={busy}
              acts={acts.scope}
            />
          ))}
        </Fold>
      )}

      {/* The ways in, a property of the agent (D53, D57 §c): each door, its version, its pin, which one driven work starts on. */}
      <Fold section="doors" title={t('harness.doors')} summary={doorsSummary(tool, adapter)} open={open.has('doors')} onToggle={toggle}>
        {tool.doors.map((each) => (
          <div key={each.harness} className="border-t border-line py-2 first:border-t-0">
            <header className="flex flex-wrap items-center gap-2">
              <Pill tone="neutral">{t(each.wire === 'acp' ? 'harness.wire.acp' : 'harness.wire.pipe')}</Pill>
              <span className="font-mono text-small text-ink">{each.harness}</span>
              {each.present
                ? <span className="font-mono text-small text-ink-faint">{versionOnly(each.version ?? '')}</span>
                : <span className="text-small text-ink-faint">{t('harness.absent')}</span>}
              {each.harness === adapter && <Chip accent>{t('harness.spawns')}</Chip>}
              {each.plugin && <Chip>{t('harness.declaredBy', { plugin: each.plugin })}</Chip>}
              <span className="ml-auto flex gap-2">
                {!each.present && <Button disabled={busy} onClick={() => acts.onDoor(each.harness, 'install')}>{t('harness.install')}</Button>}
                {each.present && (each.updates === 'pin' || each.updates === 'tool') && (
                  <Tip content={t(each.updates === 'pin' ? 'harness.update.pinTip' : 'harness.update.toolTip')}>
                    <Button variant="ghost" disabled={busy} onClick={() => acts.onDoor(each.harness, 'update')}>{t('harness.update')}</Button>
                  </Tip>
                )}
              </span>
            </header>
            {each.problem && (
              <Tip content={each.problem}>
                <p className="mt-1.5 line-clamp-2 text-small text-ink-soft"><Inline text={each.problem} /></p>
              </Tip>
            )}
            {each.pinnable && (
              <div className="mt-2 flex flex-wrap items-center gap-2 text-small">
                {each.pinned ? (
                  <>
                    <Pill tone={each.managed ? 'done' : 'declined'}>
                      {t(each.managed ? 'harness.pin.pinned' : 'harness.pin.missing', { version: each.pinned })}
                    </Pill>
                    {each.managed && (
                      <Tip content={t('harness.pin.managedTip')}>
                        <span className="min-w-0 flex-1 truncate font-mono text-meta text-ink-faint">{each.managed}</span>
                      </Tip>
                    )}
                    <Button variant="ghost" className="ml-auto" disabled={busy} onClick={() => acts.onDoor(each.harness, 'unpin')}>
                      {t('harness.pin.unpin')}
                    </Button>
                  </>
                ) : (
                  <>
                    <span className="text-ink-faint">{t(each.present ? 'harness.pin.fromPath' : 'harness.pin.fromPathAbsent')}</span>
                    <Button
                      variant="ghost"
                      className="ml-auto"
                      aria-expanded={pinOpen === each.harness}
                      onClick={() => setPinOpen(pinOpen === each.harness ? null : each.harness)}
                    >
                      {t('harness.pin.open')}
                    </Button>
                  </>
                )}
              </div>
            )}
            {each.pinnable && !each.pinned && pinOpen === each.harness && (
              <form
                className="mt-2 flex flex-wrap items-center gap-2"
                onSubmit={(event) => {
                  event.preventDefault();
                  const version = (pinning[each.harness] ?? '').trim();
                  if (version) acts.onPin(each.harness, version);
                }}
              >
                <input
                  autoFocus
                  aria-label={t('harness.pin.version', { harness: each.harness })}
                  value={pinning[each.harness] ?? ''}
                  onChange={(event) => setPinning((held) => ({ ...held, [each.harness]: event.target.value }))}
                  placeholder={t('harness.pin.placeholder')}
                  className="w-32 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink outline-none placeholder:text-ink-faint"
                />
                <Button type="submit" disabled={busy || !(pinning[each.harness] ?? '').trim()}>{t('harness.pin.action')}</Button>
              </form>
            )}
            {doorRunning === each.harness && doorConsole}
          </div>
        ))}
      </Fold>

      {/* What it may do (PERM1, D72): the rules every session on this machine is handed, Daoris's defaults and the
          proposals, only where Daoris hands this agent the rules file (§5.1). */}
      {rulesShown && (
        <Fold section="rules" title={t('agents.section.rules')} summary={rulesSummary(rules!)} open={open.has('rules')} onToggle={toggle}>
          <AgentRules
            rules={rules!}
            part="agent"
            circles={[]}
            repositories={[]}
            busy={busy}
            onSwitchDefault={acts.rules.onSwitchDefault}
            onRemove={acts.rules.onRemove}
            onAdd={acts.rules.onAdd}
            onAnswer={acts.rules.onAnswer}
          />
        </Fold>
      )}

      {/* Each account's own model and effort (AGT6, D98), only where Daoris knows the tool's settings. */}
      {settingsShown && (
        <Fold section="settings" title={t('agents.section.settings')} summary={settingsSummary(tool)} open={open.has('settings')} onToggle={toggle}>
          <ul className="m-0 list-none p-0">
            {tool.accounts.filter((account) => account.settings).map((account) => (
              <li key={account.home} className="flex flex-wrap items-center gap-x-3 gap-y-1 border-t border-line py-1.5 first:border-t-0">
                <span className="min-w-0 text-body text-ink [overflow-wrap:anywhere]">{labelOf(account.name)}</span>
                <AccountSettingsSummary settings={account.settings!} />
                {tuning !== account.name && (
                  <Button variant="ghost" className="ml-auto" disabled={busy} onClick={() => setTuning(account.name)}>
                    {t('harness.settings.open')}
                  </Button>
                )}
                {tuning === account.name && (
                  <AccountSettingsForm
                    harness={door.harness}
                    account={account.name}
                    accountLabel={labelOf(account.name)}
                    settings={account.settings!}
                    choices={tool.settingsChoices!}
                    busy={settingsBusy}
                    onSave={(change) => { acts.onSaveSettings(account.name, labelOf(account.name), change); setTuning(null); }}
                    onCancel={() => setTuning(null)}
                  />
                )}
              </li>
            ))}
          </ul>
        </Fold>
      )}

      {/* What each account has carried (TOOL3, D57 §4): measured, never priced, and nothing measured said so; an agent not
          installed has carried nothing here, so it has no usage to fold (§5.1, D152 §4.6). */}
      {(tool.present || sessions > 0) && (
        <Fold section="usage" title={t('usage.title')} summary={usageSummary(sessions)} open={open.has('usage')} onToggle={toggle}>
          <Prose className="text-small">{t('usage.body')}</Prose>
          {usage.length > 0 && (
            <ul className="m-0 mt-2 list-none p-0">
              {usage.map((account) => {
                const called = account.profile ? labelOf(account.profile) : ownLabel;
                return (
                  <li key={`${account.harness}:${account.profile ?? ''}`} className="flex flex-wrap items-baseline gap-3 border-t border-line py-1.5 first:border-t-0">
                    <span className="font-mono text-small">{account.harness}</span>
                    <Chip>{called}</Chip>
                    <span className="text-small text-ink-soft">{t('usage.sessions', { count: account.sessions })}</span>
                    <Tip content={t('usage.contextTip')}>
                      <span className="ml-auto font-mono text-small text-ink-faint">{t('usage.context', { used: figure(account.used) })}</span>
                    </Tip>
                  </li>
                );
              })}
            </ul>
          )}
          <p className="m-0 mt-2 text-meta text-ink-faint">{t('usage.note')}</p>
        </Fold>
      )}

      {/* The two doors stay in sight (D50): the agent's id is said here, where the terminal types it; an agent not installed
          has one door to name, its installer. */}
      <p className="m-0 mt-8 border-t border-line pt-3 text-meta text-ink-faint">
        <Inline text={t(tool.present ? 'agents.page.terminal' : 'agents.page.terminalAbsent', { agent: tool.name })} />
      </p>
    </ViewMain>
  );
}

/**
 * A section folded to its line (D150 §1 rule 4): its name, the values it holds in a phrase, and a chevron that opens it,
 * since it opens and closes something. The section's id is what a door's part scrolls to.
 */
function Fold({ section, title, summary, open, onToggle, children }: {
  section: AgentSection;
  title: string;
  summary: string;
  open: boolean;
  onToggle: (section: AgentSection) => void;
  children: ReactNode;
}) {
  const { t } = useTranslation();
  return (
    <section id={`agents-${section}`} aria-label={title} className="mt-3 scroll-mt-3 border-t border-line pt-2">
      <button
        type="button"
        aria-expanded={open}
        aria-label={t(open ? 'agents.section.hide' : 'agents.section.show', { section: title })}
        onClick={() => onToggle(section)}
        className="flex w-full min-w-0 flex-wrap items-baseline gap-x-3 gap-y-0.5 rounded-control py-1 text-left hover:bg-accent-soft/40"
      >
        <span className="min-w-40 text-small font-semibold text-ink-soft">{title}</span>
        {!open && <span className="min-w-0 flex-1 text-small text-ink-faint [overflow-wrap:anywhere]">{summary}</span>}
        <Icon name={open ? 'chevronDown' : 'chevronRight'} size={13} className="ml-auto shrink-0 self-center text-ink-faint" />
      </button>
      {open && <div className="mt-2">{children}</div>}
    </section>
  );
}

/** The agents' main area with no page: nothing chosen says how to choose, gone says the chosen agent went, loading waits. */
export function AgentMainNotice({ state }: { state: 'none' | 'gone' | 'loading' }) {
  const { t } = useTranslation();
  return (
    <ViewMain
      state={state}
      none={{ icon: 'account', headline: t('agents.none.headline'), body: t('agents.none.body') }}
      gone={{ icon: 'account', headline: t('agents.gone.headline'), body: t('agents.gone.body') }}
    />
  );
}
