import type { Meta, StoryObj } from '@storybook/react-vite';
import * as Tooltip from '@radix-ui/react-tooltip';
import { ActivityBar, type ActivityItem, AppStrip } from './frame';
import { CommandCenter } from '../strip/CommandCenter';
import { InTheme } from '../../plugins/storyIcons';

// The application's own chrome (D56, D66): the strip across the top and the bar down the side. Both
// are molecules — every state below is reached by passing props, with no shell and no service behind
// them (components plan §2).

type Place = 'overview' | 'sessions' | 'quests' | 'projects' | 'map' | 'knowledge' | 'agents' | 'plugins' | 'settings';

// The bar's places as a shell draws them since UX6i (D150 §2.1): Search and Convergence are one place, Knowledge. Each number is
// what waits on the person there, the bell's rows its door opens, and running work is a mark, never a number (BADGEUX1, the UI/UX
// design §3.10): Sessions with two parks and the driver's work under way, Quests with a proposal and a quest taken, Agents with a
// signed-out account.
const VIEWS: ActivityItem<Place>[] = [
  { tab: 'overview', label: 'Overview', icon: 'overview' },
  { tab: 'sessions', label: 'Sessions', icon: 'frameWork', waits: 2, running: true, detail: '2 waiting on you · 5 under way' },
  { tab: 'quests', label: 'Quests', icon: 'quests', waits: 1, running: true, detail: '1 waiting on you · 3 taken' },
  { tab: 'projects', label: 'Repositories', icon: 'projects' },
  { tab: 'map', label: 'Map', icon: 'map' },
  { tab: 'knowledge', label: 'Knowledge', icon: 'knowledge' },
  { tab: 'agents', label: 'Agents', icon: 'account', waits: 1, detail: '1 waiting on you' },
  { tab: 'plugins', label: 'Plugins', icon: 'plug' },
];
const SETTINGS: ActivityItem<Place>[] = [{ tab: 'settings', label: 'Settings', icon: 'settings' }];

/** The same places with nothing waiting: work under way at Sessions and Quests, marked, and no number anywhere. */
const RUNNING_ONLY: ActivityItem<Place>[] = VIEWS.map(({ waits: _waits, detail: _detail, ...place }) => (
  place.tab === 'sessions' ? { ...place, detail: '5 under way' }
    : place.tab === 'quests' ? { ...place, detail: '3 taken' }
      : { ...place, running: false }));

/** Every place a wait can be counted at, a long count among them, and nothing running. */
const WAITS_ONLY: ActivityItem<Place>[] = VIEWS.map((place) => ({
  ...place,
  running: false,
  waits: place.tab === 'sessions' ? 140 : place.tab === 'projects' ? 1 : place.waits,
  detail: place.tab === 'sessions' ? '140 waiting on you' : place.tab === 'projects' ? '1 waiting on you' : place.waits ? '1 waiting on you' : undefined,
}));
const SETTINGS_WAITING: ActivityItem<Place>[] = [{ ...SETTINGS[0]!, waits: 1, detail: '1 waiting on you' }];

/** The bar in a frame of the window's height, beside an empty main area, in a theme. */
const barIn = (items: ActivityItem<Place>[], end: ActivityItem<Place>[], theme: 'light' | 'dark', active: Place = 'quests') => (
  <Tooltip.Provider>
    <InTheme theme={theme}>
      <div className="flex h-[30rem] border border-line bg-page">
        <ActivityBar label="Views" items={items} end={end} active={active} onSelect={() => {}} />
        <div className="flex-1 bg-page" />
      </div>
    </InTheme>
  </Tooltip.Provider>
);

const meta: Meta<typeof AppStrip> = {
  title: 'Chrome/AppStrip',
  component: AppStrip,
  decorators: [(Story) => (
    <Tooltip.Provider><div className="w-[52rem] border border-line"><Story /></div></Tooltip.Provider>
  )],
};
export default meta;

type Story = StoryObj<typeof AppStrip>;

/** A browser: no window to command, so the strip is the mark and nothing reserved. */
export const InABrowser: Story = {};

/** The shell: the application's menus, and the room the window paints its buttons into. */
export const InTheShell: Story = {
  args: {
    menus: <span className="text-small text-ink-soft">Daoris · View</span>,
    captionRoom: true,
  },
};

/** The scope is a slot, because the switcher is absent while the family is one circle (WSP5). */
export const WithScope: Story = {
  args: {
    scope: (
      <span className="rounded-control border border-line px-2 py-1 text-small text-ink-soft">
        every circle
      </span>
    ),
  },
};

/**
 * 🔴 A narrow window (UX5 U15): the four menus, the command center, the scope and the caption room
 * in 52rem. The pill used to be laid over the strip at a fixed 28rem, and here it ran over the View
 * menu. It gives way to the menus now, and centres on the strip again once the window is wide
 * enough for both sides to fit beside it.
 */
export const AtANarrowWindow: Story = {
  args: {
    menus: <span className="text-small text-ink-soft">Daoris · Workspace · Agents · View</span>,
    center: <CommandCenter scope="every circle" shortcut="Ctrl K" onOpen={() => {}} label="Search and run commands" />,
    scope: <span className="text-small text-ink-soft">game</span>,
    captionRoom: true,
  },
};

/**
 * The room SURF7 will draw the caption buttons into. It is reserved from this landing so the strip's
 * right edge does not move when the window loses its OS title bar — a layout that shifts on the
 * landing after this one is a layout nobody trusts.
 */
export const CaptionRoomReserved: Story = { args: { captionRoom: true } };

/* ------------------------------------------------------------------ activity bar */

/**
 * The one navigation (D66): every view in one list, Settings alone at the foot (UX6j, D150 §2.1), and its marks (BADGEUX1): a
 * number in open's hue is what waits on the person there, a true circle at the upper corner; a still dot in taken's hue at the
 * lower corner is work under way there. Sessions and Quests wear both, Agents a number alone. Nine controls: the eight places and
 * the gear.
 */
export const Bar: StoryObj<typeof ActivityBar> = { render: () => barIn(VIEWS, SETTINGS, 'light') };

/** The same, dark: the number's ink and the mark's hue each hold against the page and the current place (§3.10.2). */
export const BarDark: StoryObj<typeof ActivityBar> = { render: () => barIn(VIEWS, SETTINGS, 'dark') };

/** Running work and nothing waiting: the marks alone, and no number anywhere, the current place's among them. */
export const BarRunningOnly: StoryObj<typeof ActivityBar> = { render: () => barIn(RUNNING_ONLY, SETTINGS, 'light', 'sessions') };

/** Running work alone, dark. */
export const BarRunningOnlyDark: StoryObj<typeof ActivityBar> = { render: () => barIn(RUNNING_ONLY, SETTINGS, 'dark', 'sessions') };

/** Waits at every place that counts them, a count past 99 among them and the gear's own, with nothing running. */
export const BarWaitsOnly: StoryObj<typeof ActivityBar> = { render: () => barIn(WAITS_ONLY, SETTINGS_WAITING, 'light') };

/** Waits alone, dark. */
export const BarWaitsOnlyDark: StoryObj<typeof ActivityBar> = { render: () => barIn(WAITS_ONLY, SETTINGS_WAITING, 'dark') };

/** Settings current — the foot place wears the marking the list's places do. */
export const BarOnSettings: StoryObj<typeof ActivityBar> = {
  render: () => (
    <Tooltip.Provider><div className="flex h-[30rem] border border-line">
      <ActivityBar label="Views" items={VIEWS} end={SETTINGS} active="settings" onSelect={() => {}} />
      <div className="flex-1 bg-page" />
    </div></Tooltip.Provider>
  ),
};

/**
 * A browser has no Sessions, Agents or Plugins at all — an absent item, never a disabled one. Nor any number: who needs the person
 * is the app's judgement (PUSH1d4), so no place counts a wait there; Quests' mark still says a quest is taken, from the service.
 */
export const BarInABrowser: StoryObj<typeof ActivityBar> = {
  render: () => (
    <Tooltip.Provider><div className="flex h-[30rem] border border-line">
      <ActivityBar
        label="Views"
        items={RUNNING_ONLY.filter((item) => item.tab !== 'sessions' && item.tab !== 'agents' && item.tab !== 'plugins')}
        end={SETTINGS}
        active="overview"
        onSelect={() => {}}
      />
      <div className="flex-1 bg-page" />
    </div></Tooltip.Provider>
  ),
};
