import { useCallback, useRef, useState } from 'react';
import { useListPanes } from '../../work/frame/listPanes';
import { type Compose, type OpenGroup } from '../opener';
import { useKnowledgeMode } from '../../knowledge/KnowledgeView';
import { type ProjectTab, readProjectTab, readWorkspaceTab, storeProjectTab, storeWorkspaceTab, type WorkspaceSection, type WorkspaceTab } from '../../projects/tabs';
import { type SettingsAnchor, type SettingsSection } from '../../settings/SettingsView';
import { type FrameIntent } from '../../menus/commands';
import { useMenuFold } from '../../work/strip/AppMenu';
import { rememberedDomain } from '../../settings/domains';
import { useAgentsMode } from '../../agents/AgentsView';
import { type AgentPart } from '../../agents/agents';
import { type Tab, rememberedView } from '../shellHelpers';

export function useShellState() {
  const [tab, setTab] = useState<Tab>(rememberedView);
  // What each view's list remembers: its closing, its width, its chosen item, its filters.
  const lists = useListPanes();
  const attending = lists.pane('sessions').chosen;
  // Knowledge's mode: Search or Convergence, remembered for the place. Held here, where every door is
  // applied, since a door into either names it.
  const [knowledgeMode, chooseKnowledgeMode] = useKnowledgeMode(lists);
  // The Agents place's mode: Agents or Roles, remembered for the place and held here for the same reason.
  const agentsModes = useAgentsMode(lists);
  // A domain remembered from before it left Settings reads as the one Settings opens on, so Ask Daoris is told the domain
  // shown: Plugins as Driver, which keeps its folder's row; Agents, Workspace and Permissions as Appearance.
  const settingsSection: SettingsSection = rememberedDomain(lists.pane('settings').chosen);
  // The part of a Settings domain a menu item named, brought into view once it is drawn.
  const [settingsAnchor, setSettingsAnchor] = useState<SettingsAnchor | null>(null);
  // And the host a door into Settings → Git named, whose add form it opens filled, until that form closes.
  const [settingsGitHost, setSettingsGitHost] = useState<string | null>(null);
  // And the part of an agent's page a door named: its accounts, what it may do, its usage.
  const [agentPart, setAgentPart] = useState<AgentPart | null>(null);
  // And the group of Sessions' or Quests' list a door into what waits on the person named: brought into view once
  // the list has answered, then let go.
  const [listGroup, setListGroup] = useState<OpenGroup | null>(null);
  // A quest the review asked for: the repository whose work is being sent back, handed
  // to the composer as an opening draft. Held here because the door crosses two views.
  const [opening, setOpening] = useState<{ from?: string; to?: string } | null>(null);
  // The Workspace menu's *Add repository…*, an event Repositories consumes, like the draft above.
  const [addRequested, setAddRequested] = useState(false);
  // And its *Import a folder…*: the import drawer, which names the workspace it lands in.
  const [importRequested, setImportRequested] = useState(false);
  // And the workspace either opens filled with, where Ask Daoris's go named one: consumed with the event.
  const [drawerWorkspace, setDrawerWorkspace] = useState<string | null>(null);
  // The composer a go to New conversation opens filled: an event the frame consumes once by its id, as it does `workIntent`.
  const [compose, setCompose] = useState<(Compose & { id: number }) | null>(null);
  const composes = useRef(0);
  // A repository's page's tab, remembered for the view: a door into a repository's own value names
  // Setup, so it is held here, where every door is applied.
  const [projectTab, setProjectTab] = useState<ProjectTab>(readProjectTab);
  const chooseProjectTab = useCallback((next: ProjectTab) => {
    setProjectTab(next);
    storeProjectTab(next);
  }, []);
  // A workspace's page's tab, remembered apart, and the Setup section a door asked for: a door into a
  // workspace's remote or its defaults names both, so they are held here too.
  const [workspaceTab, setWorkspaceTab] = useState<WorkspaceTab>(readWorkspaceTab);
  const chooseWorkspaceTab = useCallback((next: WorkspaceTab) => {
    setWorkspaceTab(next);
    storeWorkspaceTab(next);
  }, []);
  const [workspaceSection, setWorkspaceSection] = useState<WorkspaceSection | null>(null);
  // A landed branch's page a door asked for, with how it landed open: an event Repositories consumes.
  const [branchAsked, setBranchAsked] = useState<{ repository: string; branch: string } | null>(null);
  // The repository whose code map a door opened the Map on: a repository's page names it. The Map
  // goes one level in on it, and back to the workspace from there by its own door.
  const [mapCode, setMapCode] = useState<string | null>(null);
  // The palette asked for the ask composer — an event Quests consumes, like the two above. A quest or an
  // ask a door names is Quests' chosen item, and its page is the main area's.
  const [asking, setAsking] = useState(false);
  // The palette, and what it asks the Work frame to do. Both are events consumed on arrival
  // rather than state, for the reason the quest composer's opening draft is.
  const [palette, setPalette] = useState(false);
  // The application's own card. It is where the NAME lives now that the strip carries only the mark.
  const [about, setAbout] = useState(false);
  // Help › Keyboard shortcuts: every key the table holds, by menu.
  const [shortcuts, setShortcuts] = useState(false);
  // The menu bar's open menu, which moves with the pointer once one is open, and whether Alt is held, which
  // shows each menu's letter.
  const [openMenu, setOpenMenu] = useState<string | null>(null);
  const [mnemonics, setMnemonics] = useState(false);
  // Whether the window is narrower than the seven names need, so they fold into one menu.
  const foldMenus = useMenuFold();
  // What a menu, the palette or a key asked the Work frame to do with the session attended there: an event.
  const [workIntent, setWorkIntent] = useState<FrameIntent | null>(null);
  return { tab, knowledgeMode, lists, setListGroup, chooseKnowledgeMode, agentsModes, setSettingsAnchor, setSettingsGitHost, setAgentPart, setDrawerWorkspace, composes, setCompose, setAddRequested, setImportRequested, chooseProjectTab, setBranchAsked, chooseWorkspaceTab, setWorkspaceSection, setMapCode, setTab, agentPart, attending, setWorkIntent, setAsking, setOpening, setPalette, setShortcuts, setAbout, openMenu, setMnemonics, setOpenMenu, opening, asking, listGroup, projectTab, workspaceTab, workspaceSection, addRequested, importRequested, drawerWorkspace, branchAsked, settingsSection, settingsAnchor, settingsGitHost, mapCode, mnemonics, foldMenus, workIntent, compose, about, shortcuts, palette };
}
