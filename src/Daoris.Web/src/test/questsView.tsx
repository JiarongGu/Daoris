import { useState } from 'react';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { OpenGroup } from '../opener';
import { useQuestsView } from '../QuestsView';
import type { Notify } from '../ui';
import { useListPanes } from '../work/listPanes';
import { ViewFrame } from '../work/ViewFrame';
import { useDoor } from './door';

// Quests alone, as its suites hold it (FRAME1d): the view's list and its main area on a browser's frame, with the
// application's list memory, so what a test chooses is what a relaunch would remember. The doors' events are props,
// held the way `App` holds them.

export function QuestsView({ notify, onAttend, opening, onOpened, asking, onAsked, door, group, onGroupBrought }: {
  notify: Notify;
  onAttend?: (session: string) => void;
  opening?: { from?: string; to?: string } | null;
  onOpened?: () => void;
  asking?: boolean;
  onAsked?: () => void;
  /** The item a door names as it opens the view (`useDoor`). */
  door?: string;
  /** The group a door brings into view (ENTRY1b), and its telling. */
  group?: OpenGroup | null;
  onGroupBrought?: () => void;
}) {
  const lists = useListPanes();
  const [over, setOver] = useState(false);
  useDoor(lists, 'quests', door);
  const pane = lists.pane('quests');
  const layout = useQuestsView({
    active: true,
    chosen: pane.chosen,
    onChoose: (item) => lists.choose('quests', item),
    filters: pane.filters,
    onFilters: (filters) => lists.setFilters('quests', filters),
    notify, onAttend, opening, onOpened, asking, onAsked, group, onGroupBrought,
  });
  return <ViewFrame layout={layout} lists={lists} over={over} onOver={setOver} />;
}

/** The list pane: the one region of a browser's frame beside the main area. */
export const questList = () => screen.getByRole('complementary');

/** The main area, found afresh: it is a new element as it goes from nothing chosen to a page. */
export const questMain = () => screen.getByRole('main');

/** A record's page, found by its title once the main area shows it. */
export async function questPage(title: string) {
  await screen.findByRole('heading', { level: 1, name: title });
  return questMain();
}

/** A row of the list chosen by its title, and the page it opens. */
export async function chooseRow(title: string) {
  const row = await within(questList()).findByText(title);
  await userEvent.click(row);
  return questPage(title);
}

/** The list's ＋, then one of its kinds: a menu opens from the keyboard in jsdom, the path D41 §6 requires anyway. */
export async function makeFromList(kind: 'Ask' | 'New quest') {
  const user = userEvent.setup();
  within(questList()).getByRole('button', { name: 'New ask or quest' }).focus();
  await user.keyboard('{Enter}');
  await user.click(await screen.findByRole('menuitem', { name: kind }));
}

/**
 * One of a quest page's acts its head keeps in its ⋯ (UX7c, D152 §7): *Mark done* while open, *Pause…*, *Abandon…*,
 * *Delete…*, *Copy quest ID*. Opened from the keyboard, as every menu here is in jsdom.
 */
export async function moreAct(page: HTMLElement, act: string) {
  const user = userEvent.setup();
  within(page).getByRole('button', { name: 'More actions' }).focus();
  await user.keyboard('{Enter}');
  await user.click(await screen.findByRole('menuitem', { name: act }));
}

/** The list's ⋯, open on its filters. */
export async function openFilters() {
  const user = userEvent.setup();
  within(questList()).getByRole('button', { name: 'Filter the list' }).focus();
  await user.keyboard('{Enter}');
  await screen.findByRole('group', { name: 'Receiver' });
  return user;
}
