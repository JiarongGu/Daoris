
import { useEffect, useLayoutEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useQuests, useRefreshIndex, useRepositories, useStatus } from '../data/queries';
import { NotificationsBell } from '../notifications/NotificationsBell';
import { NotificationsView } from '../notifications/NotificationsView';
import { messagesCount } from '../notifications/Messages';
import { AttentionBand } from '../work/attention/AttentionBand';
import { AskDaoris } from '../help/AskDaoris';
import { QuickAsk } from '../help/QuickAsk';
import { ContextMenus } from '../menus/ContextMenu';
import { MainOffer } from '../menus/mainOffer';
import { itemOf } from '../help/where';
import { listToggled } from '../work/frame/layout';
import { viewsIn } from '../work/frame/placements';
import { type LayoutRegion, LayoutToggles } from '../work/strip/LayoutToggles';
import { planOpening, workspaceItem } from './opener';
import { ruleDoor } from './rulePlace';
import { WorkspaceSwitcher } from './WorkspaceSwitcher';
import { Drawer, failure, Prose, Toasts, useToasts } from '../design/ui';
import { ShellSignals } from './ShellSignals';
import { logEvent, pageShows, useAttached, useDriver } from '../bridge/shell';
import { NotificationsStanding } from '../notifications/NotificationsStanding';
import { AdoptionNotice } from '../person/Confirmations';
import { UpdateBanner } from '../update/UpdateBanner';
import { LinkOpener } from '../design/links';
import { BrowserDoor } from '../work/strip/BrowserDoor';
import { ReviewChip } from '../work/gates/ReviewChip';
import { newestSetUp, setUpRef } from '../work/gates/review';
import { KeyboardShortcuts } from '../work/strip/KeyboardShortcuts';
import { SyncStatus } from '../work/frame/SyncStatus';
import { WorkFrame } from '../work/WorkFrame';
import { ViewFrame } from '../work/frame/ViewFrame';
import { ActivityBar, AppStrip, StatusBar } from '../work/frame/frame';
import { useWindowChrome } from './windowChrome';
import { useSplashLift } from './splash';
import { type Command, listOf, paletteCommands, shortcutGroups, VIEWS } from '../menus/commands';
import { CommandPalette } from '../work/strip/CommandPalette';
import { CommandCenter } from '../work/strip/CommandCenter';
import { AppMenuBar } from '../work/strip/AppMenu';
import { useZoom, zoomBy } from '../design/zoom';
import { useZoomWheel } from './windowZoom';
import { ZoomReadout } from '../work/frame/ZoomReadout';
import { figure } from '../locales/format';
import { HarnessRuns } from '../agents/harnessRuns';
import { type Tab, NAV, placeMarks, isListed, loggedId } from './shellHelpers';
import { useAttentionDoors } from './shell/useAttentionDoors';
import { useWorkViews } from './shell/useWorkViews';
import { useDoors } from './shell/useDoors';
import { useShortcuts } from './shell/useShortcuts';
import { useCommands } from './shell/useCommands';
import { useSideViews } from './shell/useSideViews';
import { useOpener } from './shell/useOpener';
import { useShellFigures } from './shell/useShellFigures';
import { useShellFeeds } from './shell/useShellFeeds';
import { useFrameDoors } from './shell/useFrameDoors';
import { useShellState } from './shell/useShellState';
import { useViewLayouts } from './shell/useViewLayouts';
import { useScopeAndRefresh } from './shell/useScopeAndRefresh';

/**
 * The strip's layout toggles, which leave it below the width the seven menus and the caption room need (measured
 * on the stories at 680 px and above).
 */
const TOGGLES_ROOM = 'flex items-center max-[54rem]:hidden';

/** A palette command that says it ran, into the machine log: by its id, never the text typed. */
const counted = (command: Command): Command => ({
  ...command,
  run: () => {
    logEvent('command.run', { command: loggedId(command.id) });
    command.run();
  },
});

/**
 * The platform: the person's window over the family, landing on management, wearing the
 * console shell, on properly-tooled foundations. Doctrine stays unwritable from every
 * view; the service's sentences render verbatim; UI chrome speaks the active language.
 */
export function App() {
  const { t, i18n } = useTranslation();
  const { tab, knowledgeMode, lists, setListGroup, chooseKnowledgeMode, agentsModes, setSettingsAnchor, setSettingsGitHost, setAgentPart, setDrawerWorkspace, composes, setCompose, setAddRequested, setImportRequested, chooseProjectTab, setBranchAsked, chooseWorkspaceTab, setWorkspaceSection, setMapCode, setTab, agentPart, attending, setWorkIntent, setAsking, setOpening, setPalette, setShortcuts, setAbout, openMenu, setMnemonics, setOpenMenu, opening, asking, listGroup, projectTab, workspaceTab, workspaceSection, addRequested, importRequested, drawerWorkspace, branchAsked, settingsSection, settingsAnchor, settingsGitHost, mapCode, mnemonics, foldMenus, workIntent, compose, about, shortcuts, palette } = useShellState();
  const { toasts, notify, dismiss } = useToasts();

  // The window this page is the chrome of. In a browser `present` is false and the strip is
  // simply a strip: no caption room, no drag, nothing to command.
  const chrome = useWindowChrome();
  // And its splash, held until this first screen has its data.
  useSplashLift();

  const status = useStatus();
  const repositories = useRepositories();
  // Quests' running mark, a quest taken, shares the Quests view's cache — one fetch, two readers.
  const outstanding = useQuests(null, false);
  const refresh = useRefreshIndex();
  // The same "is a shell here" answer every control uses — one detection path, not two that drift.
  // A shell whose driver has not answered yet is still a shell: it waits, never reading as a browser.
  const driver = useDriver();
  const attached = useAttached();
  // The window's zoom: the content scales, the frame keeps its size. A shell's alone: a browser
  // zooms its page itself, so it is left its wheel and its keys.
  const zoom = useZoom();
  useZoomWheel(attached);
  const { onSessions, closings, listMode, askSetup, listShown, placements, openHelp, askQuickly, searchAsked, setSearchAsked, openings, helpOpening, setHelpOpening, helpFocus, setListMode, quick, setQuick, quickOpening, setQuickOpening } = useFrameDoors();
  const { attention, presses, askRecords, serviceRead, notificationsOpen, showNotifications, running, remotes, roster, openWindow, openBrowser, messages, markNoticesRead, linkOpener, notices, setNotificationsToggle, showReviewAgain, reviewActs, update, sayUpdate, dismissUpdate, unkeyedHost, unkeyedSaid, setUnkeyedSaid, answerConfirmation, pressShown, notificationsAt, setNotificationsOpen, dismissNotice, markChannel, notificationsShow, notificationsToggle } = useShellFeeds({ notify });

  // Sessions does not exist in a browser: no stream, no tree path, nothing honest to show. A
  // remembered `sessions` where no shell answers falls back rather than rendering an empty view, and so does
  // any shell-only view a door asked for: Plugins too, which are this machine's.
  const view: Tab = !attached && VIEWS.some((entry) => entry.view === tab && entry.shellOnly) ? 'overview' : tab;
  onSessions.current = view === 'sessions';

  // What is used, and what never is: each view the person lands on, into the machine log — once
  // the shell has answered, so a relaunch into Sessions is not first logged as the Overview it stands in for.
  const settling = driver.isLoading;
  // Knowledge says its mode too, so the log still tells Search's use from Convergence's, as their views did.
  const loggedMode = view === 'knowledge' ? knowledgeMode : null;
  useEffect(() => {
    if (!settling) logEvent('view.opened', loggedMode ? { view, mode: loggedMode } : { view });
  }, [view, loggedMode, settling]);
  // And which view a render failure's line names: said at the commit that shows it, settled or not, before a
  // failure in the same commit's effects reaches the page's error screen.
  useLayoutEffect(() => pageShows(view), [view]);

  // Whether the view in front has a list pane, whose four doors — the strip's toggle, the View
  // menu's item, Ctrl+B and a press on its place — are absent where it has none, never disabled. A browser
  // keeps a view's list too; Sessions, the one view that needs a shell, is never in front there.
  const listed = isListed(view);

  // A region toggled: the side bar and the panel are the frame's on every view, and
  // the list is the view's own, so where the view has none its key does nothing. True when it did. A
  // browser has the view's list and never the side bar or the panel (they hold this machine's sessions).
  const toggleRegion = (region: LayoutRegion): boolean => {
    if (!attached && region !== 'list') return false;
    if (region === 'right') closings.setDock(!closings.dock);
    else if (region === 'panel') closings.setPanel(!closings.panel);
    else if (isListed(view) && listMode) {
      // By what the room made of it: an open list closes, one laid over goes, and a strip opens — over
      // the main area where the window drew it. The closing is this view's own, and one laid
      // over leaves it unchanged. A go's group still on its way goes with it, since the frame lays the list over
      // for it.
      const next = listToggled({ mode: listMode });
      if (next.closed !== undefined) lists.setClosed(view, next.closed);
      closings.setListOver(next.over);
      setListGroup(null);
    }
    else return false;
    return true;
  };
  const { scope, workspaces, holdings, onRefresh, scopeNamed } = useScopeAndRefresh({ t, refresh, notify });
  const { machineRegistry, circle, wired, onSyncNow, syncNow, driving, announcements, waiting, pressUnshown, placed, sessionsRunning, questsTaken, needsRows, working, pressNames, messageNames, presence, workingSaid, standing, everything, indexed } = useShellFigures({ repositories, driver, attached, attention, presses, t, outstanding, scope, workspaces, askRecords, serviceRead, notificationsOpen, showNotifications, running, remotes, notify });
  const { open, openSettings, apply, chooseSettings } = useOpener({ machineRegistry, chooseKnowledgeMode, agentsModes, lists, view, knowledgeMode, setSettingsAnchor, setSettingsGitHost, setAgentPart, setListGroup, setDrawerWorkspace, composes, setCompose, setAddRequested, setImportRequested, chooseProjectTab, setBranchAsked, chooseWorkspaceTab, setWorkspaceSection, setMapCode, closings, setTab, circle });
  const { plugins, agents } = useSideViews({ view, lists, notify, attached, askSetup, agentsModes, agentPart, setAgentPart, open });
  const { commandState, commandDoors, translate, copyText, offers, barMenus, onMenu, entries, setupCount } = useCommands({ tab, openSettings, running, attending, lists, attached, view, listed, listShown, closings, placements, holdings, scope, circle, wired, i18n, roster, knowledgeMode, refresh, zoom, open, setWorkIntent, setAsking, setOpening, onSyncNow, notify, t, setPalette, toggleRegion, showNotifications, openWindow, openBrowser, onRefresh, openHelp, askQuickly, setShortcuts, setAbout });
  useShortcuts({ commandState, commandDoors, translate, openMenu, attached, setMnemonics, setOpenMenu });
  const { openInWork, openRun, openAtWork, openQuest, openAsk, go, messageAnnouncements, setAttending, openAnswer } = useDoors({ lists, open, setWorkIntent, t, messages, view, attending, markNoticesRead, openHelp, apply, circle });
  const { quests, projects, knowledge } = useWorkViews({ lists, view, notify, attached, openInWork, openRun, openAtWork, opening, setOpening, asking, setAsking, listGroup, setListGroup, projectTab, chooseProjectTab, workspaceTab, chooseWorkspaceTab, workspaceSection, open, onSyncNow, syncNow, addRequested, setAddRequested, setDrawerWorkspace, importRequested, setImportRequested, drawerWorkspace, openQuest, openAsk, branchAsked, setBranchAsked, knowledgeMode, status, searchAsked, setSearchAsked });
  // What Ask Daoris is handed wherever it stands: what is on the screen — the view, the scope,
  // the settings domain on Settings, and the attended session on Sessions — and its two ways out.
  const itemList = { quests, projects, plugins, agents }[view as 'quests' | 'projects' | 'plugins' | 'agents']?.list;
  const askProps = {
    where: {
      view, workspace: scope.workspace ?? null, settings: settingsSection, knowledge: knowledgeMode,
      agents: attached ? agentsModes.mode : null,
      // The item the view in front has chosen, while its list says it is still there.
      item: itemOf(view, itemList?.chosen, itemList?.standing),
      // Where Sessions' views stand: the helper cannot see the window, and guessed without it.
      layout: {
        right: viewsIn(placements.places, 'right'),
        panel: viewsIn(placements.places, 'panel'),
        rightShown: !closings.dock,
        panelShown: !closings.panel,
      },
    },
    attending,
    // Unframed in the side bar, whose own close is the region's; nothing here closes on its own.
    onClose: () => closings.setDock(true),
    onGo: go,
  };
  const { attentionDoors } = useAttentionDoors({ attached, openInWork, openAsk, openQuest, open, setWorkIntent, showNotifications, openSettings });
  const { renderView, contextDoors } = useViewLayouts({ notify, settingsSection, chooseSettings, settingsAnchor, setSettingsAnchor, settingsGitHost, setSettingsGitHost, go, attached, askSetup, driving, openInWork, plugins, quests, projects, knowledge, agents, t, openings, setSearchAsked, open, askQuickly, openBrowser, copyText, view, openQuest, presses, showNotifications, onRefresh, refresh, mapCode });

  return (
    // A tool's running action — a sign-in above all — outlives the view it started on.
    <HarnessRuns notify={notify}>
    {/* Every link on the page opens through one place, told here where to. */}
    <LinkOpener.Provider value={linkOpener}>
    {/* What the main area's page offers, which the menu bar's record groups read. */}
    <MainOffer.Provider value={offers}>
    {/* A window, not a page: the viewport IS the frame, every region scrolls inside it, and
        the status bar is therefore always in the same place. Page scrolling would put the output panel
        below the fold exactly when a session is producing output. */}
    <div className="flex h-screen flex-col overflow-hidden">
      {/* First, as in the secondary root: its effects run before any view's, so the driver's state and the
          lines are read as the snapshot it asks and never pulled beside it. A notification is a door: clicking the OS
          balloon lands on that session rather than on whatever is open. It renders nothing. */}
      <ShellSignals
        notify={notify}
        onAttend={openInWork}
        attended={view === 'sessions' ? attending : null}
        onConfirming={announcements.heard}
        onMessage={messageAnnouncements.heard}
      />
      {/* The application's one global row. It holds what is true everywhere, which is exactly
          why none of it belongs in a sidebar owned by one view. It is the window's own
          title bar. */}
      <AppStrip
        captionRoom={chrome.present}
        stripRef={chrome.stripRef}
        onDragStart={chrome.present ? chrome.onDragStart : undefined}
        onToggleMaximize={chrome.present ? chrome.onToggleMaximize : undefined}
        onResizeTop={chrome.present ? chrome.onResizeTop : undefined}
        onSystemMenu={chrome.present ? chrome.onSystemMenu : undefined}
        // 🔴 The APPLICATION's menus, which is what a title bar holds in an IDE, and they are
        // its verbs and places, as VS Code's bar holds them: Workspace, Edit, View, Go, Run, Terminal,
        // Help, every row a row of the one table (`commands.ts`) that the palette and the keys read too.
        // The activity bar is still the one navigation region; Go is its keyboard twin.
        menus={(
          <AppMenuBar
            label={t('menu.bar')}
            menus={barMenus}
            open={openMenu}
            onOpen={setOpenMenu}
            onChoose={onMenu}
            mnemonics={mnemonics}
            // Narrower than the seven names need, they fold into one menu, which the keys reach as they reach the bar.
            compact={foldMenus}
            foldLabel={t('menu.fold')}
          />
        )}
        // The palette's way in is the command center now, not a 14px glyph wedged against the
        // caption buttons — same dialog, a target a person can find.
        center={(
          <CommandCenter
            scope={scopeNamed ?? t('palette.scope')}
            shortcut="Ctrl K"
            onOpen={() => setPalette(true)}
            label={t('palette.open')}
          />
        )}
        // The region toggles at the strip's right, beside the window controls, as VS
        // Code's sit: the panel and the right side bar on every view, and the view's list where
        // it has one, named for it. Ask Daoris has no button of its own up here: it is a tab of
        // the side bar, so the right toggle, F1 and Ctrl+Alt+I are its doors. Before them, Daoris's browser:
        // an act of the application's, one press from every view.
        // 🔴 On a narrow window the toggles leave the strip before any menu does: each is View's,
        // with its key, and seven menus beside a command center reduced to its glyph need the room.
        trailing={attached ? (
          <div className="flex items-center gap-2">
            {/* *Notifications*: first of the trailing group, before the browser's door, and on a narrow window
                too, since it is how anything that came to the person is found. */}
            <NotificationsBell
              needs={waiting}
              presses={presses.length}
              warn={pressUnshown}
              unread={notices.some((notice) => !notice.read) || messagesCount(messages.asks ?? [], messages.answered).items > 0}
              onPress={() => setNotificationsToggle((was) => was + 1)}
            />
            <BrowserDoor onOpen={() => openBrowser.mutate()} drivers={driving} onAttend={openInWork} />
            {/* A set-up waiting for the person's review: said beside the browser, never inside it, which an
                agent drives. */}
            <ReviewChip
              // Each with the newest set-up its step's record holds, which *Reviewed* names.
              reviews={driver.data?.inReview?.map((row) => ({
                ...row,
                setUp: setUpRef(newestSetUp(outstanding.data?.find((quest) => quest.id === row.quest) ?? {})),
              }))}
              onShowAgain={(quest) => showReviewAgain.mutate({ quest }, {
                onSuccess: () => notify(t('browser.review.shownAgain', { quest })),
                onError: failure(notify),
              })}
              onReviewed={(quest, setUp) => reviewActs.actsFor({ state: 'shown', step: quest }).reviewed?.(setUp, {
                done: () => {},
                refused: (said) => notify(said, 'error'),
              })}
              // A not yet needs words, which the step's page asks for: a menu holds no box.
              onNotYet={(quest) => openQuest(quest)}
            />
            <div className={TOGGLES_ROOM}>
              <LayoutToggles
                regions={['list', 'panel', 'right']}
                list={listed ? t(`layout.list.${listOf(view, knowledgeMode)}`) : undefined}
                closed={{ list: !listShown, panel: closings.panel, right: closings.dock }}
                onToggle={toggleRegion}
              />
            </div>
          </div>
        ) : listed ? (
          // A browser's strip holds the list's toggle alone: it has no side bar and no panel.
          <div className={TOGGLES_ROOM}>
            <LayoutToggles
              regions={['list']}
              list={t(`layout.list.${listOf(view, knowledgeMode)}`)}
              closed={{ list: !listShown, panel: true, right: true }}
              onToggle={toggleRegion}
            />
          </div>
        ) : undefined}
      />

      {/* The install's update, under the strip and above every view: quiet, one line, and only while
          there is something to say. The shell's alone: a browser has no install to update. */}
      {attached && (
        <UpdateBanner
          update={update.data}
          busy={sayUpdate.isPending || dismissUpdate.isPending}
          onSay={(mode) => sayUpdate.mutate(mode, { onError: failure(notify) })}
          onDismiss={() => dismissUpdate.mutate(undefined, { onError: failure(notify) })}
        />
      )}

      {/* The adoption notice, said once under the strip: about the window's own host. What waits for the person's
          Confirm is drawn in *Notifications* and nowhere across the page. */}
      {attached && unkeyedHost && !unkeyedSaid && <AdoptionNotice onDismiss={() => setUnkeyedSaid(true)} />}

      {/* No narrow-window stacking. A wide sidebar has to become a top bar under
          768px — a 48px icon rail does not, and stacking it is actively wrong: it puts a
          276px-tall column of icons ABOVE the content and leaves the page 105px, measured in a real
          browser at 686px. One layout at every width, which is also what keeps "a window, not
          a page" true on a narrow screen instead of only on a wide one. */}
      <div className="flex min-h-0 flex-1">
        {/* The application's one navigation: every view is one click from every other, and
            nothing is gated behind a mode. Its foot holds Settings alone: the state
            lives in the status bar, and the two actions, refreshing the index and the
            language, in View, the palette and Settings → Appearance. */}
        <ActivityBar
          label={t('nav.label')}
          items={NAV.filter(({ shellOnly }) => !shellOnly || attached).map(({ view: target, icon }) => ({
            tab: target,
            label: t(`nav.${target}`),
            icon,
            // 🔴 A place's number is what waits on the person there and nothing else, never a count of the sessions the
            // driver runs or of the outstanding quests: the bell's rows its door opens, in the
            // bell's hue. Running work is its mark, never a number; Overview holds no wait of its own, its lines being doors.
            ...placeMarks(target, placed[target as keyof typeof placed] ?? 0,
              target === 'sessions' ? sessionsRunning : target === 'quests' ? questsTaken : 0, t),
            // Its key from the table, as Go prints it; none in a browser, which keeps Ctrl+1–8.
            keys: entries.find((entry) => entry.id === `go.${target}`)?.keys[0]?.combo,
          }))}
          // Settings is everywhere now — a browser has appearance to set, if nothing of a machine.
          end={[{
            tab: 'settings', label: t('nav.settings'), icon: 'settings',
            // A volume below its floor waits on the person at This Daoris's *Disk space* card.
            ...placeMarks('settings', placed.settings ?? 0, 0, t),
            keys: entries.find((entry) => entry.id === 'workspace.settings')?.keys[0]?.combo,
          }]}
          active={view}
          onSelect={(target) => open(target)}
          // The list's fourth door: the place you are on, pressed again, toggles its list.
          onToggleCurrent={listed ? () => { toggleRegion('list'); } : undefined}
        />

        {/* 🔴 ONE frame on every view: the right side bar and the panel stay whatever the
            centre shows, as VS Code's workbench does. One element in one place, so what is open, selected and sized survives a change
            of view. Sessions' list and main area are its own; every other view hands in its layout. A browser
            keeps the view's list and main area and never the side bar or the panel, which hold this machine's
            sessions. */}
        {attached
          ? (
            <WorkFrame
              selected={attending}
              onSelect={setAttending}
              notify={notify}
              intent={workIntent}
              onIntentTaken={() => setWorkIntent(null)}
              compose={compose}
              // The composer's quiet press: its draft in Quick Ask, for the person to send.
              onMakeTask={(text, focus) => askQuickly(text, true, focus)}
              // Sending work back is publishing a request, which is the platform's own door — so
              // this goes to the composer rather than growing a second one here.
              onSendBack={(repository) => {
                setOpening({ from: repository });
                open('quests');
              }}
              // A parked intake's answer is on its ask: the same door the band's ask rows use.
              onAnswerAsk={openAsk}
              // A quest on the chain, or one the session asked, opens where quests are read.
              onOpenQuest={openQuest}
              // How a landed branch landed: the work's milestone and the run's landing step open its page in Repositories.
              onOpenBranch={(repository, branch) => open('projects', repository, { branch })}
              // Ask Daoris as a view of the frame's regions: one right region, never a second column.
              ask={<AskDaoris {...askProps} framed={false} opening={helpOpening} onOpened={() => setHelpOpening(null)} />}
              askFocus={helpFocus}
              // *Notifications* as a view of the frame's regions: the presses of both sources as their cards, then
              // What needs you; shown where it stands by the bell and every door into it.
              notifications={(
                <NotificationsView
                  presses={presses}
                  needs={attention.state === 'known' ? needsRows : null}
                  working={working}
                  attention={<AttentionBand inView doors={attentionDoors} notify={notify} onSessions={() => open('sessions')} onRun={openRun} />}
                  onAnswer={(waiting, confirm) => answerConfirmation.mutateAsync({ waiting, confirm })}
                  notify={notify}
                  onShown={pressShown}
                  update={attached ? update.data : undefined}
                  onSayUpdate={(mode) => sayUpdate.mutate(mode, { onError: failure(notify) })}
                  names={pressNames}
                  // A press's door to its plan: the plan's own page, scoped as the press is; a land's at its review.
                  onOpenPlan={(door) => {
                    const plan = planOpening(door, circle, (repository) =>
                      holdings.data?.find((each) => each.members.includes(repository))?.name ?? null);
                    apply(plan.opening);
                    if (plan.review) setWorkIntent('review');
                  }}
                  at={notificationsAt}
                  onVisible={setNotificationsOpen}
                  notices={notices}
                  onRead={(ids) => markNoticesRead.mutate(ids)}
                  onOpenNotice={(notice) => { if (notice.session) openInWork(notice.session); }}
                  onDismiss={(notice) => dismissNotice.mutate(notice.id)}
                  messages={messages.asks}
                  answered={messages.answered}
                  messageNames={messageNames}
                  onOpenAsk={(entry) => openAsk(entry.ask)}
                  onMarkAsk={(entry) => markChannel.mutate({ ask: entry.ask, through: entry.newest?.at })}
                  onOpenAnswer={(notice) => { if (notice.session) openAnswer(notice.session, notice.repository); }}
                  standing={<NotificationsStanding notify={notify} onOpenHelp={openHelp} />}
                />
              )}
              notificationsShow={notificationsShow}
              notificationsToggle={notificationsToggle}
              // The person's own shell: a frame is only drawn where a shell is attached.
              terminal
              closings={closings}
              lists={lists}
              onListMode={setListMode}
              placements={placements}
              layout={view === 'sessions' ? undefined : renderView()}
              onOpenSessions={() => open('sessions')}
              onChangeRule={(place) => {
                const door = ruleDoor(place);
                if (door) open(door.view, door.item, door.part);
              }}
              group={listGroup}
              onGroupBrought={() => setListGroup(null)}
            />
          )
          : (
            <ViewFrame
              layout={renderView()}
              lists={lists}
              over={closings.listOver}
              onOver={closings.setListOver}
              onListMode={setListMode}
              // A go's group lays Quests' strip over its main area in a browser as on the desktop.
              group={listGroup}
              onGroupBrought={() => setListGroup(null)}
            />
          )}
      </div>

      {/* Ambient truth, true on every view without being looked at. Which circle and whether
          it syncs are as true on Quests as on Sessions, and a bar that appeared and vanished with a
          view would be chrome. It also carries the tier — "stated on every screen"
          is better served by a bar that is on every screen by construction — and what the index
          holds. */}
      <StatusBar
        // The scope lives here now, not in the strip: this bar is what is TRUE, and a circle is a
        // fact about what you are looking at rather than an action.
        //
        // 🔴 Passed ONLY when there is a choice to make. `WorkspaceSwitcher` renders null below two
        // circles, so handing it over unconditionally replaced the bar's sentence with
        // nothing and the scope disappeared from the window entirely — caught by photographing the
        // bar, which is the only place it would ever have shown.
        scope={(workspaces.data?.length ?? 0) >= 2
          ? (
            <WorkspaceSwitcher
              bar
              workspaces={workspaces.data ?? []}
              value={scope.workspace}
              onChange={scope.setWorkspace}
            />
          )
          : undefined}
        driver={presence}
        sessions={workingSaid}
        workspace={scopeNamed}
        remote={wired}
        sync={circle && standing.data?.wired
          ? (
            <SyncStatus
              workspace={circle}
              standing={standing.data}
              conflicts={standing.data.conflicts.map((id) => ({
                id, title: everything.data?.find((quest) => quest.id === id)?.title,
              }))}
              syncing={syncNow.isPending}
              // The pass is the shell's: a browser reads where the circle stands and is not
              // offered a button that could not run it.
              onSyncNow={attached ? () => onSyncNow(circle) : undefined}
              onOpenQuest={openQuest}
              // Named for the part: the workspace's page at its remote, where it is wired.
              onRemotes={attached ? () => open('projects', workspaceItem(circle), { workspaceSection: 'remote' }) : undefined}
            />
          )
          : undefined}
        /* Where each fact leads, and the rule is that a status item goes where the fact is SET
           rather than where it is merely repeated. The driver and the remote are both machine wiring,
           so both land on Settings; sessions land on Sessions; the index count leads to the
           repositories it is built from.

           🔴 Handed over unconditionally and refused per-item inside the bar. A browser has no
           machine settings at all, and `driver === 'absent'` is exactly that case — the bar drops
           the target itself rather than making every caller remember to. */
        onDriver={() => openSettings('driver')}
        // Where the workspace in view syncs is its page's: at its remote on a shell, its Details in a browser.
        onRemote={() => open('projects', null, attached ? { workspaceSection: 'remote' } : { workspaceTab: 'details' })}
        onSessions={attached ? () => open('sessions') : undefined}
        onIndex={() => open('projects')}
        // Settings holds Daoris's own AI in a browser too, so the tier leads there everywhere.
        onTier={() => openSettings('ai')}
        // Where the setup is done: absent in a browser, and once the required steps are.
        setup={setupCount}
        onSetup={() => openSettings('start')}
        // The window's zoom while it is not 100%, pressed back to 100%: a shell's alone.
        zoom={attached ? { level: zoom, onReset: () => zoomBy('reset') } : undefined}
        // The tier by its value: the bar words it, and its note, in the page's language.
        tier={status.data ? { semantic: status.data.semantic } : undefined}
        indexed={indexed > 0
          ? t('sidebar.count', {
            entries: figure(indexed),
            repositories: repositories.data?.length ?? 0,
          })
          : undefined}
      />

      {/* Every action, by name. The registry is a pure function of what is true right now,
          so a browser's list and a shell's list differ by OMISSION rather than by a disabled row —
          a palette is a promise that what it lists can be done. */}
      {/* 🔴 Where the NAME lives now that the strip carries only the mark. It is also the honest
          home for it: a title bar in an IDE says what you can do, and the one place a person looks
          for "what is this and which version" is About. */}
      {about && (
        <Drawer title={t('menu.about')} onClose={() => setAbout(false)}>
          <div className="flex items-baseline gap-2">
            <strong className="font-serif text-wordmark font-semibold tracking-[-0.01em]">Daoris</strong>
            <span className="text-small text-ink-faint">道衍</span>
          </div>
          <Prose className="mt-2">{t('about.what')}</Prose>
          <dl className="m-0 mt-3 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-small">
            {/* What the index can answer with, not a version number nobody serves: `Status` carries
                the tier and no canon version, and inventing a field to fill a row is how a card
                starts lying. */}
            <dt className="text-ink-faint">{t('about.index')}</dt>
            <dd className="m-0 font-mono text-meta">{status.data?.tier ?? '—'}</dd>
            <dt className="text-ink-faint">{t('about.surface')}</dt>
            <dd className="m-0">{attached ? t('about.shell') : t('about.browser')}</dd>
          </dl>
        </Drawer>
      )}

      {/* Help › Keyboard shortcuts: the table's keys by menu, as the menus print them. */}
      {shortcuts && (
        <KeyboardShortcuts groups={shortcutGroups(entries, translate)} browser={!attached} onClose={() => setShortcuts(false)} />
      )}

      <CommandPalette
        open={palette}
        onClose={() => setPalette(false)}
        // The menus' rows that apply now, under their menu's name: one table, so a palette row is a menu row.
        commands={paletteCommands(entries, translate).map(counted)}
        // The command center's one question: the text typed, asked in Quick Ask.
        onAsk={attached ? (question) => { logEvent('command.run', { command: 'ask' }); askQuickly(question); } : undefined}
      />

      {attached && (
        <QuickAsk
          open={quick}
          onClose={() => setQuick(false)}
          onExpand={() => { setQuick(false); openHelp(); }}
        >
          <AskDaoris
            {...askProps}
            framed={false}
            opening={quickOpening}
            // The box is drawn anew each time it opens: a question still held would be asked again.
            onOpened={() => setQuickOpening(null)}
            // A starter's door leads away from the box, so the box goes with it.
            onGo={(door) => { setQuick(false); askProps.onGo(door); }}
            onClose={() => setQuick(false)}
          />
        </QuickAsk>
      )}

      {/* The zoom's level, said for a moment as it changes; the status bar keeps it while it is not 100%. */}
      {attached && <ZoomReadout level={zoom} />}

      {/* A press's toast first, staying until the view opens or the press ends; then the window's outcomes. */}
      <Toasts
        items={[...announcements.toasts, ...messageAnnouncements.toasts, ...toasts]}
        onClose={(id) => { if (!announcements.close(id) && !messageAnnouncements.close(id)) dismiss(id); }}
      />
      {/* The window's one right-click handler: each surface offers its acts, and it draws the menu. */}
      <ContextMenus doors={contextDoors} shell={chrome.present} />
    </div>
    </MainOffer.Provider>
    </LinkOpener.Provider>
    </HarnessRuns>
  );
}
