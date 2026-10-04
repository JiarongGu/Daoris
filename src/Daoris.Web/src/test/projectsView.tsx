import { useState } from 'react';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useProjectsView } from '../ProjectsView';
import type { Notify } from '../ui';
import { useListPanes } from '../work/listPanes';
import { ViewFrame } from '../work/ViewFrame';
import { useDoor } from './door';

// Repositories alone, as its suites hold it (FRAME1e): the view's list and its main area on a browser's frame, with
// the application's list memory, so what a test chooses is what a relaunch would remember. The doors' events are
// props, held the way `App` holds them.

export function ProjectsView({ notify, onOpenCode, addRequested, onAddOpened, importRequested, onImportOpened, door }: {
  notify: Notify;
  onOpenCode?: (repository: string) => void;
  addRequested?: boolean;
  onAddOpened?: () => void;
  importRequested?: boolean;
  onImportOpened?: () => void;
  /** The repository a door names as it opens the view (`useDoor`). */
  door?: string;
}) {
  const lists = useListPanes();
  const [over, setOver] = useState(false);
  useDoor(lists, 'projects', door);
  const layout = useProjectsView({
    active: true,
    chosen: lists.pane('projects').chosen,
    onChoose: (item) => lists.choose('projects', item),
    notify, onOpenCode, addRequested, onAddOpened, importRequested, onImportOpened,
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
