import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../locales/i18n';
import type { PluginShown } from './catalog';
import { PluginPage } from './PluginPage';

// A plugin's page with what it declares: Set up and About as tabs, a round card under the header, and a removal that offers to
// sign it out first.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const PLUGIN: PluginShown = {
  id: 'acme-boards', name: 'Acme boards', version: '3.0.1', description: 'Opens pull requests on the boards service.',
  enabled: true, problem: null, harnesses: [], points: ['work/perform'], running: false,
  folder: 'C:/somewhere/data/plugins/acme-boards', data: 'C:/somewhere/data/plugins/.data/acme-boards',
};

function page(over: Partial<Parameters<typeof PluginPage>[0]> = {}) {
  const calls = {
    onSwitch: vi.fn(), onTry: vi.fn(), onAskUpdate: vi.fn(), onApplyUpdate: vi.fn(), onCancelUpdate: vi.fn(),
    onAskRemove: vi.fn(), onRemove: vi.fn(), onCancelRemove: vi.fn(),
  };
  render(<PluginPage plugin={PLUGIN} {...calls} {...over} />);
  return calls;
}

describe("a plugin's page with interfaces", () => {
  it('opens at Set up beside About, and About holds what the page said before it had tabs', async () => {
    page({ setup: <p>The commands and the switches.</p>, setupFirst: true });
    const tabs = screen.getByRole('tablist', { name: 'Parts of its page' });
    expect(within(tabs).getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Set up', 'About']);
    expect(screen.getByText('The commands and the switches.')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Points' })).toBeNull();
    await userEvent.click(screen.getByRole('tab', { name: 'About' }));
    expect(screen.getByRole('heading', { name: 'Points' })).toBeInTheDocument();
  });

  it('is About alone, with no tab row, where nothing is set up; a round card stands under the header', () => {
    page({ round: <section aria-label="a round card" /> });
    expect(screen.queryByRole('tablist')).toBeNull();
    expect(screen.getByRole('heading', { name: 'Points' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'a round card' })).toBeInTheDocument();
  });

  it('offers at removal to sign it out first, ticked, and hands the person\'s choice on', async () => {
    const { onRemove } = page({ asking: true, signOut: { command: 'logout', why: null } });
    const ask = screen.getByRole('group', { name: 'remove acme-boards' });
    const tick = within(ask).getByRole('checkbox', { name: /Ask it to sign out at its service first/ });
    expect(tick).toBeChecked();
    await userEvent.click(tick);
    await userEvent.click(within(ask).getByRole('button', { name: 'Remove plugin' }));
    expect(onRemove).toHaveBeenCalledWith('acme-boards', expect.anything(), false);
  });

  it('says why a removal asks no sign-out where its forget is disabled', () => {
    page({ asking: true, signOut: { command: null, why: '`logout` asks the person, so it cannot run alone.' } });
    const ask = screen.getByRole('group', { name: 'remove acme-boards' });
    expect(within(ask).queryByRole('checkbox')).toBeNull();
    expect(ask).toHaveTextContent('It is not asked to sign out: logout asks the person, so it cannot run alone.');
  });
});
