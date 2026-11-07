import type * as React from 'react';
import { OverviewView } from '../../overview/OverviewView';
import { MapView } from '../../map/MapView';
import { useSettingsLayout, type SettingsAnchor } from '../../settings/SettingsView';
import { type ViewLayout } from '../../work/frame/ViewFrame';
import { ViewMain } from '../../work/frame/ViewMain';
import { type View } from '../../menus/commands';
import { useContextDoors } from './useContextDoors';
import { type Notify } from '../../design/atoms/Toasts';
import { type StarterDoor } from '../../help/starters';
import { type BrowserDriver } from '../../work/strip/browserDrivers';
import { type TFunction } from 'i18next';
import { type OpenPart } from '../opener';
import { type UseMutationResult } from '@tanstack/react-query';
import { type Confirmation } from '../../person/Confirmations';
import { type NotificationsAt } from '../../notifications/NotificationsView';
import { type RefreshResponse } from '../../data/generated/service';

export function useViewLayouts({ notify, settingsSection, chooseSettings, settingsAnchor, setSettingsAnchor, settingsGitHost, setSettingsGitHost, go, attached, askSetup, driving, openInWork, plugins, quests, projects, knowledge, agents, t, openings, setSearchAsked, open, askQuickly, openBrowser, copyText, view, openQuest, presses, showNotifications, onRefresh, refresh, mapCode }: {
  notify: Notify;
  settingsSection: "git" | "start" | "appearance" | "ai" | "wiring" | "driver" | "tools" | "browser" | "logs";
  chooseSettings: (section: "git" | "start" | "appearance" | "ai" | "wiring" | "driver" | "tools" | "browser" | "logs") => void;
  settingsAnchor: SettingsAnchor | null;
  setSettingsAnchor: React.Dispatch<React.SetStateAction<SettingsAnchor | null>>;
  settingsGitHost: string | null;
  setSettingsGitHost: React.Dispatch<React.SetStateAction<string | null>>;
  go: (door: StarterDoor) => void;
  attached: boolean;
  askSetup: (message: string) => void;
  driving: BrowserDriver[];
  openInWork: (session: string) => void;
  plugins: ViewLayout;
  quests: ViewLayout;
  projects: ViewLayout;
  knowledge: ViewLayout;
  agents: ViewLayout;
  t: TFunction<"translation", undefined>;
  openings: React.RefObject<number>;
  setSearchAsked: React.Dispatch<React.SetStateAction<{ text: string; id: number; } | null>>;
  open: (target: View, item?: string | null | undefined, part?: OpenPart | undefined) => void;
  askQuickly: (question?: string | undefined, draft?: boolean) => void;
  openBrowser: UseMutationResult<{ opened: boolean; windows: string[]; }, Error, string | undefined, unknown>;
  copyText: React.RefObject<(text: string) => void>;
  view: View;
  openQuest: (id: string) => void;
  presses: Confirmation[];
  showNotifications: (at?: Omit<NotificationsAt, "n"> | undefined) => void;
  onRefresh: () => void;
  refresh: UseMutationResult<RefreshResponse, Error, void, unknown>;
  mapCode: string | null;
}) {
  // Settings on the frame: its domains are its list pane and the domain chosen its main area.
  const settings = useSettingsLayout({
    notify,
    section: settingsSection,
    onSection: chooseSettings,
    anchor: settingsAnchor,
    onAnchored: () => setSettingsAnchor(null),
    gitHost: settingsGitHost,
    onGitHostClosed: () => setSettingsGitHost(null),
    onGo: go,
    // Ask Daoris is the shell's: a browser's guide offers no hand-off.
    onAskSetup: attached ? askSetup : undefined,
    // Who is driving Daoris's browser, for its domain, each a door into Sessions.
    browserDrivers: driving,
    onAttend: attached ? openInWork : undefined,
  });

  /**
   * Every view but Sessions, as it hands itself to the frame: its list pane where it has one, and
   * its main area, in a shell's frame beside the side bar and the panel, and in a browser's without them.
   * Plugins was built on the frame, and Quests, Repositories, Settings,
   * Convergence and Search (FRAME1f, Knowledge since UX6i) moved onto it: each hands its list and its pages whole.
   * Overview and Map have no list (§4), and their page is their main area.
   */
  const listedLayouts: Partial<Record<View, ViewLayout>> = { plugins, quests, projects, knowledge, agents, settings };
  const { contextDoors } = useContextDoors({ notify, t, openings, setSearchAsked, open, attached, askQuickly, openBrowser, copyText });
  const renderView = (): ViewLayout => listedLayouts[view] ?? {
    main: (
      // No cap: content follows the window (UX5 U59), as the session's centre does since U16. It was 72rem,
      // and a maximized window left every view a third empty. Every block wraps at the column's edge, prose
      // included, and a form keeps its own size. The main area is the container a view's split
      // follows (§3b).
      <ViewMain>
        {view === 'overview' && (
          <OverviewView
            onNavigate={(target) => open(target)}
            onOpenQuest={openQuest}
            notify={notify}
            // Its summary's lines, each a door into *Notifications* at its group: the app's alone.
            presses={presses}
            onNotifications={attached ? (group) => showNotifications({ group }) : undefined}
            onRefresh={onRefresh}
            refreshing={refresh.isPending}
          />
        )}
        {view === 'map' && (
          <MapView
            // Drawn anew on a code map a door names, so the door's repository is what it opens on.
            key={mapCode ?? ''}
            code={mapCode}
            notify={notify}
            onOpenConvergence={() => open('knowledge', null, { knowledge: 'convergence' })}
            onOpenQuest={openQuest}
            onOpenRepository={(repository) => open('projects', repository)}
            onOpenRepositories={() => open('projects')}
          />
        )}
      </ViewMain>
    ),
  };
  return { renderView, contextDoors };
}
