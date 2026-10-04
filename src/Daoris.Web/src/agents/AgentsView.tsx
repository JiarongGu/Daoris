import { useEffect } from 'react';
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
import { ListMore } from '../work/ListPane';
import type { ViewLayout } from '../work/ViewFrame';
import { workspacesOf } from '../workspaces';
import { agentOf } from '../settings/accounts';
import type { ScopeActs } from '../settings/AccountUse';
import { proposalChange } from '../settings/proposals';
import { AgentList, AgentStrip } from './AgentList';
import { type AgentActs, AgentMainNotice, AgentPage } from './AgentPage';
import { type AgentPart, agentRows, signedOutHeld } from './agents';

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
export function useAgentsView({ active, chosen, onChoose, filters, onFilters, notify, part = null, onAnchored }: {
  /** The view is in front. */
  active: boolean;
  /** The list's chosen item: an agent's id, which the application remembers (`daoris.list.agents.chosen`). */
  chosen: string | null;
  onChoose: (item: string | null) => void;
  /** The list's own filters: whether the agents not installed are shown (its ⋯). */
  filters: Record<string, unknown>;
  onFilters: (filters: Record<string, unknown>) => void;
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
  const rows = agentRows(tools, accounts.data);
  const showAbsent = filters.absent === true;
  // The agents not installed are the ⋯'s to show (§5.1); the one chosen stays listed whatever the filter says.
  const shown = rows.filter((row) => row.installed || showAbsent || row.name === chosen);
  const chosenTool = tools.find((tool) => tool.name === chosen) ?? null;
  const adapter = roster.data?.adapter;

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
      more: (
        <ListMore
          label={t('agents.list.more')}
          items={[{ id: 'absent', label: t('agents.list.absent'), checked: showAbsent }]}
          onChoose={() => onFilters({ ...filters, absent: !showAbsent })}
        />
      ),
      strip: harnesses ? <AgentStrip rows={shown} chosen={chosen} onChoose={onChoose} /> : undefined,
      loading,
      empty: harnesses && shown.length === 0 ? { headline: t('agents.empty.headline'), body: t('agents.empty.body') } : undefined,
      chosen,
      // A remembered agent reopens only while this machine still knows it (UX6b): one gone opens nothing chosen.
      standing: !chosen ? undefined : !harnesses ? 'unread' : chosenTool ? 'live' : 'gone',
      body: <AgentList rows={shown} chosen={chosen} onChoose={onChoose} />,
    },
    main: (
      <WithHarnessRuns notify={notify}>
        {loading
          ? <AgentMainNotice state="loading" />
          : chosenTool
            ? <AgentsMain key={chosenTool.name} tool={chosenTool} adapter={adapter} notify={notify} part={part} onAnchored={onAnchored} />
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
function AgentsMain({ tool, adapter, notify, part, onAnchored }: {
  tool: Tool;
  adapter?: string;
  notify: Notify;
  part?: AgentPart | null;
  onAnchored?: () => void;
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
  const { running, runningProfile, signingInNew, busy: acting, run } = useHarnessRun();
  const onError = failure(notify);
  const door = tool.doors[0]!.harness;
  const product = tool.product ?? tool.name;
  const use = agentOf(accounts.data, tool.name);
  const busy = acting || act.isPending || accountUse.isPending || ruleAct.isPending || settle.isPending;

  const labelOf = (name: string | null) => {
    if (!name) return tool.ownAccount ?? t('agents.account.own');
    const account = tool.accounts.find((each) => each.name === name);
    return account?.account ?? (account?.key ? t('harness.profile.keyName', { handle: account.key }) : name);
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
      doorRunning={doorRunning}
      doorConsole={running && doorRunning ? <SessionConsole id={running} /> : null}
      acts={acts}
      onAnchored={onAnchored}
    />
  );
}
