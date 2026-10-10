import { useState } from 'react';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useProjectsView } from '../ProjectsView';
import {
  type ProjectTab, readProjectTab, readWorkspaceTab, storeProjectTab, storeWorkspaceTab, type WorkspaceSection, type WorkspaceTab,
} from '../projects/tabs';
import type { Notify } from '../ui';
import { useListPanes } from '../work/listPanes';
import { ViewFrame } from '../work/ViewFrame';
import { useDoor } from './door';

// Repositories alone, as its suites hold it (FRAME1e): the view's list and its main area on a browser's frame, with
// the application's list memory, so what a test chooses is what a relaunch would remember. The doors' events are
// props, held the way `App` holds them.

export function ProjectsView({
  notify, onOpenCode, onOpenAgent, onSyncNow, addRequested, onAddOpened, importRequested, onImportOpened, drawerWorkspace, door,
  section = null, onOpenQuest, onOpenAsk, onAttend,
}: {
  notify: Notify;
  onOpenCode?: (repository: string) => void;
  onOpenAgent?: (agent: string) => void;
  onSyncNow?: (workspace: string) => void;
  onOpenQuest?: (id: string) => void;
  onOpenAsk?: (id: string) => void;
  onAttend?: (session: string) => void;
  addRequested?: boolean;
  onAddOpened?: () => void;
  importRequested?: boolean;
  onImportOpened?: () => void;
  /** The workspace a go's Add or Import opens filled with (ENTRY1d2b). */
  drawerWorkspace?: string | null;
  /** The item a door names as it opens the view (`useDoor`): a repository, or a workspace's item. */
  door?: string;
  /** The section of a workspace's Setup the door asked to see open (UX6g). */
  section?: WorkspaceSection | null;
}) {
  const lists = useListPanes();
  const [over, setOver] = useState(false);
  // The page's tab, held as `App` holds it, so a relaunch reopens the tab a test chose (UX6f); a workspace's apart (UX6g).
  const [tab, setTab] = useState<ProjectTab>(readProjectTab);
  const [workspaceTab, setWorkspaceTab] = useState<WorkspaceTab>(() => (section ? 'setup' : readWorkspaceTab()));
  useDoor(lists, 'projects', door);
  const layout = useProjectsView({
    active: true,
    chosen: lists.pane('projects').chosen,
    onChoose: (item) => lists.choose('projects', item),
    tab,
    onTab: (next) => { setTab(next); storeProjectTab(next); },
    workspaceTab,
    onWorkspaceTab: (next) => { setWorkspaceTab(next); storeWorkspaceTab(next); },
    workspaceSection: section,
    notify, onOpenCode, onOpenAgent, onSyncNow, addRequested, onAddOpened, importRequested, onImportOpened, drawerWorkspace,
    onOpenQuest, onOpenAsk, onAttend,
  });
  return <ViewFrame layout={layout} lists={lists} over={over} onOver={setOver} />;
}

/** The list pane: the one region of a browser's frame beside the main area. */
export const repositoryList = () => screen.getByRole('complementary', { name: 'Repositories' });

/** The main area, found afresh: it is a new element as it goes from nothing chosen to a page. */
export const repositoryMain = () => screen.getByRole('main');

/** A repository's row in the list, named by the repository. */
export const repositoryRow = (name: string) => within(repositoryList()).findByRole('listitem', { name });

/** A repository's row chosen, and the page it opens, found by its title. */
export async function chooseRepository(name: string) {
  const row = await repositoryRow(name);
  await userEvent.click(within(row).getAllByRole('button')[0]!);
  await screen.findByRole('heading', { level: 1, name });
  return repositoryMain();
}

/** A workspace's head in the list chosen (UX6g, D150 §4.1), and the page it opens, found by its title. */
export async function chooseWorkspace(name: string) {
  const group = await within(repositoryList()).findByRole('region', { name });
  await userEvent.click(within(within(group).getByRole('heading', { level: 3 })).getAllByRole('button')[0]!);
  await screen.findByRole('heading', { level: 1, name });
  return repositoryMain();
}

/** The chosen repository's Setup tab (UX6f), and a section of it opened where it is folded. */
export async function openSetup(section?: string) {
  await userEvent.click(await within(repositoryMain()).findByRole('tab', { name: 'Setup' }));
  if (section) {
    const head = await within(repositoryMain()).findByRole('button', { name: section });
    if (head.getAttribute('aria-expanded') !== 'true') await userEvent.click(head);
  }
  return repositoryMain();
}
