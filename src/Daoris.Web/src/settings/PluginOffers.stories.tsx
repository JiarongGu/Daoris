import type { Meta, StoryObj } from '@storybook/react-vite';
import { PluginOffersCard, type PluginOfferShown } from './PluginOffers';
import { PluginUpdatePlan } from './PluginUpdate';

// PLUG9 (c) and (d), D103, in the shapes the driver's PLUGINS and PLUGIN_UPDATE answers take: the install's
// own plugins as the tracked examples are, one of them unsound, and an update's plan three ways.

const OFFERS: PluginOfferShown[] = [
  {
    id: 'github-pull-request', name: 'GitHub pull request', version: '1.0.0',
    description: 'Once a branch rule\'s landing has made its branch, pushes it to origin and opens a pull request with the gh CLI.',
    problem: null, harnesses: [], points: ['work/land'], servers: [], installed: false,
    needs: [
      'node on the PATH (it is a `node` script, as the other example plugins are).',
      'git, with `origin` pointing at the GitHub repository and push access for whoever `git` pushes as.',
      'gh, the GitHub CLI, installed and signed in: `gh auth login` (check with `gh auth status`).',
    ],
  },
  {
    id: 'azure-devops-pull-request', name: 'Azure DevOps pull request', version: '1.0.0',
    description: 'Once a branch rule\'s landing has made its branch, pushes it to origin and opens a pull request with the az CLI\'s devops extension.',
    problem: null, harnesses: [], points: ['work/land'], servers: [], installed: false,
    needs: [
      'node on the PATH (it is a `node` script, as the other example plugins are).',
      'az, the Azure CLI, signed in with `az login`, and its devops extension: `az extension add --name azure-devops`.',
    ],
  },
  {
    id: 'in-app-browser', name: 'In-app browser', version: '1.0.0', description: '', problem: null,
    harnesses: [], points: [], servers: ['browser'], installed: true, needs: [],
  },
];

const meta = {
  title: 'Settings/PluginOffers',
  component: PluginOffersCard,
  args: { offers: OFFERS, onInstall: () => {} },
} satisfies Meta<typeof PluginOffersCard>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Offered: Story = {};

export const Installing: Story = { args: { busy: true } };

export const OneCannotBeInstalled: Story = {
  args: {
    offers: [
      OFFERS[0]!,
      { ...OFFERS[1]!, needs: [], problem: 'needs plugin API 2, and this build speaks 1 — update Daoris, or use a plugin written for 1.' },
    ],
  },
};

export const WhatAnUpdateChanges: Story = {
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
