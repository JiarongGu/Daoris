import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import { SETUP_DEFAULTS } from '../fixtures';
import { RepositorySetup, type RepositorySetupProps } from './RepositorySetup';

// A repository's Setup carries the growth switch as its Sessions row, *New repositories and rooms*: the value standing for it (its own, its workspace's
// or `mode`), where it is set, and a press that goes out as the edit's value. The person's alone, so it offers no clear and no agent press.

const draw = (growth: NonNullable<RepositorySetupProps['sessions']>['growth'], opened = true, onGrowth = vi.fn()) => {
  render(
    <Tooltip.Provider>
      <RepositorySetup {...SETUP_DEFAULTS} sessions={{ ...SETUP_DEFAULTS.sessions!, growth, onGrowth }} {...(opened ? { open: 'sessions' as const } : {})} />
    </Tooltip.Provider>,
  );
  return onGrowth;
};

const rowOf = () => screen.getByText('New repositories and rooms', { selector: 'span' }).closest('[data-setting-row]') as HTMLElement;

afterEach(async () => { await i18n.changeLanguage('en'); });

describe("a repository's growth row", () => {
  it('shows the value that stands, from the workspace, and sends the value pressed', async () => {
    const onGrowth = draw({ repository: 'engine', workspace: 'aurora', value: 'off', source: 'workspace' });

    expect(within(rowOf()).getByRole('radio', { name: 'Off' })).toBeChecked();
    expect(within(rowOf()).getByText(/is refused, even in full access.* From the workspace aurora\./)).toBeInTheDocument();
    expect(within(rowOf()).queryByRole('button', { name: /clear/i })).toBeNull();

    await userEvent.click(within(rowOf()).getByRole('radio', { name: 'As the mode says' }));

    expect(onGrowth).toHaveBeenCalledWith('mode');
  });

  it('shows a value set for this repository as its own, and the section opens on its own for it', () => {
    draw({ repository: 'engine', workspace: 'aurora', value: 'asked', source: 'repository' }, false);

    expect(screen.getByRole('button', { name: 'Sessions' })).toHaveAttribute('aria-expanded', 'true');
    expect(within(rowOf()).getByRole('radio', { name: 'Ask me' })).toBeChecked();
    expect(within(rowOf()).getByText(/Set for this repository\./)).toBeInTheDocument();
  });

  it("folds into the Sessions line, marked where it comes from, and says it in 中文", async () => {
    draw({ repository: 'engine', workspace: 'aurora', value: 'asked', source: 'workspace' }, false);

    expect(screen.getByRole('button', { name: 'Sessions' })).toHaveAccessibleDescription(expect.stringContaining('new places asked each time'));
    await userEvent.click(screen.getByRole('button', { name: 'Sessions' }));
    await i18n.changeLanguage('zh');
    expect(screen.getByText('新仓库与新房间', { selector: 'span' })).toBeInTheDocument();
  });

  it('offers no row where the driver answers none', () => {
    draw(undefined);

    expect(screen.queryByText('New repositories and rooms')).toBeNull();
  });
});
