import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../locales/i18n';
import type { PluginShown } from './catalog';
import { PluginPage } from './PluginPage';

// An unsound plugin (three unasked exits within an hour) is started again by the person's *Try again*, offered on its page while the
// loop holds it so, with no restart of Daoris.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const PLUGIN: PluginShown = {
  id: 'acme-tickets', name: 'Acme tickets', version: '1.0.0', description: 'Opens the pull request.',
  enabled: true, problem: null, harnesses: [], points: ['work/perform'], running: false,
  folder: 'C:/somewhere/data/plugins/acme-tickets', data: 'C:/somewhere/data/plugins/.data/acme-tickets',
};

function page(plugin: PluginShown, onTryAgain = vi.fn()) {
  render(
    <PluginPage
      plugin={plugin} kitPoints={null} canTry={false} onSwitch={vi.fn()} onTry={vi.fn()} onTryAgain={onTryAgain}
      onAskUpdate={vi.fn()} onApplyUpdate={vi.fn()} onCancelUpdate={vi.fn()} onAskRemove={vi.fn()} onRemove={vi.fn()} onCancelRemove={vi.fn()}
    />,
  );
  return onTryAgain;
}

const header = () => screen.getByRole('heading', { level: 1 }).closest('header')!;

describe("an unsound plugin's page", () => {
  afterEach(() => i18n.changeLanguage('en'));

  it('offers Try again while the loop holds it unsound, and the press names the plugin', async () => {
    const onTryAgain = page({ ...PLUGIN, unsound: true });

    await userEvent.click(within(header()).getByRole('button', { name: 'Try again' }));

    expect(onTryAgain).toHaveBeenCalledWith('acme-tickets');
    expect(screen.getByText(/three times within an hour/)).toBeInTheDocument();
  });

  it('offers none while it is sound', () => {
    page({ ...PLUGIN, unsound: false });

    expect(within(header()).queryByRole('button', { name: 'Try again' })).toBeNull();
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    page({ ...PLUGIN, unsound: true });

    expect(within(header()).getByRole('button', { name: '重试' })).toBeInTheDocument();
  });
});
