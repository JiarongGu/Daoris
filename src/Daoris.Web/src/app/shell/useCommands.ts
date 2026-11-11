import type * as React from 'react';
import { useEffect, useMemo, useRef } from 'react';
import type { useScope } from '../../data/scope';
import { type NotificationsAt } from '../../notifications/NotificationsView';
import { offerStore, useMainOffer } from '../../menus/mainOffer';
import { fieldTracker, findTarget, runEdit } from '../../menus/editing';
import { opensAtStart, setupProgress, setupSteps } from '../../help/setup';
import { useMachine } from '../../help/useMachine';
import { useSetupAtStart } from '../../help/setupGuide';
import { type LayoutRegion } from '../../work/strip/LayoutToggles';
import { focusRegion } from '../../work/frame/regions';
import { type OpenPart, questsItem, workspaceItem } from '../opener';
import { type SettingsAnchor, type SettingsSection } from '../../settings/SettingsView';
import { logEvent } from '../../bridge/shell';
import { byTool } from '../../agents/tools';
import { menuRows } from '../../work/strip/appMenus';
import { offeredActs } from '../../work/acts';
import { MONITOR_WINDOW } from '../../work/window';
import { type CommandDoors, type CommandState, commandTable, type FrameIntent, MENUS, type View } from '../../menus/commands';
import { type BarMenu } from '../../work/strip/AppMenu';
import { useThemeChoice } from '../../design/theme';
import { zoomBy } from '../../design/zoom';
import { SETTINGS_DOMAINS } from '../../settings/domains';
import { pageLanguage } from '../../locales/languages';
import { loggedId } from '../shellHelpers';
import { type UseQueryResult, type UseMutationResult } from '@tanstack/react-query';
import { type Session } from '../../data/api';
import { type ListPanes } from '../../work/frame/listPanes';
import { type FrameClosings } from '../../work/frame/closings';
import { type Placements } from '../../work/frame/placements';
import { type i18n, type TFunction } from 'i18next';
import { type HarnessRoster } from '../../bridge/agents/agents';
import { type RefreshResponse } from '../../data/generated/service';
import { type Notify } from '../../design/atoms/Toasts';

export function useCommands({ tab, openSettings, running, attending, lists, attached, view, listed, listShown, closings, placements, holdings, scope, circle, wired, i18n, roster, knowledgeMode, refresh, zoom, open, setWorkIntent, setAsking, setOpening, onSyncNow, notify, t, setPalette, toggleRegion, showNotifications, openWindow, openBrowser, onRefresh, openHelp, changeFocus, askQuickly, setShortcuts, setAbout }: {
  tab: View;
  openSettings: (section: SettingsSection, anchor?: SettingsAnchor | undefined) => void;
  running: UseQueryResult<Session[], Error>;
  attending: string | null;
  lists: ListPanes;
  attached: boolean;
  view: View;
  listed: boolean;
  listShown: boolean;
  closings: FrameClosings;
  placements: Placements;
  holdings: UseQueryResult<{ name: string; repositories: number; members: string[]; }[], Error>;
  scope: ReturnType<typeof useScope>;
  circle: string | null;
  wired: boolean | null;
  i18n: i18n;
  roster: UseQueryResult<HarnessRoster, Error>;
  knowledgeMode: "search" | "convergence";
  refresh: UseMutationResult<RefreshResponse, Error, void, unknown>;
  zoom: number;
  open: (target: View, item?: string | null | undefined, part?: OpenPart | undefined) => void;
  setWorkIntent: React.Dispatch<React.SetStateAction<FrameIntent | null>>;
  setAsking: React.Dispatch<React.SetStateAction<boolean>>;
  setOpening: React.Dispatch<React.SetStateAction<{ from?: string | undefined; to?: string | undefined; } | null>>;
  onSyncNow: (workspace: string) => void;
  notify: Notify;
  t: TFunction<"translation", undefined>;
  setPalette: React.Dispatch<React.SetStateAction<boolean>>;
  toggleRegion: (region: LayoutRegion) => boolean;
  showNotifications: (at?: Omit<NotificationsAt, "n"> | undefined) => void;
  openWindow: UseMutationResult<{ opened: boolean; windows: string[]; }, Error, string | { name: string; workspace?: string | null | undefined; }, unknown>;
  openBrowser: UseMutationResult<{ opened: boolean; windows: string[]; }, Error, string | undefined, unknown>;
  onRefresh: () => void;
  openHelp: () => void;
  changeFocus: () => void;
  askQuickly: (question?: string | undefined, draft?: boolean) => void;
  setShortcuts: React.Dispatch<React.SetStateAction<boolean>>;
  setAbout: React.Dispatch<React.SetStateAction<boolean>>;
}) {
  // The setup guide, from the reading Ask Daoris's starters share: the status bar's count
  // until the required steps are done, and Get started opened once at start on a machine missing any of
  // the first three. Both wait for the machine to be read, and neither is a browser's.
  const setupReading = useMachine();
  const setupStepsNow = setupSteps(setupReading.machine);
  const setupCount = setupReading.settled ? setupProgress(setupStepsNow) ?? undefined : undefined;
  useSetupAtStart({
    settled: setupReading.settled,
    needed: opensAtStart(setupStepsNow),
    // The view chosen, not the one shown: a remembered Sessions stands in as Overview until the shell
    // answers, and that is not the person going anywhere.
    view: tab,
    open: () => openSettings('start'),
  });

  // The record in the main area, as its page offers it to a right-click, read by Run's record groups and Edit's
  // *Copy ID*: each act has one owner, whichever door pressed it.
  const copyText = useRef<(text: string) => void>(() => {});
  const offers = useMemo(() => offerStore((text) => copyText.current(text)), []);
  const offered = useMainOffer(offers);
  // The field the person last had focus in, which Edit's acts go back to once a menu has taken the focus.
  const fields = useRef<ReturnType<typeof fieldTracker> | null>(null);
  useEffect(() => {
    const tracker = fieldTracker(document);
    fields.current = tracker;
    return () => {
      tracker.stop();
      fields.current = null;
    };
  }, []);
  const [theme, setTheme] = useThemeChoice();
  const attendedSession = (running.data ?? []).find((session) => session.id === attending) ?? null;
  const chosenQuest = lists.pane('quests').chosen;

  /**
   * What is true now, which decides what the menus, the palette and the keys offer. The box Find goes to and
   * the field Edit acts on are read from the page as it stands, so a key reads them at the press.
   */
  const commandState = (): CommandState => {
    const live = (offered?.acts ?? []).filter((act) => !act.disabled).map((act) => act.id);
    return {
      attached,
      view,
      list: listed ? { shown: listShown } : null,
      panelShown: !closings.panel,
      sideShown: !closings.dock,
      moved: placements.moved,
      workspaces: holdings.data ?? [],
      scope: scope.workspace,
      circle,
      wired: Boolean(wired),
      theme,
      language: pageLanguage(i18n.language),
      // Each agent installed here, as the Agents place lists it: the palette goes to each one's page.
      agents: byTool(Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : [])
        .filter((tool) => tool.present)
        .map((tool) => ({ name: tool.name, label: tool.product ?? tool.name })),
      domains: SETTINGS_DOMAINS.filter((domain) => attached || !domain.machine).map(({ id, label, rows }) => ({ id, label, rows })),
      knowledge: knowledgeMode,
      // The session attended on Sessions, with what its header offers; *Answer…* is its card's, offered where its row's is.
      session: view === 'sessions' && offered
        ? { acts: live, answer: attached && attendedSession !== null && offeredActs({ session: attendedSession }, 'row').includes('answer') }
        : null,
      // A quest open on Quests (an ask's page is not a quest's), with what its header and its body offer.
      quest: view === 'quests' && offered && chosenQuest && 'quest' in questsItem(chosenQuest) ? { acts: live } : null,
      record: live.includes('copy'),
      find: findTarget(document, view) !== null,
      field: fields.current?.field() != null,
      history: fields.current?.history() != null,
      refreshing: refresh.isPending,
      zoom,
    };
  };

  /** What the Work frame does with the session attended there; answering, starting and archiving are Sessions' own. */
  const frameIntent = (intent: FrameIntent) => {
    if (intent === 'start' || intent === 'answer' || intent === 'archiveEnded' || intent === 'review') open('sessions');
    setWorkIntent(intent);
  };

  /** What each verb runs: the application's doors, handed to the table. */
  const commandDoors: CommandDoors = {
    // Asking lives at the head of Quests; the menu goes there and opens the composer, and a quest's composer too.
    ask: () => { open('quests'); setAsking(true); },
    quest: () => { open('quests'); setOpening({}); },
    scope: scope.setWorkspace,
    add: () => open('projects', null, { drawer: 'add' }),
    // `daoris import <folder> --workspace <name>`'s screen door: the drawer chooses the folder and names the workspace.
    import: () => open('projects', null, { drawer: 'import' }),
    // The workspace in view's setup: its place's Workflow defaults, where the opener sends a section.
    workspaceSetup: () => open('projects', null, { workspaceSection: 'defaults' }),
    sync: () => { if (circle) onSyncNow(circle); },
    wire: () => { if (circle) open('projects', workspaceItem(circle), { workspaceSection: 'remote' }); },
    // At a domain the row names, or at the one Settings' list remembers.
    settings: (section) => (section ? openSettings(section as SettingsSection) : open('settings')),
    edit: (act) => {
      void runEdit(act, fields.current?.field() ?? fields.current?.history() ?? null).then((done) => {
        if (!done && act === 'paste') notify(t('menu.pasteRefused'), 'error');
      });
    },
    find: () => findTarget(document, view)?.focus(),
    searchKnowledge: () => {
      // Knowledge's Search, whatever mode the place is left in.
      open('knowledge', null, { knowledge: 'search' });
      // A beat after the view is drawn, as Quick Ask's box is opened: its box is in its list.
      window.setTimeout(() => findTarget(document, 'knowledge')?.focus(), 0);
    },
    copyId: () => { offers.run('copy'); },
    palette: () => setPalette(true),
    toggle: (region: LayoutRegion) => { toggleRegion(region); },
    reset: () => placements.reset(),
    frame: frameIntent,
    // *Notifications* where it stands: View's row and the palette's, as the bell shows it.
    notifications: () => showNotifications(),
    // A window is the shell's to open, so neither is in a browser's table.
    monitor: () => openWindow.mutate(MONITOR_WINDOW),
    browser: () => openBrowser.mutate(),
    theme: setTheme,
    language: (language) => void i18n.changeLanguage(language),
    zoom: zoomBy,
    refresh: onRefresh,
    go: (target) => open(target),
    knowledge: (mode) => open('knowledge', null, { knowledge: mode }),
    agent: (name) => open('agents', name),
    agentPart: (part) => open('agents', null, { agentPart: part }),
    region: (previous) => focusRegion(document, previous),
    // A record's act by its page's own press: the session's through `sessionActs.ts`, the quest's by its page.
    sessionAct: (act) => { offers.run(act); },
    questAct: (act) => { offers.run(act); },
    help: openHelp,
    quickAsk: () => askQuickly(),
    changeFocus,
    setup: () => openSettings('start'),
    shortcuts: () => setShortcuts(true),
    // Settings → Driver, where the install's update is: nothing here checks a release channel.
    update: () => openSettings('updates', 'update'),
    about: () => setAbout(true),
  };
  const translate = (key: string, values?: Record<string, unknown>) => (values ? t(key, values) : t(key));
  const entries = commandTable(commandState(), commandDoors, translate);
  // The menus as the bar draws them; a browser has no Terminal, and claims no Alt, so shows no letter.
  const barMenus: BarMenu[] = MENUS.filter((menu) => !menu.shell || attached).map((menu) => ({
    id: menu.id,
    label: t(menu.label),
    ...(attached ? { letter: menu.letter } : {}),
    items: menuRows(entries, menu.id),
  }));
  const onMenu = (_menu: string, item: string) => {
    const entry = entries.find((each) => each.id === item);
    if (!entry) return;
    logEvent('command.run', { command: loggedId(entry.id) });
    entry.run();
  };
  return { commandState, commandDoors, translate, copyText, offers, barMenus, onMenu, entries, setupCount };
}
