import { type ReactNode, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { type Account, type AccountSettingsChange, type Tool } from './tools';
import { type UsedBy } from './roles/roleStructure';
import { Button, Icon, Inline, Menu, type MenuAct, Tip } from '../design/ui';
import { type Answered, InlineConfirm, Refused } from '../work/InlineConfirm';
import { PageHead, ViewMain } from '../work/frame/ViewMain';
import { type AccountScope, type AgentAccounts, machineScope } from '../settings/accounts/accounts';
import { type AccountChoice, type ScopeActs } from '../settings/accounts/AccountUse';
import { type AgentRulesState, type RuleAddition, type RuleScopeName } from '../settings/repository/AgentRules';
import { AccountRow } from './AccountRow';
import { type BackAccount, RenameAccount } from './AddAccount';
import { type AccountAct, type AgentPart, type AgentUsage, accountAct, accountLabel, accountName, accountStates, accountWho, latestRead, nextStartSaid, whoApart, usageCells, versionOnly } from './agents';
import { AgentMore } from './page/AgentMore';
import { AgentDoors } from './page/AgentDoors';
import { AccountsFold } from './page/AccountsFold';

/** The sections that fold (D150 §1 rule 4): the accounts never do. */
export type AgentSection = 'use' | 'doors' | 'rules' | 'settings' | 'usage' | 'used';

/** A door's acts that run its own mechanism (D49 §4): its installer, its updater, its pin. */
export type DoorAction = 'install' | 'update' | 'unpin';

/** What the rules section presses, the screen's half of `daoris agent rules` (PERM1, D50). */
export type RuleActs = {
  onSwitchDefault: (id: string, on: boolean) => void;
  onRemove: (target: { scope: RuleScopeName; name: string | undefined; rule: string }) => void;
  onAdd: (addition: RuleAddition, added: () => void) => void;
  onAnswer: (id: string, accept: boolean) => void;
};

/** The add flow's last step answered (D152 §4.5; POOL1c, the account-pool design §4.1): the name in its field, empty being none. */
export type AddedAnswer = { name: string };

/**
 * How a question asked in the page hears how its act ended (ACCTEDIT1): `done` once it landed, and `refused` with the
 * sentence the person reads, said in the question with what they entered kept. One type, the inline confirmation's, since
 * UXFIX2 made it every inline ask's contract.
 */
export type { Answered };

/**
 * The questions that wait on their act's answer: a row's rename, the add flow's last step, the key's field and an account's
 * model and effort (ACCTUX3).
 */
export type Question = 'rename' | 'added' | 'key' | 'settings';

/** Every press on an agent's page, each the terminal's twin (D50). */
export type AgentActs = {
  /** *Read again*: this agent's accounts, one at a time; never the tool's own sign-in (D197 point 3). */
  onReadAgain: () => void;
  /** An account's own *Read*, from its row or its ⋯: that account alone (ROSTER1). */
  onReadOne: (account: string) => void;
  /** *Read now*: each account's use read again where its reading is older than a minute, no sign-in asked (SWITCH1d). */
  onReadNow: () => void;
  /** A sign-in that makes a new account (D66 §3), step 2 of *Add an account…*. */
  onSignInNew: () => void;
  /** A key added as a new account (AGT3), told back to its field (ACCTUX3): it closes once the key is kept. */
  onAddKey: (key: string, answered: Answered) => void;
  /** A new key put into the account a refused key's row names, keeping its name and lists (KEYREPLACE1b), told back to its field. */
  onReplaceKey: (account: string, key: string, answered: Answered) => void;
  /** A sign-in to the account a row names, by its id (ACCT1). */
  onSignIn: (account: string) => void;
  /** *Try now*: an account's cool-off ended early. */
  onTryNow: (account: string, label: string) => void;
  /** An account removed, from its *Remove…*'s ask, told back to it (UXFIX2). */
  onRemove: (account: string, answered: Answered) => void;
  /** An account named by the person, or its name taken back with null (ACCT2, `daoris agent profile rename`). */
  onRename: (account: string, name: string | null, answered: Answered) => void;
  /** The new account's name, at the add flow's end (POOL1c: nothing else is asked). */
  onAddedAnswer: (answer: AddedAnswer, answered: Answered) => void;
  /**
   * *Got it* on what the fold into one set changed (POOL1a, the account-pool design §6.3): the receipts the page showed, by id,
   * marked said (`ACCOUNT_USE` `said`); one written after the page drew is not among them, and stays.
   */
  onPooledSaid: (ids: string[]) => void;
  /** An account's own model and effort saved (AGT6), told back to its editor (ACCTUX3): it closes once the save lands. */
  onSaveSettings: (account: string, label: string, change: AccountSettingsChange, answered: Answered) => void;
  onDoor: (door: string, action: DoorAction) => void;
  onPin: (door: string, version: string) => void;
  scope: ScopeActs;
  rules: RuleActs;
};

/**
 * **An agent's page** (UX7b, D152 §4; first built by UX6e, D150 §5.2): its header is one line, the product, its maker, its
 * version and whether it is installed, with *Add an account…* and a ⋯; then its accounts, one row each in columns that line
 * up, each with the one act its state asks for, and your own sign-in last, outside them; then its sections, each folded to a
 * line naming its values: how accounts are used, its ways in, what it may do, model and effort, usage. Its accounts of Daoris's
 * are one set serving every workspace (POOL1c, D197; the account-pool design §4.2): no row says where it runs, and what the fold
 * into one set changed is said above them until *Got it*.
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
 * - **No sign-in is asked when it opens** (§5.3): each account says its last known state and when it was read, and *Read* or
 *   *Read again* asks on the press. Its use is read as it opens where a reading is older than a minute, and at *Read now*, by
 *   the organism (SWITCH1d; D165 point 1, which spends nothing), and each window is drawn against its scope's curve.
 * - **Explanation folds** (D152 §4.4): your own sign-in's paragraph is its row's ⓘ, the plans and terms one line that opens.
 */
export function AgentPage({
  tool, use, rules, usage, adapter, part, reading = false, readingNow = false, busy = false, settingsBusy = false, signingIn = null,
  signInPanel, signInNewPanel, added = null, doorRunning = null, doorConsole, acts, onOpenPlugin, onAnchored, now, usedBy = null,
}: {
  tool: Tool;
  /**
   * The roles that lean on this agent on this machine, each at its place in the order (COMPANYVIEW1d, *Used by*); null on a browser or a
   * shell older than the structure's read, and the section is absent. A read that failed is said, never an empty list.
   */
  usedBy?: { read: 'ok' | 'loading' | 'failed'; rows: UsedBy[] } | null;
  /** How its accounts are used (TOOL4g): null from a shell older than that, and the sections it feeds are absent. */
  use: AgentAccounts | null;
  /** The rules file's answer (PERM1): null where it is not read, and *What it may do* is absent. */
  rules: AgentRulesState | null;
  /** What each of its accounts has carried (TOOL3), by its doors; null while it is not answered yet, never nothing measured (PUSHUX1). */
  usage: AgentUsage[] | null;
  /** The adapter driven work starts on (`driver.json`'s). */
  adapter?: string;
  /** The part a door named, opened and brought into view. */
  part?: AgentPart | null;
  /** *Read again* is on its way. */
  reading?: boolean;
  /** *Read now*, or the read as the page opened, is on its way (SWITCH1d). */
  readingNow?: boolean;
  /** An act on the agent is on its way: one at a time, by construction. */
  busy?: boolean;
  settingsBusy?: boolean;
  /** The account a sign-in is running for, whose row holds `signInPanel`. */
  signingIn?: string | null;
  signInPanel?: ReactNode;
  /** A sign-in to a new account, running: the add flow's step 2, under the header. */
  signInNewPanel?: ReactNode;
  /**
   * The new account a sign-in or a key added kept, by its id, who signed in and, for a key, its handle, waiting on the add
   * flow's step 3, its name: a key added asks the same question (the UX7 design §4.5, ACCTUX3).
   */
  added?: { account: string; who: string | null; key?: string | null; offered?: string | null } | null;
  /** The door whose action runs, whose block holds `doorConsole`. */
  doorRunning?: string | null;
  doorConsole?: ReactNode;
  acts: AgentActs;
  /** Opens the Plugins place at a plugin (PLUGUI1c2b): a door's *declared by plugin* is a door to it; without it, text. */
  onOpenPlugin?: (plugin: string) => void;
  /** Told once the part a door named is in view. */
  onAnchored?: () => void;
  /** The moment the spans are measured to; now, unless a story's. */
  now?: Date;
}) {
  const { t } = useTranslation();
  const door = tool.doors[0]!;
  const signsIn = door.signsIn !== false;
  const product = tool.product ?? tool.name;
  // The set's one scope, every workspace's (POOL1a2, D197): its order, how it is used and its next start.
  const machine: AccountScope | null = use ? machineScope(use) : null;
  const states = accountStates(tool, use);
  // When any of its accounts was last read, said once at the head of the list beside *Read again*; your own sign-in is never
  // read (D197 point 3), so a reading of it kept from before is not among them.
  const latest = latestRead([...states.values()]);
  const ownLabel = t('agents.account.own');
  const accountOf = (name: string) => tool.accounts.find((each) => each.name === name);
  // An account on one line, told apart from another sign-in of its email (ACCOUNTID1b).
  const labelOf = (name: string) => {
    const account = accountOf(name);
    return account ? accountLabel(account, tool) : name;
  };
  const choices: AccountChoice[] = tool.accounts.map((account) => ({
    name: account.name, label: labelOf(account.name), login: account.login, keyed: Boolean(account.key),
  }));
  // Where an account stands in the set's order (D197 point 2), for the lists the page draws in it; one the order does not
  // name yet goes after, as `Change` would place it.
  const order = machine?.list ?? [];
  const place = (name: string) => (order.includes(name) ? order.indexOf(name) : order.length);
  const sessions = (usage ?? []).reduce((sum, account) => sum + account.sessions, 0);
  const rulesShown = tool.takesRules && rules !== null;
  const settingsShown = Boolean(tool.settingsChoices) && tool.accounts.some((account) => account.settings);
  // The set's next start, in any workspace, said above the accounts (ACCTUX4b; POOL1c: one set, so no workspace is named).
  // None where a test double runs in its own home, nor from a shell that names none.
  const nextSaid = nextStartSaid(machine, labelOf, (name) => Boolean(accountOf(name)?.key));
  // What the fold into one set changed (POOL1a, the account-pool design §6.3): each receipt still unsaid, said above the rows
  // until *Got it*, which marks those the page showed, by id.
  const pooled = Array.isArray(use?.pooled) ? use.pooled : [];

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
  const [renaming, setRenaming] = useState<string | null>(null);
  const [removing, setRemoving] = useState<string | null>(null);
  const [tuning, setTuning] = useState<string | null>(null);
  const [keying, setKeying] = useState(false);
  // KEYREPLACE1b: the account whose refused key a new one is being typed for; the one key draft serves it and the header's field.
  const [replacing, setReplacing] = useState<string | null>(null);
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
  const askRename = (account: string | null) => {
    setRefused(null);
    setRenaming(account);
  };
  // The key's field and the model and effort editor ask so too (ACCTUX3): opened or put down, the last refusal goes.
  const askKey = (open: boolean) => {
    setRefused(null);
    setKeying(open);
    setReplacing(null);
    if (!open) setKeyDraft('');
  };
  const askReplace = (account: string | null) => {
    setRefused(null);
    setKeying(false);
    setReplacing(account);
    setKeyDraft('');
  };
  const askSettings = (account: string | null) => {
    setRefused(null);
    setTuning(account);
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

  // ACCTUX3: the field closes and forgets the key once it is kept, never on the press, so a refused key stays to be corrected
  // with why said under it (ACCTEDIT1's contract); kept, the add flow's last step asks its name.
  const addKey = () => {
    const key = keyDraft.trim();
    if (!key) return;
    acts.onAddKey(key, answered('key', '', () => {
      setKeying(false);
      setKeyDraft('');
    }));
  };

  // KEYREPLACE1b: the same discipline for a replacement: closed and forgotten once kept, a refusal said in the row's field.
  const replaceKey = (account: string) => {
    const key = keyDraft.trim();
    if (!key) return;
    acts.onReplaceKey(account, key, answered('key', account, () => {
      setReplacing((was) => (was === account ? null : was));
      setKeyDraft('');
    }));
  };

  // *Add an account…*'s first step (D152 §4.5; the account-pool design §4.1): the accounts a person signing one back in means,
  // each signed out or unknown, in the set's order. With none, it starts the sign-in at once.
  const back: BackAccount[] = tool.accounts
    .filter((account) => !account.key && ['out', 'unknown'].includes(states.get(account.name)!.state))
    .sort((a, b) => place(a.name) - place(b.name))
    .map((account) => ({ id: account.name, name: accountLabel(account, tool), state: states.get(account.name)! }));
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
              {/* While the agent has no Daoris account the press is the first sign-in, which names no account (D175 point 6). */}
              {t(tool.accounts.length === 0 ? 'harness.login.forDaoris' : 'agents.add.open')}
            </Button>
          )}
          {tool.present && door.takesKey && !keying && (
            <Button variant="ghost" disabled={busy} onClick={() => askKey(true)}>
              <Icon name="account" size={13} />
              {t('harness.profile.addKey')}
            </Button>
          )}
          {/* An agent not installed heads with its own installer (D152 §4.6), the same one *Ways in* runs. */}
          {!tool.present && door.installs !== false && (
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

  // An account's ⋯ (D152 §4.2; the account-pool design §4.2): its model and effort, *Rename…*, *Sign in again*, *Read again*
  // and *Remove…*. A default, and a list to add it to or take it from, went with POOL1c: every account serves every workspace,
  // and the order says where starts begin (*How accounts are used*).
  const accountMenu = (account: Account, state: { state: string }): MenuAct[] => {
    const name = account.name;
    return [
      ...(settingsShown && account.settings && tool.settingsChoices
        ? [{ id: 'settings', label: t('agents.account.settings'), onSelect: () => { setOpen((was) => new Set([...was, 'settings' as const])); askSettings(name); } }]
        : []),
      { id: 'rename', label: t('agents.account.rename'), disabled: busy, onSelect: () => askRename(name) },
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
  const actOf = (act: AccountAct | null, id: string, label: string) => {
    if (!act) return undefined;
    switch (act.act) {
      case 'signIn':
        return {
          label: t('harness.login.action'), ariaLabel: t('agents.act.signInTo', { account: label }), loud: act.loud,
          onPress: () => acts.onSignIn(id), disabled: busy,
        };
      case 'read':
        return {
          label: t('agents.act.read'), ariaLabel: t('agents.act.readFor', { account: label }), loud: act.loud,
          onPress: () => acts.onReadOne(id), disabled: reading || busy,
        };
      case 'tryNow':
        return {
          label: t('harness.cooling.tryNow'), ariaLabel: t('harness.cooling.tryNowFor', { account: label }),
          onPress: () => acts.onTryNow(id, label), disabled: busy,
        };
      // A key its provider refused (ACCTUX1): a key field on its own row, which puts the new key into the same account
      // (`key-replace`, KEYREPLACE1b), keeping its name and its place in the order.
      case 'newKey':
        return {
          label: t('agents.act.newKey'), ariaLabel: t('agents.act.newKeyFor', { account: label }), loud: act.loud,
          onPress: () => askReplace(id), disabled: busy,
        };
    }
  };

  const accountRow = (account: Account) => {
    const name = accountName(account);
    // Where a name reads as another sign-in's, the line its acts and asks name it by (ACCOUNTID1b).
    const label = accountLabel(account, tool);
    const state = states.get(account.name)!;
    const facts = use?.accounts.find((each) => each.name === account.name);
    const offered = accountAct(state, { signsIn: signsIn && !account.key, present: tool.present, takesKey: Boolean(door.takesKey) });
    // What its agent last said, and since when it is offered again, under that row alone; a cool-off's column says its hold's
    // end, so each reset the agent reported is said in its window's cell, and one Daoris chose the length of says the reset is
    // unknown (ACCTUX1). The session's and the week's cells are on every sign-in whose agent speaks, unknown until read
    // (ACCTUX4); a key's are drawn only where it said something, since a key has no window its agent was heard to keep.
    // Each window against the curve of the list it is measured by, as .NET judged it beside the reading (PUSH1e), and where its
    // reading came from (SWITCH1d).
    const usage = usageCells(facts, state, { fixed: Boolean(use?.speaks) && !account.key, now, product });
    return (
      <AccountRow
        key={account.home}
        name={name}
        label={label}
        who={accountWho(account, tool)}
        id={account.name}
        home={account.home}
        state={state}
        current={typeof facts?.running === 'number' && facts.running > 0 ? t('agents.now.sessions', { count: facts.running }) : null}
        usage={usage}
        act={actOf(offered, account.name, label)}
        menu={accountMenu(account, state)}
        now={now}
      >
        {/* The one inline confirmation (UXFIX2): open until the removal answers, a refusal said inside it. */}
        {removing === account.name && (
          <InlineConfirm
            label={t('harness.profile.removeTitle', { account: label })}
            says={t('harness.profile.removeConfirm')}
            meanIt={t('harness.profile.removeMeanIt')}
            busy={busy}
            onConfirm={(answered) => acts.onRemove(account.name, answered)}
            onClose={() => setRemoving((was) => (was === account.name ? null : was))}
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
            onCancel={() => askRename(null)}
          />
        )}
        {replacing === account.name && (
          <form className="flex flex-wrap items-center gap-2" onSubmit={(event) => { event.preventDefault(); replaceKey(account.name); }}>
            <input
              autoFocus
              type="password"
              autoComplete="off"
              spellCheck={false}
              value={keyDraft}
              onChange={(event) => setKeyDraft(event.target.value)}
              aria-label={t('agents.key.replaceLabel', { account: label })}
              placeholder={t('harness.profile.keyPlaceholder')}
              className="min-w-72 flex-1 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink outline-none placeholder:text-ink-faint"
            />
            <Button type="submit" variant="primary" disabled={busy || !keyDraft.trim()}>{t('harness.profile.keySave')}</Button>
            <Button variant="ghost" disabled={busy} onClick={() => askReplace(null)}>{t('common.cancel')}</Button>
            {refusalOf('key', account.name) && <Refused sentence={refusalOf('key', account.name)!} />}
            <span className="basis-full text-meta text-ink-faint">{t('harness.profile.keyHint')}</span>
          </form>
        )}
        {signingIn === account.name && signInPanel}
      </AccountRow>
    );
  };

  // The add flow's step 3 offers the name the person gave at the sign-in, else the name the sign-in's end offered (ACCOUNTID1b:
  // who signed in, or with the organisation beside it where another account has the email, by the driver's one rule), else
  // none; an older shell offered none, so who signed in. It says what it is called left empty, which is the row's own namer's
  // answer with no name given (ACCTUX1), and who signed in told apart from another sign-in of the email.
  const addedAccount = added ? accountOf(added.account) : undefined;
  const offeredName = addedAccount?.displayName?.trim()
    || (added && added.offered !== undefined ? added.offered : added?.who) || '';
  const addedWho = (addedAccount ? whoApart(addedAccount, tool) : null) ?? added?.who ?? null;
  const addedFallback = added
    ? accountName({ name: added.account, key: added.key ?? addedAccount?.key, account: added.who ?? addedAccount?.account })
    : '';
  // A key added names no one signed in (AGT3): its step says the key was added, under the header's own *Add an API key*.
  const addedKey = added?.key ?? null;
  const adding = (goingBack && !signInNewPanel && !added) || Boolean(signInNewPanel) || Boolean(added);

  return (
    <ViewMain header={header}>
      {/* *Add an account…* (D152 §4.5), under the header where it was started: step 1, the sign-in, or step 3. */}
      <AccountsFold adding={adding} goingBack={goingBack} signInNewPanel={signInNewPanel} added={added} back={back} busy={busy} now={now} setGoingBack={setGoingBack} acts={acts} tool={tool} t={t} addedKey={addedKey} addedWho={addedWho} offeredName={offeredName} addedFallback={addedFallback} refusalOf={refusalOf} answered={answered} latest={latest} use={use} product={product} readingNow={readingNow} reading={reading} pooled={pooled} labelOf={labelOf} nextSaid={nextSaid} accountRow={accountRow} ownLabel={ownLabel} termsOpen={termsOpen} setTermsOpen={setTermsOpen} keying={keying} addKey={addKey} keyDraft={keyDraft} setKeyDraft={setKeyDraft} askKey={askKey} machine={machine} open={open} toggle={toggle} choices={choices} />
      <AgentDoors t={t} tool={tool} adapter={adapter} open={open} toggle={toggle} onOpenPlugin={onOpenPlugin} busy={busy} acts={acts} pinOpen={pinOpen} setPinOpen={setPinOpen} pinning={pinning} setPinning={setPinning} doorRunning={doorRunning} doorConsole={doorConsole} />
      <AgentMore rulesShown={rulesShown} t={t} rules={rules} open={open} toggle={toggle} busy={busy} acts={acts} settingsShown={settingsShown} tool={tool} labelOf={labelOf} tuning={tuning} askSettings={askSettings} door={door} settingsBusy={settingsBusy} answered={answered} setTuning={setTuning} refusalOf={refusalOf} sessions={sessions} usage={usage} ownLabel={ownLabel} usedBy={usedBy} product={product} />
      <p className="m-0 mt-8 border-t border-line pt-3 text-meta text-ink-faint">
        {/* An agent Daoris has no installer for is installed with its maker's own tooling, so no `daoris agent install` (INSTALLDOOR1). */}
        <Inline text={t(
          tool.present ? 'agents.page.terminal'
            : door.installs === false ? (tool.maker ? 'agents.page.installedByMaker' : 'agents.page.installedByMakerUnnamed')
              : 'agents.page.terminalAbsent',
          { agent: tool.name, product, maker: tool.maker },
        )} />
      </p>
    </ViewMain>
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
