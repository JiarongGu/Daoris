import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import '../i18n';
import { PluginOffersCard, type PluginOfferShown } from './PluginOffers';
import { PluginSourceLine, PluginUpdatePlan, type PluginUpdatePlanShown } from './PluginUpdate';

// PLUG9 (c) and (d), D103: where an installed plugin came from and what an update changes (`PluginUpdate`),
// and the install's own plugins offered beside the catalogue (`PluginOffers`), held in one file because the
// Plugins screen draws them together. Molecules: props in, presses out. Every sentence
// the driver wrote — a refusal, a requirement line, a command — renders as it said it; the chrome translates.

const GITHUB: PluginOfferShown = {
  id: 'github-pull-request', name: 'GitHub pull request', version: '1.0.0',
  description: 'Pushes the branch and opens a pull request with the gh CLI.',
  problem: null, harnesses: [], points: ['work/land'], servers: [],
  needs: ['node on the PATH (it is a `node` script).', 'gh, signed in: `gh auth login`.'], installed: false,
};

const BROWSER: PluginOfferShown = {
  id: 'in-app-browser', name: 'In-app browser', version: '1.0.0', description: '', problem: null,
  harnesses: [], points: [], servers: ['browser'], needs: [], installed: true,
};

const FUTURE: PluginOfferShown = {
  ...GITHUB, id: 'future', name: 'future', needs: [],
  problem: 'needs plugin API 99, and this build speaks 1 — update Daoris, or use a plugin written for 1.',
};

const drawOffers = (offers: PluginOfferShown[], busy = false) => {
  const onInstall = vi.fn();
  render(<Tooltip.Provider><PluginOffersCard offers={offers} busy={busy} onInstall={onInstall} /></Tooltip.Provider>);
  return onInstall;
};

describe('the install\'s own plugins', () => {
  it('lists each one not installed, with what it speaks on and what it needs, and installs it by a press', async () => {
    const onInstall = drawOffers([GITHUB, BROWSER]);

    expect(screen.getByText('Daoris\'s own plugins')).toBeInTheDocument();
    expect(screen.getByText('GitHub pull request')).toBeInTheDocument();
    expect(screen.getByText(/speaks on work\/land/)).toBeInTheDocument();
    const needs = within(screen.getByRole('list', { name: 'What github-pull-request needs' }));
    // The README's own words: code renders as code, and nothing is re-said.
    expect(needs.getByText('gh auth login', { selector: 'code' })).toBeInTheDocument();
    expect(needs.getAllByRole('listitem')).toHaveLength(2);
    // An installed one is in the catalogue above, not offered again.
    expect(screen.queryByText('In-app browser')).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Install github-pull-request' }));
    expect(onInstall).toHaveBeenCalledWith('github-pull-request');
  });

  it('says why an unsound one cannot be installed, in the driver\'s words, and offers no Install', () => {
    drawOffers([FUTURE]);

    expect(screen.getByText(/needs plugin API 99/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Install future' })).toBeNull();
  });

  it('draws nothing when every offer is installed, and holds Install while a press is on its way', () => {
    const { container } = render(<Tooltip.Provider><PluginOffersCard offers={[BROWSER]} onInstall={vi.fn()} /></Tooltip.Provider>);
    expect(container).toBeEmptyDOMElement();
    cleanup();

    drawOffers([GITHUB], true);
    expect(screen.getByRole('button', { name: 'Install github-pull-request' })).toBeDisabled();
  });
});

describe('where an installed plugin came from', () => {
  it('names the folder, the install\'s offer, no record, or a record that does not read', () => {
    render(
      <div>
        <PluginSourceLine source={{ kind: 'folder', folder: 'C:/checkouts/house-plugins/quiet-hours' }} />
        <PluginSourceLine source={{ kind: 'offer', offer: 'github-pull-request' }} />
        <PluginSourceLine source={{ kind: 'none' }} />
        <PluginSourceLine source={{ kind: 'unread', problem: '`.daoris-source.json` does not read (it is not a JSON object).' }} />
      </div>,
    );

    expect(screen.getByText(/Added from/)).toHaveTextContent('C:/checkouts/house-plugins/quiet-hours');
    expect(screen.getByText(/Installed from Daoris's own plugins/)).toBeInTheDocument();
    expect(screen.getByText(/No record of where it came from/)).toBeInTheDocument();
    expect(screen.getByText(/it is not a JSON object/)).toBeInTheDocument();
  });

  it('says nothing for a shell older than the record', () => {
    const { container } = render(<PluginSourceLine source={undefined} />);
    expect(container).toBeEmptyDOMElement();
  });
});

const PLAN: PluginUpdatePlanShown = {
  id: 'acme.gate', applied: false, refusal: null, source: 'C:/checkouts/gate', from: 'C:/checkouts/gate',
  changes: [
    { what: 'version', was: '1.0.0', now: '1.1.0' },
    { what: 'harnesses', was: '', now: 'acme-agent (acme-agent --acp)' },
  ],
};

const drawPlan = (plan: PluginUpdatePlanShown, busy = false) => {
  const handlers = { onApply: vi.fn(), onCancel: vi.fn() };
  render(<PluginUpdatePlan plan={plan} busy={busy} {...handlers} />);
  return handlers;
};

describe('what an update changes', () => {
  it('lists each change as the manifests write it, before the press, and updates or not', async () => {
    const { onApply, onCancel } = drawPlan(PLAN);

    const changes = within(screen.getByRole('region', { name: 'What an update changes' }));
    expect(changes.getByText('1.0.0', { selector: 'code' })).toBeInTheDocument();
    expect(changes.getByText('1.1.0', { selector: 'code' })).toBeInTheDocument();
    expect(changes.getByText('agents')).toBeInTheDocument();
    expect(changes.getByText('(none)')).toBeInTheDocument();
    expect(changes.getByText(/what it kept stays/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Update now' }));
    await userEvent.click(screen.getByRole('button', { name: 'Not now' }));
    expect(onApply).toHaveBeenCalledWith('acme.gate');
    expect(onCancel).toHaveBeenCalled();
  });

  it('says so when nothing it declares changes', () => {
    drawPlan({ ...PLAN, changes: [] });

    expect(screen.getByText(/What it declares does not change/)).toBeInTheDocument();
  });

  it('shows the driver\'s refusal verbatim, and no Update now', () => {
    drawPlan({
      ...PLAN, changes: [], source: null, from: null,
      refusal: 'plugin `acme.gate` has no record of where it came from: it was added before Daoris kept one.',
    });

    expect(screen.getByText(/has no record of where it came from/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Update now' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Not now' })).toBeInTheDocument();
  });

  it('holds its presses while the update runs', () => {
    drawPlan(PLAN, true);

    expect(screen.getByRole('button', { name: 'Update now' })).toBeDisabled();
  });
});

describe('in 中文', () => {
  it('translates the chrome and leaves the driver\'s and the README\'s words as they are', async () => {
    const { default: i18n } = await import('../i18n');
    await i18n.changeLanguage('zh');
    try {
      render(
        <Tooltip.Provider>
          <PluginOffersCard offers={[GITHUB]} onInstall={vi.fn()} />
          <PluginSourceLine source={{ kind: 'none' }} />
          <PluginUpdatePlan plan={PLAN} onApply={vi.fn()} onCancel={vi.fn()} />
        </Tooltip.Provider>,
      );

      expect(screen.getByText('Daoris 自带的插件')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: '安装 github-pull-request' })).toBeInTheDocument();
      expect(screen.getByText('gh auth login', { selector: 'code' })).toBeInTheDocument();
      expect(screen.getByText(/没有它来自哪里的记录/)).toBeInTheDocument();
      expect(screen.getByRole('region', { name: '更新会改变什么' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: '立即更新' })).toBeInTheDocument();
    } finally {
      cleanup();
      await i18n.changeLanguage('en');
    }
  });
});
