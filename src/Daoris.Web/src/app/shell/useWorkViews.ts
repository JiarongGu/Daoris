import type * as React from 'react';
import { type OpenGroup, type OpenPart, workspaceItem } from '../opener';
import { useKnowledgeView } from '../../knowledge/KnowledgeView';
import { useQuestsView } from '../../quests/QuestsView';
import { useProjectsView } from '../../projects/ProjectsView';
import { type WorkspaceSection } from '../../projects/tabs';
import { type View } from '../../menus/commands';
import { type ListPanes } from '../../work/frame/listPanes';
import { type Notify } from '../../design/atoms/Toasts';
import { type UseMutationResult, type UseQueryResult } from '@tanstack/react-query';
import { type SyncNowReport } from '../../bridge/driver';
import { type StatusResponse } from '../../data/generated/service';

export function useWorkViews({ lists, view, notify, attached, openInWork, openRun, openAtWork, opening, setOpening, asking, setAsking, listGroup, setListGroup, projectTab, chooseProjectTab, workspaceTab, chooseWorkspaceTab, workspaceSection, open, onSyncNow, syncNow, addRequested, setAddRequested, setDrawerWorkspace, importRequested, setImportRequested, drawerWorkspace, openQuest, openAsk, branchAsked, setBranchAsked, knowledgeMode, status, searchAsked, setSearchAsked }: {
  lists: ListPanes;
  view: View;
  notify: Notify;
  attached: boolean;
  openInWork: (session: string) => void;
  openRun: (session: string) => void;
  openAtWork: (session: string) => void;
  opening: { from?: string | undefined; to?: string | undefined; } | null;
  setOpening: React.Dispatch<React.SetStateAction<{ from?: string | undefined; to?: string | undefined; } | null>>;
  asking: boolean;
  setAsking: React.Dispatch<React.SetStateAction<boolean>>;
  listGroup: OpenGroup | null;
  setListGroup: React.Dispatch<React.SetStateAction<OpenGroup | null>>;
  projectTab: "branches" | "details" | "history" | "changes" | "integration";
  chooseProjectTab: (next: "branches" | "details" | "history" | "changes" | "integration") => void;
  workspaceTab: "branches" | "workflow" | "details";
  chooseWorkspaceTab: (next: "branches" | "workflow" | "details") => void;
  workspaceSection: WorkspaceSection | null;
  open: (target: View, item?: string | null | undefined, part?: OpenPart | undefined) => void;
  onSyncNow: (workspace: string) => void;
  syncNow: UseMutationResult<SyncNowReport, Error, string, unknown>;
  addRequested: boolean;
  setAddRequested: React.Dispatch<React.SetStateAction<boolean>>;
  setDrawerWorkspace: React.Dispatch<React.SetStateAction<string | null>>;
  importRequested: boolean;
  setImportRequested: React.Dispatch<React.SetStateAction<boolean>>;
  drawerWorkspace: string | null;
  openQuest: (id: string) => void;
  openAsk: (id: string) => void;
  branchAsked: { repository: string; branch: string; } | null;
  setBranchAsked: React.Dispatch<React.SetStateAction<{ repository: string; branch: string; } | null>>;
  knowledgeMode: "search" | "convergence";
  status: UseQueryResult<StatusResponse, Error>;
  searchAsked: { text: string; id: number; } | null;
  setSearchAsked: React.Dispatch<React.SetStateAction<{ text: string; id: number; } | null>>;
}) {
  // The Quests view: held on every view, as Plugins is, so what its composers hold lasts while
  // Daoris is open; its list and its main area read the list's memory, which every door into it names (§3i).
  const questsPane = lists.pane('quests');
  const quests = useQuestsView({
    active: view === 'quests',
    chosen: questsPane.chosen,
    onChoose: (item) => lists.choose('quests', item),
    filters: questsPane.filters,
    onFilters: (filters) => lists.setFilters('quests', filters),
    notify,
    onAttend: attached ? openInWork : undefined,
    onOpenRun: attached ? openRun : undefined,
    onOpenAtWork: attached ? openAtWork : undefined,
    opening,
    onOpened: () => setOpening(null),
    asking,
    onAsked: () => setAsking(false),
    group: listGroup,
    onGroupBrought: () => setListGroup(null),
  });
  // The Repositories view: held on every view, as Quests is; its list is the registry and its main
  // area the chosen repository's page, which every door into it names (§3i). Its drawers are the Workspace menu's too.
  const projects = useProjectsView({
    active: view === 'projects',
    chosen: lists.pane('projects').chosen,
    onChoose: (item) => lists.choose('projects', item),
    tab: projectTab,
    onTab: chooseProjectTab,
    workspaceTab,
    onWorkspaceTab: chooseWorkspaceTab,
    workspaceSection,
    notify,
    onOpenCode: (repository) => open('map', null, { code: repository }),
    // A workspace's Details names each agent's accounts there, set on the agent's page.
    onOpenAgent: (agent) => open('agents', agent),
    // A remote nothing serves opens Settings → Git, where an identity is set up for it.
    onOpenGit: () => open('settings', 'git'),
    // A landing rule's plugin opens its page (PLUGUI1c2c).
    onOpenPlugin: (plugin) => open('plugins', plugin),
    // A repository's setup is its place's page in the Agents place's Setup mode.
    onOpenSetup: (repository) => open('agents', null, { placeOf: repository }),
    // A workspace's setup sections have their homes on its place, which `apply` opens for a door naming one.
    onOpenWorkspaceSetup: (workspace, section) => open('projects', workspaceItem(workspace), { workspaceSection: section }),
    onSyncNow,
    syncing: syncNow.isPending,
    addRequested,
    onAddOpened: () => { setAddRequested(false); setDrawerWorkspace(null); },
    importRequested,
    onImportOpened: () => { setImportRequested(false); setDrawerWorkspace(null); },
    drawerWorkspace,
    // A workspace's clear names what it keeps, each with the page that frees it.
    onOpenQuest: openQuest,
    onOpenAsk: openAsk,
    onAttend: attached ? openInWork : undefined,
    branchAsked,
    onBranchOpened: () => setBranchAsked(null),
  });
  // Knowledge: Search and Convergence as one place, held on every view as Quests is
  // and asking the service nothing until in front. Each mode's list memory is its chosen item and its one filter, *local
  // only* and the similarity (§3f), as it was; a mode chosen at the list's head is a door into the place in that mode.
  const knowledge = useKnowledgeView({
    active: view === 'knowledge',
    mode: knowledgeMode,
    onMode: (mode) => open('knowledge', null, { knowledge: mode }),
    lists,
    notify,
    semantic: status.data?.semantic ?? false,
    handed: searchAsked,
    onHanded: () => setSearchAsked(null),
  });
  return { quests, projects, knowledge };
}
