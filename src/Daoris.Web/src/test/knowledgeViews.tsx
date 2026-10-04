import { type ReactNode, useState } from 'react';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import { useConvergenceView } from '../ConvergenceView';
import { useSearchView } from '../SearchView';
import { useListPanes } from '../work/listPanes';
import { ViewFrame } from '../work/ViewFrame';
import { useDoor } from './door';

// Search and Convergence alone, as their suites hold them (FRAME1f): each view's list and its main area on a browser's
// frame, with the application's list memory, so what a test chooses and filters is what a relaunch would remember.

function SearchAlone({ semantic, onConverge, door }: { semantic: boolean; onConverge: () => void; door?: string }) {
  const lists = useListPanes();
  const [over, setOver] = useState(false);
  useDoor(lists, 'search', door);
  const pane = lists.pane('search');
  const layout = useSearchView({
    active: true,
    chosen: pane.chosen,
    onChoose: (item) => lists.choose('search', item),
    filters: pane.filters,
    onFilters: (filters) => lists.setFilters('search', filters),
    notify: () => {},
    semantic,
    onConverge,
  });
  return <ViewFrame layout={layout} lists={lists} over={over} onOver={setOver} />;
}

function ConvergenceAlone({ semantic, door }: { semantic: boolean; door?: string }) {
  const lists = useListPanes();
  const [over, setOver] = useState(false);
  useDoor(lists, 'convergence', door);
  const pane = lists.pane('convergence');
  const layout = useConvergenceView({
    active: true,
    chosen: pane.chosen,
    onChoose: (item) => lists.choose('convergence', item),
    filters: pane.filters,
    onFilters: (filters) => lists.setFilters('convergence', filters),
    notify: () => {},
    semantic,
  });
  return <ViewFrame layout={layout} lists={lists} over={over} onOver={setOver} />;
}

const wrapped = (node: ReactNode) => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}><Tooltip.Provider>{node}</Tooltip.Provider></QueryClientProvider>);
};

/** Search on a browser's frame, over whatever `fetch` the test stubbed; `door` names the entry a door opens it on. */
export const showSearch = ({ semantic = false, onConverge = () => {}, door }: { semantic?: boolean; onConverge?: () => void; door?: string } = {}) =>
  wrapped(<SearchAlone semantic={semantic} onConverge={onConverge} door={door} />);

/** Convergence on a browser's frame, over whatever `fetch` the test stubbed; `door` names the finding a door opens it on. */
export const showConvergence = ({ semantic = false, door }: { semantic?: boolean; door?: string } = {}) =>
  wrapped(<ConvergenceAlone semantic={semantic} door={door} />);

/** A view's list pane, by the view's name: the one region of a browser's frame beside the main area. */
export const listOf = (name: 'Search' | 'Convergence') => screen.getByRole('complementary', { name });

/** The main area, found afresh: it is a new element as it goes from nothing chosen to a page. */
export const mainArea = () => screen.getByRole('main');

/** A page's title, once the main area draws it; the main area is found afresh, since it is a new element by then. */
export async function pageTitled(title: string) {
  await screen.findByRole('heading', { level: 1, name: title });
  return mainArea();
}

/** A row of a list, by what it is titled, chosen; and the main area it opens, once its page is drawn. */
export async function chooseRow(name: 'Search' | 'Convergence', title: string) {
  const row = await within(listOf(name)).findByRole('listitem', { name: title });
  await userEvent.click(within(row).getByRole('button'));
  return pageTitled(title);
}
