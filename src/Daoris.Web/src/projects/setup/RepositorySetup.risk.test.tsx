import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import { repositoryRisk } from '../../settings/place/PlaceRisk';
import { SETUP_DEFAULTS } from '../fixtures';
import { RepositorySetup, type RepositorySetupProps, SESSIONS_SECTIONS } from './RepositorySetup';

// A repository's Setup carries its own risk row, *Risk and your marks*, read from `STATE`'s `placeRisk` (`repositoryRisk`): its own row, else
// its workspace's or the machine's, said as where it comes from; and each press goes out as the edit for this repository alone.

const rows = [
  { place: 'machine', risk: 'medium', marks: [] },
  { place: 'workspace/aurora', risk: 'low', marks: ['release.production'] },
];

const draw = (risk: RepositorySetupProps['risk'], only?: RepositorySetupProps['only']) => {
  render(
    <Tooltip.Provider>
      <RepositorySetup {...SETUP_DEFAULTS} risk={risk} {...(only ? { only } : {})} />
    </Tooltip.Provider>,
  );
};

const rowOf = () => screen.getByText('Risk and your marks', { selector: 'span' }).closest('[data-setting-row]') as HTMLElement;

afterEach(async () => { await i18n.changeLanguage('en'); });

describe("a repository's risk row", () => {
  it('shows the workspace’s risk and mark where the repository sets none, and sends a press as this repository’s', async () => {
    const onChange = vi.fn();
    draw({ ...repositoryRisk(rows, 'engine', 'aurora'), workspace: 'aurora', onChange }, SESSIONS_SECTIONS);

    expect(within(rowOf()).getByRole('radio', { name: 'Low' })).toBeChecked();
    expect(within(rowOf()).getByText(/From its workspace\..*production releases stay yours/)).toBeInTheDocument();

    await userEvent.click(within(rowOf()).getByRole('radio', { name: 'High' }));

    expect(onChange).toHaveBeenCalledWith({ risk: 'high' });
  });

  it('shows a row set for the repository as its own, over its workspace’s', () => {
    draw({ ...repositoryRisk([...rows, { place: 'repository/engine', risk: 'high', marks: [] }], 'engine', 'aurora'), workspace: 'aurora', onChange: vi.fn() });

    expect(within(rowOf()).getByRole('radio', { name: 'High' })).toBeChecked();
    expect(within(rowOf()).getByText(/Set for this place\./)).toBeInTheDocument();
  });

  it('says it in 中文 and offers no row where the driver answers none', async () => {
    draw({ ...repositoryRisk(rows, 'engine', 'aurora'), workspace: 'aurora', onChange: vi.fn() });
    await i18n.changeLanguage('zh');
    expect(screen.getByText('风险与你的标记', { selector: 'span' })).toBeInTheDocument();
  });

  it('offers no row where the driver answers no risk record', () => {
    draw(null);

    expect(screen.queryByText('Risk and your marks')).toBeNull();
  });
});
