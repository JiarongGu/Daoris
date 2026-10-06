import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useHarnessRun, WithHarnessRuns } from '../harnessRuns';
import { useRegistry } from '../queries';
import { SessionConsole } from '../SessionConsole';
import {
  useAccountUse, useAccounts, useHarnessAction, useHarnesses, useRefreshHarnesses, useRuleAction, useRuleProposal, useRules,
  useSetAgentSettings, useUsage,
} from '../shell';
import { SignIn } from '../SignIn';
import { byTool, type Tool } from '../tools';
import { failure, type Notify, useErrorNotify } from '../ui';
import type { ViewLayout } from '../work/ViewFrame';
import { workspacesOf } from '../workspaces';
import { agentOf } from '../settings/accounts';
import type { ScopeActs } from '../settings/AccountUse';
import { proposalChange } from '../settings/proposals';
import { AgentList, AgentStrip } from './AgentList';
import { type AgentActs, AgentMainNotice, AgentPage } from './AgentPage';
import { type AgentPart, accountName, agentRows, runsForLine, signedOutHeld } from './agents';

/** What a door runs, and so what streams under it in *Ways in* (D49 §2). */
const DOOR_ACTIONS = ['install', 'update', 'pin', 'unpin'] as const;

/**
 * **The Agents place** (UX6e, D150 §5): what it hands the frame (D118 §5), its list pane and its main area. The list is
 * each agent this machine knows, once; the main area is the chosen agent's page.
 *
 * @remarks
 * **A hook, because a view hands the frame a value**, as the Plugins view does. The application holds it on every view.
 *
 * 🔴 **Opening it starts no process and asks no account** (§5.3). It reads the roster the frame already holds (asked once,
 * and kept: `staleTime: Infinity`) and the accounts' files (`ACCOUNTS`, a file read the tick refreshes); each account says
 * its last known state with when it was read. Only *Read again* asks, on the press, one agent's accounts one at a time.
 *
 * **Every act has its terminal twin** (D50): `daoris agent …`, named at the page's foot.
 */
export function useAgentsView({ active, chosen, onChoose, notify, part = null, onAnchored }: {
  /** The view is in front. */
  active: boolean;
  /** The list's chosen item: an agent's id, which the application remembers (`daoris.list.agents.chosen`). */
  chosen: string | null;
  onChoose: (item: string | null) => void;
  notify: Notify;
  /** The part of an agent's page a door named (D150 §2.4), opened and brought into view. */
  part?: AgentPart | null;
  /** Told once the part is open, so a later visit opens at the page's top again. */
  onAnchored?: () => void;
}): ViewLayout {
  const { t } = useTranslation();
  const roster = useHarnesses();
  const accounts = useAccounts();
  useErrorNotify(active ? roster.error : null, notify);

  const harnesses = Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : null;
  const tools = harnesses ? byTool(harnesses) : [];
  // Every agent this build knows, installed or not (AGENTS2): a fold for the ones not installed hid Codex.
  const rows = agentRows(tools, accounts.data);
  const chosenTool = tools.find((tool) => tool.name === chosen) ?? null;
  const adapter = roster.data?.adapter;
  // A row's Install (AGENTS2) opens its agent and asks the page to run the installer there, where the application's running
  // action is held and its console streams under *Ways in*; this view's hook sits outside that holder.
  const [installing, setInstalling] = useState<string | null>(null);
  const install = (agent: string) => {
    onChoose(agent);
    setInstalling(agent);
  };

  // A door that names a part and no agent opens the agent that has it (D150 §2.4): what agents may do is the agent's that
  // Daoris hands the rules file; usage and accounts the agent driven work starts on, else the first installed.
  useEffect(() => {
    if (!active || !part || !harnesses) return;
    if (part === 'rules' && !chosenTool?.takesRules) {
      const holder = tools.find((tool) => tool.takesRules);
      if (holder) onChoose(holder.name);
    } else if (part !== 'rules' && !chosenTool) {
      const driving = tools.find((tool) => tool.doors.some((door) => door.harness === adapter)) ?? tools.find((tool) => tool.present);
      if (driving) onChoose(driving.name);
    }
  }, [active, part, harnesses, chosenTool, tools, adapter, onChoose]);

  // A first answer still on its way; an answer that is no roster (a shell older than this place) lists nothing.
  const loading = active && roster.data === undefined && roster.isPending;
  return {
    list: {
      view: 'agents',
      name: t('nav.agents'),
      labels: { open: t('agents.list.open'), close: t('agents.list.close'), resize: t('agents.list.resize') },
      strip: harnesses ? <AgentStrip rows={rows} chosen={chosen} onChoose={onChoose} /> : undefined,
      loading,
      empty: harnesses && rows.length === 0 ? { headline: t('agents.empty.headline'), body: t('agents.empty.body') } : undefined,
      chosen,
      // A remembered agent reopens only while this machine still knows it (UX6b): one gone opens nothing chosen.
      standing: !chosen ? undefined : !harnesses ? 'unread' : chosenTool ? 'live' : 'gone',
      body: <AgentList rows={rows} chosen={chosen} onChoose={onChoose} onInstall={install} />,
    },
    main: (
      <WithHarnessRuns notify={notify}>
        {loading
          ? <AgentMainNotice state="loading" />
          : chosenTool
            ? (
              <AgentsMain
                key={chosenTool.name}
                tool={chosenTool}
                adapter={adapter}
                notify={notify}
                part={part}
                onAnchored={onAnchored}
                install={installing === chosenTool.name}
                onInstallTaken={() => setInstalling(null)}
              />
            )
            : <AgentMainNotice state={chosen && harnesses ? 'gone' : 'none'} />}
      </WithHarnessRuns>
    ),
  };
}

/**
 * The activity bar's badge for Agents (D150 §2.1): the accounts a list or a default holds that read signed out, the tool's
 * own sign-in among them while the starts run on it. From the roster the frame holds and the accounts' files; nothing asked.
 */
export function useAgentsWaiting(): number {
  const roster = useHarnesses();
  const accounts = useAccounts();
  const harnesses = Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : [];
  return byTool(harnesses).reduce((sum, tool) => sum + (tool.present ? signedOutHeld(tool, agentOf(accounts.data, tool.name)) : 0), 0);
}

/**
 * The chosen agent's page and every act on it (UX6e): the organism under the page, inside the application's running
 * action (SIGNIN1), so a sign-in started here outlives leaving the place and its end is said wherever the person is.
 */
function AgentsMain({ tool, adapter, notify, part, onAnchored, install = false, onInstallTaken }: {
  tool: Tool;
  adapter?: string;
  notify: Notify;
  part?: AgentPart | null;
  onAnchored?: () => void;
  /** The list's Install was pressed on this agent (AGENTS2): its installer runs here, once. */
  install?: boolean;
  /** Told as the install is taken, so the request is spent. */
  onInstallTaken?: () => void;
}) {
  const { t } = useTranslation();
  const read = useRefreshHarnesses();
  const act = useHarnessAction();
  const tune = useSetAgentSettings();
  const usage = useUsage();
  const accounts = useAccounts();
  const accountUse = useAccountUse();
  const rules = useRules();
  const ruleAct = useRuleAction();
  const settle = useRuleProposal();
  const registry = useRegistry('machine');
  const { running, runningProfile, signingInNew, busy: acting, run, added, settleAdded } = useHarnessRun();
  const onError = failure(notify);
  const door = tool.doors[0]!.harness;
  const product = tool.product ?? tool.name;
  const use = agentOf(accounts.data, tool.name);
  const busy = acting || act.isPending || accountUse.isPending || ruleAct.isPending || settle.isPending;
  // The new account this agent's sign-in kept, waiting on its name and its lists (UX7b): the add flow's step 3.
  const addedHere = added && added.harness === door ? added : null;

  // The list's Install (AGENTS2): the account-owning door's installer, as the first Install under *Ways in* runs it. Taken
  // once, by a ref, since a development build runs an effect twice and the run's own guard learns of the first only later.
  const installTaken = useRef(false);
  useEffect(() => {
    if (!install) {
      installTaken.current = false;
      return;
    }
    if (installTaken.current) return;
    installTaken.current = true;
    onInstallTaken?.();
    if (!tool.present) run(door, 'install');
  }, [install, onInstallTaken, tool.present, run, door]);

  const labelOf = (name: string | null) => {
    if (!name) return t('agents.account.own');
    const account = tool.accounts.find((each) => each.name === name);
    return account ? accountName(account) : name;
  };

  // ACCT2's rename and ACCT1's join, each the terminal's `daoris agent profile rename|join` (D50), said as they land.
  const rename = async (account: string, name: string | null) => {
    // Naming an account its own id gives it no name of its own (ACCT2): the one way to clear it at the door.
    const answer = await act.mutateAsync({ harness: door, action: 'profile-rename', profile: account, name: name ?? account });
    notify(answer.name
      ? t('agents.renamed', { id: account, name: answer.name })
      : t('agents.unnamed', { id: account }));
    return answer.name ?? null;
  };
  const join = async (account: string, lists: (string | null)[], called?: string) => {
    const answer = await act.mutateAsync({ harness: door, action: 'profile-join', profile: account, join: lists });
    const places = (answer.places ?? []).map((place) => ({ workspace: place.workspace ?? null, first: place.default }));
    notify(t('agents.joined', { account: called ?? labelOf(account), places: runsForLine(places) }));
  };

  const scope: ScopeActs = {
    onOrder: (workspace, list) => accountUse.mutate({ harness: door, action: 'order', accounts: list, ...(workspace ? { workspace } : {}) }, { onError }),
    onUse: (workspace, change) => accountUse.mutate({ harness: door, action: 'use', ...(workspace ? { workspace } : {}), ...change }, { onError }),
    onInherit: (workspace) => accountUse.mutate({ harness: door, action: 'inherit', workspace }, { onError }),
  };
  const where = (scopeName: string, name: string | undefined) => scopeName === 'machine'
    ? t('settings.rules.proposals.everywhere')
    : t(scopeName === 'workspace' ? 'settings.rules.where.workspace' : 'settings.rules.where.repository', { name: name ?? '' });

  const acts: AgentActs = {
    onReadAgain: () => read.mutate({ agent: tool.name }, { onError }),
    onReadOne: (account) => read.mutate({ agent: tool.name, profile: account }, { onError }),
    onSignInNew: () => run(door, 'login-new'),
    onAddKey: (key) => act.mutate({ harness: door, action: 'key-add', key }, {
      onSuccess: (result) => notify(t('harness.profile.keyAdded', { profile: result.profile, handle: result.key })),
      onError,
    }),
    onSignIn: (account) => run(door, 'login', account),
    onTryNow: (account, label) => accountUse.mutate(
      { harness: door, action: 'ready', ...(account ? { profile: account } : { own: true }) },
      { onSuccess: (answer) => notify(t(answer.ended ? 'harness.cooling.ready' : 'harness.cooling.notCooling', { account: label })), onError },
    ),
    onDefault: (account, workspace) => run(door, 'profile-default', account ?? undefined, undefined, workspace),
    onRemove: (account) => run(door, 'profile-remove', account),
    onRename: (account, name) => { rename(account, name).catch(onError); },
    onJoin: (account, lists) => { join(account, lists).catch(onError); },
    // The add flow's end (D152 §4.5): its name kept where the field holds one other than its id, then the lists joined;
    // either refused leaves the question up, so nothing the person typed is lost.
    onAddedAnswer: ({ name, join: lists }) => {
      if (!addedHere) return;
      const account = addedHere.profile;
      const current = tool.accounts.find((each) => each.name === account)?.displayName?.trim() || null;
      const wanted = name?.trim() || null;
      void (async () => {
        const called = wanted !== current && (wanted !== null || current !== null) ? await rename(account, wanted) : current;
        if (lists.length > 0) await join(account, lists, called ?? undefined);
        settleAdded();
      })().catch(onError);
    },
    onSaveSettings: (account, label, change) => tune.mutate({ harness: door, profile: account, ...change }, {
      onSuccess: () => notify(t('harness.settings.saved', { account: label })),
      onError,
    }),
    onDoor: (harness, action) => run(harness, action),
    onPin: (harness, version) => run(harness, 'pin', undefined, version),
    scope,
    rules: {
      onSwitchDefault: (id, on) => ruleAct.mutate({ action: 'default', id, on }, {
        onSuccess: () => notify(t('settings.rules.switched', { id, state: t(on ? 'settings.rules.on' : 'settings.rules.off') })),
        onError,
      }),
      onRemove: ({ scope: scopeName, name, rule }) => ruleAct.mutate({ action: 'remove', rule, scope: scopeName, name }, {
        onSuccess: () => notify(t('settings.rules.removed', { rule, where: where(scopeName, name) })),
        onError,
      }),
      onAdd: ({ list, rule, scope: scopeName, name }, added) => ruleAct.mutate({ action: 'add', list, rule, scope: scopeName, name }, {
        onSuccess: () => {
          notify(t('settings.rules.added', { rule, list: t(`settings.rules.list.${list}`), where: where(scopeName, name) }));
          added();
        },
        onError,
      }),
      onAnswer: (id, accept) => {
        const proposal = rules.data?.proposals?.find((one) => one.id === id);
        settle.mutate({ id, accept }, {
          onSuccess: () => notify(t(accept ? 'settings.rules.proposals.accepted' : 'settings.rules.proposals.declined', {
            change: proposal ? proposalChange(proposal) : `#${id}`,
          })),
          onError,
        });
      },
    },
  };

  const rulesAnswer = rules.data && Array.isArray(rules.data.defaults) && Array.isArray(rules.data.scopes) ? rules.data : null;
  const doorRunning = tool.doors.find((each) => DOOR_ACTIONS.some((action) => running === `${each.harness}:${action}`))?.harness ?? null;
  const used = Array.isArray(usage.data?.accounts) ? usage.data.accounts : [];

  return (
    <AgentPage
      tool={tool}
      use={use}
      rules={rulesAnswer}
      usage={used.filter((account) => tool.doors.some((each) => each.harness === account.harness))}
      workspaces={workspacesOf(registry.data ?? [])}
      adapter={adapter}
      part={part}
      reading={read.isPending}
      busy={busy}
      settingsBusy={tune.isPending}
      signingIn={running === `${door}:login` ? runningProfile : null}
      signInPanel={running === `${door}:login` && runningProfile
        ? <SignIn id={running} harness={door} profile={labelOf(runningProfile)} />
        : null}
      signInNewPanel={signingInNew === door
        ? <SignIn id={`${door}:login-new`} harness={door} action="login-new" tool={product} />
        : null}
      added={addedHere ? { account: addedHere.profile, who: addedHere.account } : null}
      doorRunning={doorRunning}
      doorConsole={running && doorRunning ? <SessionConsole id={running} /> : null}
      acts={acts}
      onAnchored={onAnchored}
    />
  );
}
