import { type ReactNode, useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from '../locales/format';
import type { PlaceRow } from '../data/wiring';
import { useHarnessRun, WithHarnessRuns } from './harnessRuns';
import { useSessions } from '../data/queries';
import { SessionConsole } from '../work/console/SessionConsole';
import {
  useAccountUse, useAccounts, useHarnessAction, useHarnesses, usePlaces, useReadAccounts, useRefreshHarnesses, useRuleAction, useRules,
  useSetAgentSettings, useUsage,
} from '../bridge/shell';
import { ruleHearing, useRuleAnswer } from '../work/waits/ruleAnswer';
import { SignIn } from './SignIn';
import { byTool, type Tool } from './tools';
import { failure, type Notify, useErrorNotify } from '../design/ui';
import type { ViewLayout } from '../work/frame/ViewFrame';
import { agentOf } from '../settings/accounts/accounts';
import type { ScopeActs } from '../settings/accounts/AccountUse';
import { proposalChange } from '../settings/repository/proposals';
import { AgentList, AgentStrip } from './AgentList';
import { type AgentActs, AgentMainNotice, AgentPage, type Answered } from './AgentPage';
import { type AgentPart, accountLabel, agentRows } from './agents';
import { AgentsModes, AgentsStrip } from './AgentsModes';
import { type AgentsMode, MACHINE, readAgentsMode, repositoryId, storeAgentsMode } from './modes';
import { AllRoles } from './roles/AllRoles';
import { MachineNotes } from './setup/MachineNotes';
import { ReconcileNote } from './setup/ReconcileNote';
import { PlaceRoles } from './setup/PlaceRoles';
import type { PlaceDriving } from './setup/PlacePage';
import { doorOf, draftOfAsk, type PlaceAsk, type SetUpDoor, type SetUpPage, type SetUpPlace, useSetUpPage } from './setup/placePlan';
import { type PlaceAt, type PlaceEdit, type PlaceFlowHold, type PlaceSection, type PlaceView, usePlaceShell } from './setup/placeShell';
import { findPlace, placeAsk, rowOfFound, SetupMain, setUpPlaceOf, sourceDoor, useSetupPlaces } from './setup/SetupPlaces';
import { SetupList } from './setup/SetupList';
import { type RoleOpeners, useRolesPage } from './roles/RolesView';
import { usedByAgent } from './roles/roleStructure';
import { useStructureRead } from './roles/structureRead';
import type { ListPanes } from '../work/frame/listPanes';

/** What a door runs, and so what streams under it in *Ways in* (D49 §2). */
const DOOR_ACTIONS = ['install', 'update', 'pin', 'unpin'] as const;

/** A reading older than the agent's own floor is read again as its page opens (the switch design §1.2); a shell that names none is read at a minute. */
const STALE_AFTER_SECONDS = 60;

/**
 * The Agents place's mode as the application holds it (COMPANYVIEW1b): the one it was left in, remembered for the place, and a change of
 * it; and the place Setup last chose (`daoris.list.roles.chosen`).
 *
 * @remarks
 * **A switch to Agents is a door that names no item** (UX6b, D150 §1 rule 6): the agent chosen is read again before it is shown.
 */
export function useAgentsMode(lists: ListPanes) {
  const [mode, setMode] = useState<AgentsMode>(readAgentsMode);
  const shown = useRef(mode);
  const { reopen, choose } = lists;
  const onMode = useCallback((next: AgentsMode) => {
    if (next !== shown.current && next === 'agents') reopen('agents');
    shown.current = next;
    setMode(next);
    storeAgentsMode(next);
  }, [reopen]);
  const place = lists.pane('roles').chosen || null;
  const onPlace = useCallback((next: string) => choose('roles', next), [choose]);
  // AGENTSPLACE1k: an ask Ask Daoris proposed for a place's *Set up…*, held until the place's page has opened on it.
  const [seeded, setSeeded] = useState<{ place: string; ask: PlaceAsk } | null>(null);
  const onSeed = useCallback((target: string, ask: PlaceAsk) => setSeeded({ place: target, ask }), []);
  const onSeeded = useCallback(() => setSeeded(null), []);
  // SETUPUX1h: a door to a place at a section and a row (and the way back), held until the place's page has opened on it.
  const [arrival, setArrival] = useState<{ place: string; at: PlaceAt } | null>(null);
  const onPlaceAt = useCallback((next: string, at: PlaceAt) => {
    choose('roles', next);
    setArrival(at.section || at.row || at.back || at.edit ? { place: next, at } : null);
  }, [choose]);
  const onArrived = useCallback(() => setArrival(null), []);
  return { mode, onMode, place, onPlace, onPlaceAt, arrival, onArrived, seeded, onSeed, onSeeded };
}

/**
 * What a place's sections' bodies are handed besides the place (SETUPUX1o): the route and step its Workflow shows and a choice of them, which
 * the shell holds above the mounts, a door that opens another section of the page, and a door to another place at a section and row.
 */
export type PlaceHold = {
  kind: 'workspace' | 'repository';
  row: PlaceRow;
  /** The row a door asked for here, until it is brought into view. */
  asked?: string | null;
  /** The draft a door asked the Workflow's editor to open on (FLOWCHART2g), and the word that it has. */
  edit?: PlaceEdit | null;
  onEdited?: () => void;
  /** The step editor's draft, kept above the mounts so it outlives the section (REVIEW2 P5). */
  held?: PlaceFlowHold | null;
  onHold?: (held: PlaceFlowHold | null) => void;
  view: PlaceView;
  onView: (choice: Partial<PlaceView>) => void;
  onOpenSection: (section: PlaceSection) => void;
  onGo: (target: string, at: PlaceAt) => void;
};

/**
 * **The Agents place** (UX6e, D150 §5): what it hands the frame (D118 §5), its list pane and its main area. The list is
 * each agent this machine knows, once; the main area is the chosen agent's page.
 *
 * @remarks
 * **A hook, because a view hands the frame a value**, as the Plugins view does. The application holds it on every view.
 *
 * 🔴 **Opening it asks no sign-in** (§5.3). It reads the roster the frame already holds (asked once, and kept:
 * `staleTime: Infinity`) and the accounts' files (`ACCOUNTS`, a file read the tick refreshes); each account says its last
 * known state with when it was read. Only *Read again* asks, on the press, one agent's accounts one at a time. An agent's page
 * reads its accounts' use as it opens, where a reading is older than a minute, and at *Read now* (SWITCH1d): the agent answers
 * it with no prompt and no model request (D165 point 1, amending §5.3 for that read alone).
 *
 * **Every act has its terminal twin** (D50): `daoris agent …`, named at the page's foot.
 */
export function useAgentsView({ active, chosen, onChoose, notify, part = null, onAnchored, onOpenPlugin, say, modes, roleDoors, bodies, driving, setUp }: {
  /**
   * A place's *Set up…* (AGENTSPLACE1j), opened in its main area and handed in by the application for the same reason: it reads the plan and
   * presses Apply, the person's own act. The set-up's draft per place is held here, so a door out of it and back finds it as left.
   */
  setUp?: (place: SetUpPlace, page: SetUpPage, doorFor: SetUpDoor) => ReactNode;
  /**
   * A place's Workflow and Sessions sections' bodies (AGENTSPLACE1f, 1h), handed in by the application, which holds the presses the rows
   * make: this place imports none of them, so Ask Daoris's census of what the place presses does not take them for its own. A section
   * mounts its body only while it is open, so a folded one reads nothing.
   */
  bodies?: (place: PlaceHold) => { workflow: ReactNode; sessions: ReactNode };
  /** A repository's driving for its page head (SETUPUX1h): the organism holds the hooks and renders the page with what it made. */
  driving?: (row: PlaceRow, render: (driving: PlaceDriving) => ReactNode) => ReactNode;
  /** The view is in front. */
  active: boolean;
  /**
   * The Roles mode beside Agents (COMPANYVIEW1b, the company-view design §1), on a desktop only: the mode in front and a change of it,
   * and the section chosen in the Roles mode and a change of it. Absent, the place is the Agents list alone, as a browser's is.
   */
  modes?: {
    mode: AgentsMode; onMode: (mode: AgentsMode) => void;
    /** The place Setup has chosen, and a change of it. */
    place: string | null; onPlace: (place: string) => void;
    /** A door's section and row at a place, and the way back (SETUPUX1h), which the page opens on once. */
    onPlaceAt?: (place: string, at: PlaceAt) => void;
    arrival?: { place: string; at: PlaceAt } | null; onArrived?: () => void;
    /** The set-up Ask Daoris proposed for a place, which opens on it once (AGENTSPLACE1k), and the word that it has. */
    seeded?: { place: string; ask: PlaceAsk } | null; onSeeded?: () => void;
  };
  /** The list's chosen item: an agent's id, which the application remembers (`daoris.list.agents.chosen`). */
  chosen: string | null;
  onChoose: (item: string | null) => void;
  notify: Notify;
  /** The part of an agent's page a door named (D150 §2.4), opened and brought into view. */
  part?: AgentPart | null;
  /** Told once the part is open, so a later visit opens at the page's top again. */
  onAnchored?: () => void;
  /** A door's *declared by plugin* opens that plugin in the Plugins place (PLUGUI1c2b). */
  onOpenPlugin?: (plugin: string) => void;
  /**
   * The words to a session (PROPOSEHERE1c), handed in by the application: an *Accept* on a proposal tells the session that
   * proposed it. The bridge's say hook is pressed there, not here, so this place answers for no control it did not have.
   */
  say?: (session: string, text: string) => Promise<{ sent: boolean }>;
  /** Where the Roles mode's doors lead (COMPANYVIEW1d), handed in by the application, which holds every view's opening. */
  roleDoors?: RoleOpeners;
}): ViewLayout {
  const { t } = useTranslation();
  const roster = useHarnesses();
  const inSetup = modes?.mode === 'setup';
  const places = useSetupPlaces(active && inSetup);
  // The Roles page's state lives here, so a door out of it and the way back find the selection and the scope as left.
  const rolesPage = useRolesPage();
  const setUpPage = useSetUpPage();
  // AGENTSPLACE1k: a place's *Set up…* opens on the ask Ask Daoris proposed. The page plans it on opening, and Apply stays the person's.
  const seeded = modes?.seeded ?? null;
  const onSeeded = modes?.onSeeded;
  const seededHere = active && inSetup && seeded !== null && seeded.place === modes?.place;
  useEffect(() => {
    if (!seededHere || !seeded) return;
    setUpPage.open(seeded.place, seeded.ask.repositories, draftOfAsk(seeded.ask));
    onSeeded?.();
  }, [seededHere, seeded, setUpPage, onSeeded]);
  // SETUPUX1h: the page's state (the sections open, the row a door asked for, the way back) is held here, above the sections' mounts.
  const shell = usePlaceShell();
  const arrival = modes?.arrival ?? null;
  const onArrived = modes?.onArrived;
  const arrivedHere = active && inSetup && arrival !== null && arrival.place === modes?.place;
  const { arrive } = shell;
  useEffect(() => {
    if (!arrivedHere || !arrival) return;
    arrive(arrival.place, arrival.at);
    onArrived?.();
  }, [arrivedHere, arrival, arrive, onArrived]);
  // A not-driven repository's row is not in the list's page: its own, read whole (`detail`), once it is chosen.
  const chosenPlace = active && inSetup ? findPlace(places.read, modes?.place ?? null) : null;
  const importedDetail = usePlaces(
    chosenPlace?.kind === 'imported' ? { detail: { workspace: chosenPlace.workspace, repository: chosenPlace.repository } } : {},
    { enabled: chosenPlace?.kind === 'imported' },
  );
  const accounts = useAccounts();
  useErrorNotify(active ? roster.error : null, notify);

  const harnesses = Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : null;
  const tools = harnesses ? byTool(harnesses, roster.data?.agents) : [];
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

  // A first answer still on its way; an answer that is no roster lists nothing.
  const loading = active && roster.data === undefined && roster.isPending;
  if (modes && inSetup) {
    const placed = findPlace(places.read, modes.place);
    const setUpPlace = setUp ? setUpPlaceOf(placed, modes.place, places.read) : null;
    const row = rowOfFound(placed, importedDetail.data?.detail ?? null);
    // A door to another place at a section and row: the shell holds it, and the list chooses the place.
    const onGo = (target: string, at: PlaceAt) => {
      shell.arrive(target, at);
      modes.onPlace(target);
    };
    const name = !placed || !row ? '' : placed.kind === 'machine' ? t('agents.setup.machine') : placed.kind === 'workspace' ? row.workspace ?? '' : row.repository ?? '';
    const counts = placed?.kind === 'workspace' ? { driven: placed.driven, notDriven: placed.notDriven } : undefined;
    const members = placed?.kind === 'workspace' ? (places.read?.repositories ?? []).filter((each) => each.workspace === placed.row.workspace) : undefined;
    const ofBodies = placed && placed.kind !== 'machine' && row && modes.place
      ? bodies?.({
        kind: placed.kind === 'workspace' ? 'workspace' : 'repository',
        row,
        asked: shell.rowOf(modes.place),
        edit: shell.editOf(modes.place),
        onEdited: () => shell.editOpened(modes.place!),
        held: shell.holdOf(modes.place),
        onHold: (held) => shell.hold(modes.place!, held),
        view: shell.viewOf(modes.place),
        onView: (choice) => shell.choose(modes.place!, choice),
        onOpenSection: (section) => shell.setOpen(modes.place!, section, true),
        onGo,
      })
      : undefined;
    // SETUPUX1j: a refusal's door leaves the set-up with its draft kept, opens the page where the cause is put right (the place's own, or for a
    // workspace the refusing repository's) and returns by *Back*, which finds the draft as left and the plan read again.
    const doorFor: SetUpDoor = (refusal) => {
      if (!setUpPlace || !doorOf(refusal.code)) return null;
      const target = setUpPlace.kind === 'workspace'
        ? (refusal.repository ? repositoryId(setUpPlace.workspace, refusal.repository) : null)
        : setUpPlace.id;
      if (!target || (target !== setUpPlace.id && !findPlace(places.read, target))) return null;
      return () => {
        setUpPage.leave(setUpPlace.id);
        onGo(target, { section: 'workflow', row: 'landing', back: { place: setUpPlace.id, name: setUpPlace.name, section: null, setUp: true } });
      };
    };
    return {
      list: {
        view: 'agents',
        chosenIn: 'roles',
        name: t('nav.agents'),
        labels: { open: t('agents.list.open'), close: t('agents.list.close'), resize: t('agents.list.resize') },
        head: <AgentsModes mode={modes.mode} onMode={modes.onMode} />,
        strip: <AgentsStrip mode={modes.mode} onMode={modes.onMode} />,
        chosen: modes.place,
        // A choice is the head's to be read from: the list laid over the page goes, and the focus returns to the head (SETUPUX1h).
        body: <SetupList state={places} chosen={modes.place} onChoose={(next) => { shell.clearBack(); modes.onPlace(next); shell.askFocus(); }} />,
      },
      main: (
        <SetupMain
          found={placed}
          state={places.status}
          id={modes.place}
          row={row}
          counts={counts}
          members={members}
          shell={shell}
          bodies={placed && row
            ? {
              roles: (
                <PlaceRoles
                  place={placed.kind === 'imported' ? { workspace: placed.workspace, repository: placed.repository } : placeAsk(placed)}
                  notify={notify}
                  onSource={sourceDoor({ id: modes.place!, name }, row.workspace, onGo)}
                />
              ),
              // SETUPUX1m: This machine alone draws every role once, its diagram and the suggested choices.
              ...(placed.kind === 'machine'
                ? { allRoles: <AllRoles notify={notify} page={rolesPage} doors={roleDoors} onGo={onGo} /> }
                : {}),
              ...(ofBodies ?? {}),
            }
            : {}}
          notes={placed?.kind === 'machine'
            ? <MachineNotes onShow={() => { rolesPage.onView('list'); shell.arrive(MACHINE, { section: 'allRoles' }); }} />
            // D230 point 2: an adopted repository's page offers its reconcile where the doctrine tool's status says the canon moved.
            : placed?.kind === 'repository' && row?.adopted && row.repository
              ? <ReconcileNote key={row.repository} repository={row.repository} />
              : undefined}
          drive={driving}
          onGo={onGo}
          setUp={setUp && setUpPlace
            ? {
              open: setUpPage.shown(setUpPlace.id),
              onOpen: () => setUpPage.open(setUpPlace.id, setUpPlace.kind === 'workspace' ? setUpPlace.driven : [setUpPlace.name]),
              onResume: setUpPage.resume,
              panel: setUp(setUpPlace, setUpPage, doorFor),
            }
            : undefined}
        />
      ),
    };
  }
  return {
    list: {
      view: 'agents',
      name: t('nav.agents'),
      labels: { open: t('agents.list.open'), close: t('agents.list.close'), resize: t('agents.list.resize') },
      head: modes ? <AgentsModes mode={modes.mode} onMode={modes.onMode} /> : undefined,
      strip: modes
        ? <AgentsStrip mode={modes.mode} onMode={modes.onMode}>{harnesses && <AgentStrip rows={rows} chosen={chosen} onChoose={onChoose} />}</AgentsStrip>
        : harnesses ? <AgentStrip rows={rows} chosen={chosen} onChoose={onChoose} /> : undefined,
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
                onOpenPlugin={onOpenPlugin}
                say={say}
                usedBy={modes !== undefined}
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
 * The chosen agent's page and every act on it (UX6e): the organism under the page, inside the application's running
 * action (SIGNIN1), so a sign-in started here outlives leaving the place and its end is said wherever the person is.
 */
function AgentsMain({ tool, adapter, notify, part, onAnchored, onOpenPlugin, say, usedBy: asksUsedBy = false, install = false, onInstallTaken }: {
  tool: Tool;
  /** Whether the page says the roles that lean on the agent (COMPANYVIEW1d): on a desktop, where the Roles mode is. */
  usedBy?: boolean;
  say?: (session: string, text: string) => Promise<{ sent: boolean }>;
  adapter?: string;
  notify: Notify;
  part?: AgentPart | null;
  onAnchored?: () => void;
  onOpenPlugin?: (plugin: string) => void;
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
  // A proposal's answer, its one owner (PROPOSEHERE1b): *What needs you* and the session's conversation press the same.
  const ruleAnswer = useRuleAnswer({ notify });
  // The session that proposed a rule is found in the list while a proposal names one and a door to it was handed (PROPOSEHERE1c).
  const proposalSessions = useSessions(null, true, Boolean(say) && Array.isArray(rules.data?.proposals) && rules.data.proposals.some((one) => one.session));
  const {
    running, runningProfile, signingInNew, busy: acting, run, added, settleAdded, refusal, retry, dismiss,
    failure: failed, otherWay, switchWay,
  } = useHarnessRun();
  const onError = failure(notify);
  const door = tool.lead ?? tool.name;
  const product = tool.product ?? tool.name;
  const use = agentOf(accounts.data, tool.name);
  const busy = acting || act.isPending || accountUse.isPending || ruleAct.isPending || ruleAnswer.busy;
  // *Used by*: from the structure's last readings, which probe nothing.
  const structure = useStructureRead(asksUsedBy);
  const usedBy = !asksUsedBy
    ? null
    : { read: structure.read === 'ok' ? 'ok' as const : structure.read === 'failed' ? 'failed' as const : 'loading' as const, rows: structure.answer ? usedByAgent(structure.answer, tool) : [] };

  // Each account's use read as its page opens (SWITCH1d; the switch design §1.2), once a page, where its agent is asked it and
  // a reading is older than a minute or none was had: a read asks no sign-in and spends nothing (D165 point 1), and the driver
  // skips an account cooling, refused, signed out or busy and floors each at its question's minute. A key is never read.
  const readAccounts = useReadAccounts();
  const stale = Boolean(use?.reads) && tool.present && tool.accounts.some((account) => {
    if (account.key) return false;
    const seen = use?.accounts.find((facts) => facts.name === account.name)?.said?.seen;
    return !seen || Date.now() - new Date(seen).getTime() > (use?.readFloor ?? STALE_AFTER_SECONDS) * 1000;
  });
  const readOnOpen = useRef<string | null>(null);
  const mutateRead = readAccounts.mutate;
  useEffect(() => {
    if (!stale || readOnOpen.current === tool.name) return;
    readOnOpen.current = tool.name;
    // Unasked, so a failure is not said: *Read now* says its own.
    mutateRead({ harness: door });
  }, [stale, tool.name, door, mutateRead]);
  // The new account this agent's sign-in kept, waiting on its name (UX7b, POOL1c): the add flow's step 3.
  const addedHere = added && added.harness === door ? added : null;
  // A key added here asks the same question (the UX7 design §4.5, ACCTUX3): by its id and its key's handle, held while the
  // page is, since it ends inside the request on this page; it serves every workspace whatever is answered (D197).
  const [keyAdded, setKeyAdded] = useState<{ profile: string; key: string | null } | null>(null);
  // The step answers the key's first, the newer press; a sign-in's waits behind it, held above every view.
  const answering = keyAdded
    ? { profile: keyAdded.profile, who: null, offered: null, key: keyAdded.key, settle: () => setKeyAdded(null) }
    : addedHere
      ? { profile: addedHere.profile, who: addedHere.account, offered: addedHere.offered, key: null, settle: settleAdded }
      : null;

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
    // An agent Daoris has no installer for is never run one (INSTALLDOOR1): the driver refuses it, and its page says how.
    if (!tool.present && tool.acts?.installs) run(door, 'install');
  }, [install, onInstallTaken, tool.present, tool.doors, run, door]);

  const labelOf = (name: string | null) => {
    if (!name) return t('agents.account.own');
    const account = tool.accounts.find((each) => each.name === name);
    return account ? accountLabel(account, tool) : name;
  };

  // ACCT2's rename, the terminal's `daoris agent profile rename` (D50), said as it lands.
  const rename = async (account: string, name: string | null) => {
    // Naming an account its own id gives it no name of its own (ACCT2): the one way to clear it at the door.
    const answer = await act.mutateAsync({ harness: door, action: 'profile-rename', profile: account, name: name ?? account });
    notify(answer.name
      ? t('agents.renamed', { id: account, name: answer.name })
      : t('agents.unnamed', { id: account }));
    return answer.name ?? null;
  };

  // ACCTEDIT1: a question's act told back to it: done once it lands, so it closes then and not on the press; refused, its
  // sentence is said in the question, whole, with what the person entered kept. A press with no question (an ⋯ item) has
  // no form to say it in, so it is a toast (the platform language §4).
  const told = (landing: Promise<unknown>, answered?: Answered) => {
    landing.then(() => answered?.done(), (error: unknown) => (answered ? answered.refused(sentence(error)) : onError(error)));
  };

  // The set's order and its one entry of settings (POOL1c, D197 point 2): no workspace is named, since one set serves them all.
  const scope: ScopeActs = {
    onOrder: (list) => accountUse.mutate({ harness: door, action: 'order', accounts: list }, { onError }),
    // A refusal the press's own place says (the curve's editor, PUSH1e) is told there, in the reader's language, and toasted nowhere.
    onUse: (change, refused) => accountUse.mutate(
      { harness: door, action: 'use', ...change },
      { onError: refused ? (error: unknown) => refused(sentence(error)) : onError },
    ),
  };
  const where = (scopeName: string, name: string | undefined) => scopeName === 'machine'
    ? t('settings.rules.proposals.everywhere')
    : t(scopeName === 'workspace' ? 'settings.rules.where.workspace' : 'settings.rules.where.repository', { name: name ?? '' });

  const acts: AgentActs = {
    onReadAgain: () => read.mutate({ agent: tool.name }, { onError }),
    onReadOne: (account) => read.mutate({ agent: tool.name, profile: account }, { onError }),
    onReadNow: () => readAccounts.mutate({ harness: door }, { onError }),
    onSignInNew: () => run(door, 'login-new'),
    // ACCTUX3: told back to the key's field, which closes once the key is kept and says a refusal in it; kept, the account
    // goes to the add flow's last step for its name, where a toast said it was added.
    onAddKey: (key, answered) => told((async () => {
      const result = await act.mutateAsync({ harness: door, action: 'key-add', key });
      if (result.profile) setKeyAdded({ profile: result.profile, key: result.key ?? null });
    })(), answered),
    // KEYREPLACE1b: the key goes into the same account, which keeps its name and its place in the order, so no add flow follows.
    onReplaceKey: (account, key, answered) => told(
      act.mutateAsync({ harness: door, action: 'key-replace', profile: account, key }), answered,
    ),
    onSignIn: (account) => run(door, 'login', account),
    onTryNow: (account, label) => accountUse.mutate(
      { harness: door, action: 'ready', profile: account },
      { onSuccess: (answer) => notify(t(answer.ended ? 'harness.cooling.ready' : 'harness.cooling.notCooling', { account: label })), onError },
    ),
    onRemove: (account, answered) => run(door, 'profile-remove', account, undefined, answered),
    onRename: (account, name, answered) => told(rename(account, name), answered),
    // The add flow's end (D152 §4.5; POOL1c, the account-pool design §4.1): its name kept where the field holds one other than
    // the one it has; refused, the question stays up, so nothing the person typed is lost, and says why in it.
    onAddedAnswer: ({ name }, answered) => {
      if (!answering) return;
      const account = answering.profile;
      const current = tool.accounts.find((each) => each.name === account)?.displayName?.trim() || null;
      const wanted = name.trim() || null;
      told((async () => {
        if (wanted !== current) await rename(account, wanted);
        answering.settle();
      })(), answered);
    },
    // *Got it* (POOL1a; the account-pool design §6.3): the receipts the page showed, by id, marked said; a failure is a toast,
    // since the block stays and the press is offered again.
    onPooledSaid: (ids) => accountUse.mutate({ harness: door, action: 'said', ids }, { onError }),
    // ACCTUX3: told back to its editor, which closes once the save lands and says a refusal under it, the choice kept.
    onSaveSettings: (account, label, change, answered) => told((async () => {
      await tune.mutateAsync({ harness: door, profile: account, ...change });
      notify(t('harness.settings.saved', { account: label }));
    })(), answered),
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
      // This card's press is *Accept*, whatever the proposal does, so its sentence says it so.
      onAnswer: (id, accept) => {
        const proposal = rules.data?.proposals?.find((one) => one.id === id);
        ruleAnswer.settled(
          id, accept, proposal ? proposalChange(proposal) : `#${id}`, 'accept', ruleHearing(proposal, proposalSessions.data ?? [], say),
        ).then((said) => notify(said), onError);
      },
    },
  };

  const rulesAnswer = rules.data && Array.isArray(rules.data.defaults) && Array.isArray(rules.data.scopes) ? rules.data : null;
  const doorRunning = tool.doors.find((each) => DOOR_ACTIONS.some((action) => running === `${each.harness}:${action}`))?.harness ?? null;
  // Not answered yet is not nothing measured (PUSHUX1): the fold says it is reading until the usage is known.
  const used = usage.isPending ? null : Array.isArray(usage.data?.accounts) ? usage.data.accounts : [];

  return (
    <AgentPage
      tool={tool}
      use={use}
      rules={rulesAnswer}
      usedBy={usedBy}
      usage={used && used.filter((account) => tool.doors.some((each) => each.harness === account.harness))}
      adapter={adapter}
      part={part}
      reading={read.isPending}
      readingNow={readAccounts.isPending}
      busy={busy}
      settingsBusy={tune.isPending}
      signingIn={running === `${door}:login` ? runningProfile : null}
      signInPanel={running === `${door}:login` && runningProfile
        ? (
          <SignIn
            id={running} harness={door} profile={labelOf(runningProfile)} tool={product} refused={refusal}
            failed={failed?.sentence} failedAt={failed?.at} onRetry={retry} onClose={dismiss}
            other={otherWay} onSwitch={switchWay}
          />
        )
        : null}
      signInNewPanel={signingInNew === door
        ? (
          <SignIn
            id={`${door}:login-new`} harness={door} action="login-new" tool={product} refused={refusal}
            failed={failed?.sentence} failedAt={failed?.at} onRetry={retry} onClose={dismiss}
            other={otherWay} onSwitch={switchWay}
          />
        )
        : null}
      added={answering ? { account: answering.profile, who: answering.who, key: answering.key, offered: answering.offered } : null}
      doorRunning={doorRunning}
      doorConsole={running && doorRunning ? <SessionConsole id={running} /> : null}
      acts={acts}
      onOpenPlugin={onOpenPlugin}
      onAnchored={onAnchored}
    />
  );
}
