import { type ReactNode, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { clockOf, figure, list } from '../format';
import type { AccountSettingsChange, Tool } from '../tools';
import {
  Button, Chip, Icon, Inline, type MenuAct, Pill, Prose, SectionTitle, Tip,
} from '../ui';
import { PageHead, ViewMain } from '../work/ViewMain';
import { type AccountScope, type AgentAccounts, listedIn, machineScope, workspaceScope } from '../settings/accounts';
import { AccountSettingsForm, AccountSettingsSummary } from '../settings/AccountSettings';
import {
  type AccountChoice, AccountFactsLines, OwnSignInLine, type ScopeActs, ScopeEditor, TermsLine, WorkspaceScope,
} from '../settings/AccountUse';
import { AgentRules, type AgentRulesState, type RuleAddition, type RuleScopeName } from '../settings/AgentRules';
import { AccountRow } from './AccountRow';
import {
  type AccountState, type AgentPart, type AgentUsage, accountStates, doorsSummary, latestRead, ownState, rulesSummary,
  settingsSummary, sharesOwn, usageSummary, useSummary, workspacesSummary,
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

/** Every press on an agent's page, each the terminal's twin (D50). */
export type AgentActs = {
  /** *Read again*: this agent's accounts, one at a time, and the tool's own sign-in (§5.3). */
  onReadAgain: () => void;
  onSignInNew: () => void;
  onAddKey: (key: string) => void;
  onSignIn: (account: string) => void;
  /** *Try now*: an account's cool-off ended early, or the tool's own sign-in's with null. */
  onTryNow: (account: string | null, label: string) => void;
  /** A default set (an account) or cleared (null): the machine's, or a workspace's. */
  onDefault: (account: string | null, workspace?: string) => void;
  onRemove: (account: string) => void;
  onSaveSettings: (account: string, label: string, change: AccountSettingsChange) => void;
  onDoor: (door: string, action: DoorAction) => void;
  onPin: (door: string, version: string) => void;
  scope: ScopeActs;
  rules: RuleActs;
};

/**
 * **An agent's page** (UX6e, D150 §5.2): its header names the product, its maker and its version, whether it is installed,
 * and *Sign in to another account…*; then its accounts, one list whatever doors reach it, and its sections, each folded to
 * a line naming its values: how accounts are used, its workspaces, its ways in, what it may do, model and effort, usage.
 *
 * @remarks
 * **A molecule**: every state arrives as props, the panels a press opens arrive as nodes (a sign-in's, a door's console),
 * and every press goes out. It holds only what the person has open: a section, *Remove…*'s ask, a form.
 *
 * - **A section appears only where the agent has that concept** (§5.1): what it may do only where Daoris hands its agent
 *   the rules file, model and effort only where Daoris knows the tool's settings, how accounts are used only where it has
 *   accounts of Daoris's own. Adding an agent adds a row to the list and this page to it, never a screen.
 * - **A door is a property, never a second roster** (D53, D57 §c): *Ways in* lists each door and its version; the accounts
 *   are the agent's.
 * - **Nothing is read when it opens** (§5.3): each account says its last known state and when it was read, and *Read
 *   again* asks on the press.
 */
export function AgentPage({
  tool, use, rules, usage, workspaces, adapter, part, reading = false, busy = false, settingsBusy = false, signingIn = null,
  signInPanel, signInNewPanel, doorRunning = null, doorConsole, acts, onAnchored, now,
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
  /** A sign-in to another account, running, under the accounts. */
  signInNewPanel?: ReactNode;
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
  const ownLabel = tool.ownAccount ?? t('agents.account.own');
  const labelOf = (name: string) => {
    const account = tool.accounts.find((each) => each.name === name);
    return account?.account ?? (account?.key ? t('harness.profile.keyName', { handle: account.key }) : name);
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

  // What the person has open: a section, *Remove…*'s ask, the model and effort form, a key's field, a pin's version.
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
  const [removing, setRemoving] = useState<string | null>(null);
  const [tuning, setTuning] = useState<string | null>(null);
  const [keying, setKeying] = useState(false);
  const [keyDraft, setKeyDraft] = useState('');
  const [pinning, setPinning] = useState<Record<string, string>>({});
  const [pinOpen, setPinOpen] = useState<string | null>(null);
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

  // The workspaces that may run on an account (D130 §3.2): this machine where its default or list names it, then each
  // workspace whose default is it or whose own list holds it.
  const mayRun = (name: string) => [
    ...(tool.machineDefault === name || (machine?.list.includes(name) ?? false) ? [t('harness.use.machine')] : []),
    ...new Set([
      ...tool.workspaceDefaults.filter((circle) => circle.profile === name).map((circle) => circle.workspace),
      ...(use ? listedIn(use, name) : []),
    ]),
  ];

  const header = (
    <PageHead
      title={product}
      version={tool.doors.find((each) => each.present)?.version ?? undefined}
      pills={tool.present ? <Pill tone="neutral">{t('harness.installed')}</Pill> : <Pill tone="neutral">{t('harness.absent')}</Pill>}
      id={tool.name}
      line={tool.maker ?? undefined}
      acts={tool.present && (signsIn || door.takesKey) ? (
        <>
          {signsIn && (
            <Button disabled={busy} onClick={acts.onSignInNew}>
              <Icon name="plus" size={13} />
              {t('harness.profile.signInNew')}
            </Button>
          )}
          {door.takesKey && !keying && (
            <Button variant="ghost" disabled={busy} onClick={() => setKeying(true)}>
              <Icon name="account" size={13} />
              {t('harness.profile.addKey')}
            </Button>
          )}
        </>
      ) : undefined}
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

  const accountMenu = (name: string, state: AccountState): MenuAct[] => {
    const account = tool.accounts.find((each) => each.name === name)!;
    return [
      ...(tool.machineDefault !== name && mayDefault(name)
        ? [{ id: 'default', label: t('agents.account.useDefault'), disabled: busy, onSelect: () => acts.onDefault(name) }] : []),
      ...workspaces.filter((workspace) => mayDefault(name, workspace)
        && !tool.workspaceDefaults.some((circle) => circle.workspace === workspace && circle.profile === name))
        .map((workspace) => ({
          id: `default:${workspace}`, label: t('agents.account.useDefaultIn', { workspace }), disabled: busy,
          onSelect: () => acts.onDefault(name, workspace),
        })),
      ...(settingsShown && account.settings && tool.settingsChoices
        ? [{ id: 'settings', label: t('agents.account.settings'), onSelect: () => { setOpen((was) => new Set([...was, 'settings' as const])); setTuning(name); } }]
        : []),
      ...(signsIn && !account.key && state.state !== 'out'
        ? [{ id: 'signIn', label: t('harness.login.again'), disabled: busy || !tool.present, onSelect: () => acts.onSignIn(name) }]
        : []),
      { id: 'remove', label: t('agents.account.remove'), icon: 'remove' as const, disabled: busy, onSelect: () => setRemoving(name) },
    ];
  };

  const factsLine = (name: string) => {
    const facts = use?.accounts.find((each) => each.name === name);
    return facts ? <AccountFactsLines facts={{ ...facts, cooling: null }} label={labelOf(name)} busy={busy} onTryNow={() => {}} now={now} /> : null;
  };

  return (
    <ViewMain header={header}>
      {/* The accounts: one list, whatever doors reach the agent (D150 §5.2, TOOL6g). Never folded. */}
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
        <ul className="m-0 list-none p-0">
          {/* An agent not installed has no sign-in of its own to say anything of. */}
          {tool.present && <AccountRow
            label={ownLabel}
            state={own}
            chip={tool.machineDefault === null && tool.present ? t('harness.profile.sessionsUse') : undefined}
            lines={[t('harness.ownHome')]}
            act={own.state === 'cooling' ? {
              label: t('harness.cooling.tryNow'), ariaLabel: t('harness.cooling.tryNowFor', { account: ownLabel }),
              onPress: () => acts.onTryNow(null, ownLabel), disabled: busy,
            } : undefined}
            menu={ownMenu}
            now={now}
          />}
          {tool.accounts.map((account) => {
            const label = labelOf(account.name);
            const state = states.get(account.name)!;
            const workspacesHere = mayRun(account.name);
            return (
              <AccountRow
                key={account.home}
                label={label}
                name={account.name}
                home={account.home}
                state={state}
                chip={tool.machineDefault === account.name ? t('harness.profile.sessionsUse') : undefined}
                lines={[workspacesHere.length > 0 ? t('agents.account.mayRun', { workspaces: list(workspacesHere) }) : '']}
                act={state.state === 'out' && signsIn && !account.key ? {
                  label: t('harness.login.action'), onPress: () => acts.onSignIn(account.name), disabled: busy || !tool.present,
                  loud: state.holdsWork,
                } : state.state === 'cooling' ? {
                  label: t('harness.cooling.tryNow'), ariaLabel: t('harness.cooling.tryNowFor', { account: label }),
                  onPress: () => acts.onTryNow(account.name, label), disabled: busy,
                } : undefined}
                menu={accountMenu(account.name, state)}
                now={now}
              >
                <span className="basis-full">{factsLine(account.name)}</span>
                {removing === account.name && (
                  <div
                    role="group"
                    aria-label={t('harness.profile.removeTitle', { account: label })}
                    className="flex basis-full flex-wrap items-center gap-2 rounded-control border border-line bg-sunken px-2.5 py-2"
                  >
                    <span className="min-w-0 flex-1 basis-64 text-small text-ink-soft">{t('harness.profile.removeConfirm')}</span>
                    <Button variant="danger" disabled={busy} onClick={() => { setRemoving(null); acts.onRemove(account.name); }}>
                      {t('harness.profile.removeMeanIt')}
                    </Button>
                    <Button variant="ghost" onClick={() => setRemoving(null)}>{t('common.cancel')}</Button>
                  </div>
                )}
                {signingIn === account.name && <div className="basis-full">{signInPanel}</div>}
              </AccountRow>
            );
          })}
        </ul>
        {/* While a start would run on the tool's own sign-in, the page says so, and what giving Daoris accounts of its own
            does (D125 §3.7). */}
        {sharesOwn(tool, use) && (
          <OwnSignInLine who={tool.ownAccount} cooling={use?.own.cooling} signsIn={signsIn} busy={busy || !tool.present} onSignIn={acts.onSignInNew} />
        )}
        {tool.accounts.length > 0 && <TermsLine />}
        {signInNewPanel}
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

      {/* How the accounts are used (TOOL4g; D130 §3.2, §16.6): this machine's list and settings. */}
      {use && machine && tool.accounts.length > 0 && (
        <Fold section="use" title={t('harness.use.title')} summary={useSummary(machine, labelOf)} open={open.has('use')} onToggle={toggle}>
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
                ? <span className="font-mono text-small text-ink-faint">{each.version}</span>
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

      {/* What each account has carried (TOOL3, D57 §4): measured, never priced, and nothing measured said so. */}
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

      {/* The two doors stay in sight (D50). */}
      <p className="m-0 mt-8 border-t border-line pt-3 text-meta text-ink-faint">
        <Inline text={t('agents.page.terminal', { agent: tool.name })} />
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
