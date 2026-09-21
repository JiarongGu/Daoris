import type { Meta, StoryObj } from '@storybook/react-vite';
import * as Tooltip from '@radix-ui/react-tooltip';
import { ActivityBar, AppStrip } from './frame';
import { Button, Icon, LanguageSwitcher } from '../ui';

// The application's own chrome (D56): the strip across the top and the bar down the side. Both are
// molecules — every state below is reached by passing props, with no shell and no service behind
// them (components plan §2).

const DOMAINS = [
  { tab: 'overview' as const, label: 'Overview', icon: 'overview' as const },
  { tab: 'quests' as const, label: 'Quests', icon: 'quests' as const, badge: 1 },
  { tab: 'projects' as const, label: 'Projects', icon: 'projects' as const },
  { tab: 'convergence' as const, label: 'Convergence', icon: 'convergence' as const },
  { tab: 'search' as const, label: 'Search', icon: 'search' as const },
  { tab: 'settings' as const, label: 'Machine', icon: 'settings' as const },
];

const meta: Meta<typeof AppStrip> = {
  title: 'Chrome/AppStrip',
  component: AppStrip,
  args: { mode: 'manage', modeAvailable: true, attention: 0, onMode: () => {} },
  decorators: [(Story) => (
    <Tooltip.Provider><div className="w-[52rem] border border-line"><Story /></div></Tooltip.Provider>
  )],
};
export default meta;

type Story = StoryObj<typeof AppStrip>;

/** A browser: no shell, so no Work to switch to, and the strip is a wordmark and a scope. */
export const InABrowser: Story = { args: { modeAvailable: false } };

/** The shell, in Manage, with nothing waiting. */
export const Manage: Story = {};

/** In Work, with two things waiting on a person — the one badge that wears a status hue. */
export const WorkingWithAttention: Story = { args: { mode: 'work', attention: 2 } };

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
 * The room SURF7 will draw the caption buttons into. It is reserved from this landing so the strip's
 * right edge does not move when the window loses its OS title bar — a layout that shifts on the
 * landing after this one is a layout nobody trusts.
 */
export const CaptionRoomReserved: Story = { args: { captionRoom: true } };

/* ------------------------------------------------------------------ activity bar */

export const Bar: StoryObj<typeof ActivityBar> = {
  render: () => (
    <Tooltip.Provider><div className="flex h-[24rem] border border-line">
      <ActivityBar
        label="Domains"
        items={DOMAINS}
        active="quests"
        onSelect={() => {}}
        footer={<><Button variant="ghost"><Icon name="refresh" size={14} /></Button><LanguageSwitcher compact /></>}
      />
      <div className="flex-1 bg-page" />
    </div></Tooltip.Provider>
  ),
};

/**
 * In Work **nothing is active**, because the active thing is the other frame. The bar still renders
 * every domain, and a click on one is a door back into Manage on it (D56).
 */
export const BarInWork: StoryObj<typeof ActivityBar> = {
  render: () => (
    <Tooltip.Provider><div className="flex h-[24rem] border border-line">
      <ActivityBar label="Domains" items={DOMAINS} active={null} onSelect={() => {}} />
      <div className="flex-1 bg-page" />
    </div></Tooltip.Provider>
  ),
};

/** A browser has no Machine domain at all — an absent item, never a disabled one. */
export const BarWithoutMachine: StoryObj<typeof ActivityBar> = {
  render: () => (
    <Tooltip.Provider><div className="flex h-[24rem] border border-line">
      <ActivityBar
        label="Domains"
        items={DOMAINS.filter((item) => item.tab !== 'settings')}
        active="overview"
        onSelect={() => {}}
      />
      <div className="flex-1 bg-page" />
    </div></Tooltip.Provider>
  ),
};
