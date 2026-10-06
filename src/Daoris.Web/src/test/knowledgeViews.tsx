import type { ReactNode } from 'react';
import { useState } from 'react';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import { useKnowledgeMode, useKnowledgeView } from '../KnowledgeView';
import { type KnowledgeMode, storeKnowledgeMode } from '../knowledge/modes';
import { useListPanes } from '../work/listPanes';
import { ViewFrame } from '../work/ViewFrame';
import { useDoor } from './door';

// Search and Convergence as their suites hold them (FRAME1f), through the place they are since UX6i (D150 §2.2): Knowledge
// on a browser's frame, its list the mode's and its main area the mode's page, with the application's list memory, so
// what a test chooses, filters and switches to is what a relaunch would remember.

function KnowledgeAlone({ semantic, door, mode: opening }: { semantic: boolean; door?: string; mode: KnowledgeMode }) {
  const lists = useListPanes();
  const [over, setOver] = useState(false);
  const [mode, onMode] = useKnowledgeMode(lists);
  // A door names its item in the mode it opens the place in, as the application's opener does.
  useDoor(lists, opening, door);
  const layout = useKnowledgeView({ active: true, mode, onMode, lists, notify: () => {}, semantic });
  return <ViewFrame layout={layout} lists={lists} over={over} onOver={setOver} />;
}

const wrapped = (node: ReactNode) => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}><Tooltip.Provider>{node}</Tooltip.Provider></QueryClientProvider>);
};

/** Knowledge opened on its Search, over whatever `fetch` the test stubbed; `door` names the entry a door opens it on. */
export const showSearch = ({ semantic = false, door }: { semantic?: boolean; door?: string } = {}) => {
  storeKnowledgeMode('search');
  return wrapped(<KnowledgeAlone semantic={semantic} door={door} mode="search" />);
};

/** Knowledge opened on its Convergence, over whatever `fetch` the test stubbed; `door` names the finding a door opens it on. */
export const showConvergence = ({ semantic = false, door }: { semantic?: boolean; door?: string } = {}) => {
  storeKnowledgeMode('convergence');
  return wrapped(<KnowledgeAlone semantic={semantic} door={door} mode="convergence" />);
};

/** Knowledge as a relaunch opens it: in the mode it was left in. */
export const showKnowledge = ({ semantic = false }: { semantic?: boolean } = {}) =>
  wrapped(<KnowledgeAlone semantic={semantic} mode="search" />);

/** Knowledge's list pane: the one region of a browser's frame beside the main area, whichever mode it shows. */
export const knowledgeList = () => screen.getByRole('complementary', { name: 'Knowledge' });

/** The list's head: the two-way choice between Search and Convergence. */
export const modeChoice = () => within(knowledgeList()).getByRole('radiogroup', { name: 'Knowledge' });

/** The main area, found afresh: it is a new element as it goes from nothing chosen to a page. */
export const mainArea = () => screen.getByRole('main');

/** A page's title, once the main area draws it; the main area is found afresh, since it is a new element by then. */
export async function pageTitled(title: string) {
  await screen.findByRole('heading', { level: 1, name: title });
  return mainArea();
}

/** A row of the list, by what it is titled, chosen; and the main area it opens, once its page is drawn. */
export async function chooseRow(title: string) {
  const row = await within(knowledgeList()).findByRole('listitem', { name: title });
  await userEvent.click(within(row).getByRole('button'));
  return pageTitled(title);
}

/** The list's head switched to a mode, as the person presses it. */
export const switchTo = (mode: 'Search' | 'Convergence') => userEvent.click(within(modeChoice()).getByRole('radio', { name: mode }));
