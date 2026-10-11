import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import '../i18n';
import { PluginSourceLine, PluginUpdatePlan, type PluginUpdatePlanShown } from './PluginUpdate';

// PLUG9 (c), D103: where an installed plugin came from and what an update changes (`PluginUpdate`), as a plugin's page
// draws them. Molecules: props in, presses out. Every sentence the driver wrote — a refusal, a command — renders as it
// said it; the chrome translates. The install's own plugins were offered here too, in Settings → Plugins' card, until that
// domain retired into the Plugins place (UX6j, D150 §2.3), whose list and offer page hold them.

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
    expect(changes.getByText('Agents')).toBeInTheDocument();
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
  it('translates the chrome and leaves the driver\'s words as they are', async () => {
    const { default: i18n } = await import('../i18n');
    await i18n.changeLanguage('zh');
    try {
      render(
        <Tooltip.Provider>
          <PluginSourceLine source={{ kind: 'none' }} />
          <PluginUpdatePlan plan={PLAN} onApply={vi.fn()} onCancel={vi.fn()} />
        </Tooltip.Provider>,
      );

      expect(screen.getByText(/没有它来自哪里的记录/)).toBeInTheDocument();
      expect(screen.getByRole('region', { name: '更新内容' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: '立即更新' })).toBeInTheDocument();
    } finally {
      cleanup();
      await i18n.changeLanguage('en');
    }
  });
});
