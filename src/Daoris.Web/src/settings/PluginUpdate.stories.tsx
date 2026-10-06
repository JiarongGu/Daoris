import type { Meta, StoryObj } from '@storybook/react-vite';
import { PluginUpdatePlan } from './PluginUpdate';

// PLUG9 (c), D103, in the shape the driver's PLUGIN_UPDATE answer takes: an update's plan three ways, as a plugin's page
// shows it under its header. The install's own plugins are the Plugins place's list and offer page since Settings →
// Plugins retired into it (UX6j, D150 §2.3), so their card went with the domain.

const meta = {
  title: 'Settings/PluginUpdate',
  component: PluginUpdatePlan,
} satisfies Meta<typeof PluginUpdatePlan>;

export default meta;
type Story = StoryObj<typeof meta>;

export const WhatAnUpdateChanges: Story = {
  args: {
    plan: { id: 'acme.quiet-hours', applied: false, refusal: null, source: null, from: null, changes: [] },
    onApply: () => {},
    onCancel: () => {},
  },
  render: () => (
    <div className="flex flex-col gap-3">
      <PluginUpdatePlan
        plan={{
          id: 'acme.quiet-hours', applied: false, refusal: null, source: 'C:/checkouts/house-plugins/quiet-hours',
          from: 'C:/checkouts/house-plugins/quiet-hours',
          changes: [
            { what: 'version', was: '1.0.0', now: '1.1.0' },
            { what: 'points', was: 'quest/consider', now: 'quest/consider, session/ended' },
            { what: 'servers', was: '', now: 'browser (npx -y @playwright/mcp@0.0.82)' },
          ],
        }}
        onApply={() => {}}
        onCancel={() => {}}
      />
      <PluginUpdatePlan
        plan={{ id: 'github-pull-request', applied: false, refusal: null, source: null, from: null, changes: [] }}
        onApply={() => {}}
        onCancel={() => {}}
      />
      <PluginUpdatePlan
        plan={{
          id: 'acme.bare', applied: false, changes: [],
          refusal: 'plugin `acme.bare` has no record of where it came from: it was added before Daoris kept one, or copied in by '
            + 'hand, so there is nothing to update it from. `daoris plugin add <folder>` replaces it wholesale and records where it came from.',
        }}
        onApply={() => {}}
        onCancel={() => {}}
      />
    </div>
  ),
};
