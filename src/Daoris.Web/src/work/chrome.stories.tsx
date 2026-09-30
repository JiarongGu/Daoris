import type { Meta, StoryObj } from '@storybook/react-vite';
import * as Tooltip from '@radix-ui/react-tooltip';
import { ActivityBar, AppStrip } from './frame';
import { CommandCenter } from './CommandCenter';
import { Button, Icon, LanguageSwitcher } from '../ui';

// The application's own chrome (D56, D66): the strip across the top and the bar down the side. Both
// are molecules — every state below is reached by passing props, with no shell and no service behind
// them (components plan §2).

const VIEWS = [
  { tab: 'overview' as const, label: 'Overview', icon: 'overview' as const },
  { tab: 'sessions' as const, label: 'Sessions', icon: 'frameWork' as const, badge: 2, tone: 'open' as const },
  { tab: 'quests' as const, label: 'Quests', icon: 'quests' as const, badge: 1 },
  { tab: 'projects' as const, label: 'Repositories', icon: 'projects' as const },
  { tab: 'convergence' as const, label: 'Convergence', icon: 'convergence' as const },
  { tab: 'search' as const, label: 'Search', icon: 'search' as const },
];
const SETTINGS = [{ tab: 'settings' as const, label: 'Settings', icon: 'settings' as const }];

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
 * The one navigation (D66): every view in one list, Settings at the foot, and two counts — sessions
 * waiting on a person in the status hue, outstanding quests in the accent, each a true circle.
 */
export const Bar: StoryObj<typeof ActivityBar> = {
  render: () => (
    <Tooltip.Provider><div className="flex h-[26rem] border border-line">
      <ActivityBar
        label="Views"
        items={VIEWS}
        end={SETTINGS}
        active="quests"
        onSelect={() => {}}
        footer={<><Button variant="ghost"><Icon name="refresh" size={14} /></Button><LanguageSwitcher compact /></>}
      />
      <div className="flex-1 bg-page" />
    </div></Tooltip.Provider>
  ),
};

/** Settings current — the foot place wears the marking the list's places do. */
export const BarOnSettings: StoryObj<typeof ActivityBar> = {
  render: () => (
    <Tooltip.Provider><div className="flex h-[26rem] border border-line">
      <ActivityBar label="Views" items={VIEWS} end={SETTINGS} active="settings" onSelect={() => {}} />
      <div className="flex-1 bg-page" />
    </div></Tooltip.Provider>
  ),
};

/** A browser has no Sessions at all — an absent item, never a disabled one. */
export const BarInABrowser: StoryObj<typeof ActivityBar> = {
  render: () => (
    <Tooltip.Provider><div className="flex h-[26rem] border border-line">
      <ActivityBar
        label="Views"
        items={VIEWS.filter((item) => item.tab !== 'sessions')}
        end={SETTINGS}
        active="overview"
        onSelect={() => {}}
      />
      <div className="flex-1 bg-page" />
    </div></Tooltip.Provider>
  ),
};
