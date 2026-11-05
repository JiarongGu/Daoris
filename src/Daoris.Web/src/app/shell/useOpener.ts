import type * as React from 'react';
import { type Compose, type OpenGroup, type Opening, type OpenPart, opening as plannedOpening, placeOpening } from '../opener';
import { DEFAULT_WORKSPACE, workspaceOf } from '../../data/workspaces';
import { type WorkspaceSection } from '../../projects/tabs';
import { type SettingsAnchor, type SettingsSection } from '../../settings/SettingsView';
import { type View } from '../../menus/commands';
import { store } from '../../lib/stored';
import { type AgentPart } from '../../agents/agents';
import { VIEW, isListed } from '../shellHelpers';
import { type UseQueryResult } from '@tanstack/react-query';
import { type RegistrationResponse } from '../../data/generated/service';
import { type PlaceAt } from '../../agents/setup/placeShell';
import { type PlaceAsk } from '../../agents/setup/placePlan';
import { type ListPanes } from '../../work/frame/listPanes';
import { type FrameClosings } from '../../work/frame/closings';

export function useOpener({ machineRegistry, chooseKnowledgeMode, agentsModes, lists, view, knowledgeMode, setSettingsAnchor, setSettingsGitHost, setAgentPart, setListGroup, setDrawerWorkspace, composes, setCompose, setAddRequested, setImportRequested, chooseProjectTab, setBranchAsked, chooseWorkspaceTab, setWorkspaceSection, setMapCode, closings, setTab, circle }: {
  machineRegistry: UseQueryResult<RegistrationResponse[], Error>;
  chooseKnowledgeMode: (mode: "search" | "convergence") => void;
  agentsModes: { mode: "agents" | "setup"; onMode: (next: "agents" | "setup") => void; place: string | null; onPlace: (next: string) => void; onPlaceAt: (next: string, at: PlaceAt) => void; arrival: { place: string; at: PlaceAt; } | null; onArrived: () => void; seeded: { place: string; ask: PlaceAsk; } | null; onSeed: (target: string, ask: PlaceAsk) => void; onSeeded: () => void; };
  lists: ListPanes;
  view: View;
  knowledgeMode: "search" | "convergence";
  setSettingsAnchor: React.Dispatch<React.SetStateAction<SettingsAnchor | null>>;
  setSettingsGitHost: React.Dispatch<React.SetStateAction<string | null>>;
  setAgentPart: React.Dispatch<React.SetStateAction<AgentPart | null>>;
  setListGroup: React.Dispatch<React.SetStateAction<OpenGroup | null>>;
  setDrawerWorkspace: React.Dispatch<React.SetStateAction<string | null>>;
  composes: React.RefObject<number>;
  setCompose: React.Dispatch<React.SetStateAction<(Compose & { id: number; }) | null>>;
  setAddRequested: React.Dispatch<React.SetStateAction<boolean>>;
  setImportRequested: React.Dispatch<React.SetStateAction<boolean>>;
  chooseProjectTab: (next: "branches" | "details" | "history" | "changes" | "integration") => void;
  setBranchAsked: React.Dispatch<React.SetStateAction<{ repository: string; branch: string; } | null>>;
  chooseWorkspaceTab: (next: "branches" | "workflow" | "details") => void;
  setWorkspaceSection: React.Dispatch<React.SetStateAction<WorkspaceSection | null>>;
  setMapCode: React.Dispatch<React.SetStateAction<string | null>>;
  closings: FrameClosings;
  setTab: React.Dispatch<React.SetStateAction<View>>;
  circle: string | null;
}) {
  /**
   * **The application's one opener** (D118 §3i): every door into a view calls it, naming the item it opens,
   * and the view's list has that item chosen (§3f). `opener.ts` plans it as a value; this applies it.
   * Settings opens at the domain a door names, at the part it names (D75, UX5 U72): the tier opens Daoris's
   * own AI, the remote Workspace, the driver Driver, where each once opened the whole page at its top.
   */
  const apply = (planned: Opening) => {
    // A door naming a repository's place is resolved to the place's id (AGENTSPLACE1f).
    const plan = placeOpening(planned, (repository) => {
      const row = (machineRegistry.data ?? []).find((each) => each.repository === repository);
      return row ? workspaceOf(row) : DEFAULT_WORKSPACE;
    });
    // Knowledge's mode first (UX6i): a switch reads that mode's remembered choice again, and an item a door names in it
    // is then chosen now, never a remembered choice.
    if (plan.knowledge) chooseKnowledgeMode(plan.knowledge);
    if (plan.agentsMode) agentsModes.onMode(plan.agentsMode);
    // SETUPUX1h: a door to a place names the section and the row it opens at.
    if (plan.place) agentsModes.onPlaceAt(plan.place, plan.placeAt ?? {});
    // Ask Daoris's place card (AGENTSPLACE1k): the place's *Set up…* opens on the ask, once the Agents place has the place chosen.
    if (plan.place && plan.setUp) agentsModes.onSeed(plan.place, plan.setUp);
    if (plan.chosen) lists.choose(plan.chosen.view, plan.chosen.item);
    // A view opened by a door that names nothing reopens what its list chose only while it still waits (UX6b, D150 §8):
    // the list reads it again, and lets go of one that closed or went. A door that names an item has it chosen above.
    // Knowledge's chosen item is its mode's list's.
    else if (plan.view !== view && isListed(plan.view)) {
      lists.reopen(plan.view === 'knowledge' ? plan.knowledge ?? knowledgeMode : plan.view);
    }
    if (plan.anchor !== undefined) setSettingsAnchor(plan.anchor);
    if (plan.gitHost !== undefined) setSettingsGitHost(plan.gitHost);
    setAgentPart(plan.agentPart ?? null);
    setListGroup(plan.group ?? null);
    if (plan.drawer) setDrawerWorkspace(plan.drawerWorkspace ?? null);
    if (plan.compose) {
      composes.current += 1;
      setCompose({ ...plan.compose, id: composes.current });
    }
    if (plan.drawer === 'add') setAddRequested(true);
    if (plan.drawer === 'import') setImportRequested(true);
    if (plan.tab) chooseProjectTab(plan.tab);
    if (plan.branch) setBranchAsked(plan.branch);
    if (plan.workspaceTab) chooseWorkspaceTab(plan.workspaceTab);
    setWorkspaceSection(plan.workspaceSection ?? null);
    // A door naming a code map opens the Map on it; any other way onto the Map opens the workspace, as it always has.
    if (plan.code !== undefined) setMapCode(plan.code);
    else if (plan.view !== view) setMapCode(null);
    // A list laid over the main area is never kept: another view lets it go (§3f).
    if (plan.view !== view) closings.setListOver(false);
    setTab(plan.view);
    store(VIEW, plan.view === 'sessions' ? 'sessions' : null);
  };
  // A door naming a workspace's page and no workspace opens the one in view (UX6g).
  const open = (target: View, item?: string | null, part?: OpenPart) => apply(plannedOpening(target, item, part, circle));

  /** A domain chosen in Settings' own list opens at its top: an anchor its part never answered is dropped. */
  const chooseSettings = (section: SettingsSection) => {
    lists.choose('settings', section);
    setSettingsAnchor(null);
  };
  const openSettings = (section: SettingsSection, anchor?: SettingsAnchor) => open('settings', section, { anchor });
  return { open, openSettings, apply, chooseSettings };
}
