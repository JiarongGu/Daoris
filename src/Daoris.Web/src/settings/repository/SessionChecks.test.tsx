import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../../locales/i18n';
import type { Answered } from '../../work/InlineConfirm';
import { SessionChecksRow, type SessionChecksValue, sessionChecksSummary, sessionChecksToast } from './SessionChecks';

type Press = (checks: SessionChecksValue | null, answered: Answered) => void;

// FABRIC-PIPELINE1b (D192 point 4; the pipeline design §3.1, §3.2): which checks a repository's sessions run, shown and set on its Setup
// as `daoris-driver checks` sets it from a terminal (D50). Under light it names the light gates and the heavy gates, says that an
// agent taking no rules is told them and refused run-heavy rather than refused in its shell, and that a repository marking no heavy
// gate gives its sessions every gate; the driver judges the press.

const t = i18n.t.bind(i18n);

const draw = (
  checks: SessionChecksValue | null,
  { light = ['universal', 'code-map'], heavy = ['driver', 'web'], known = true, onChange = vi.fn<Press>() }:
    { light?: string[] | null; heavy?: string[]; known?: boolean; onChange?: ReturnType<typeof vi.fn<Press>> } = {},
) => {
  render(
    <Tooltip.Provider>
      <SessionChecksRow repository="engine" checks={checks} light={light} heavy={heavy} known={known} onChange={onChange} />
    </Tooltip.Provider>,
  );
  return onChange;
};

afterEach(async () => { await i18n.changeLanguage('en'); });

describe("a repository's sessions' checks, said", () => {
  it('folds to a word where one is set, and toasts what a press did', () => {
    expect(sessionChecksSummary(t, 'light')).toBe('light checks');
    expect(sessionChecksSummary(t, 'heavy-at-end')).toBe('heavy gates at the end');
    expect(sessionChecksSummary(t, null)).toBeNull();
    expect(sessionChecksToast(t, 'engine', 'light')).toBe('engine’s sessions now run light checks: its heavy gates are left to its verification.');
    expect(sessionChecksToast(t, 'engine', null)).toBe('engine’s sessions run their checks as they did.');
  });

  it('under light, names the light gates and the heavy ones, and says what takes no rules is told, never refused in its shell', () => {
    draw('light');

    expect(screen.getByRole('radio', { name: 'Light' })).toBeChecked();
    expect(screen.getByText(/Light gates: universal, code-map\. Heavy gates: driver, web\./)).toBeInTheDocument();
    expect(screen.getByText(/an agent that takes none is told the heavy gates, and run-heavy refuses its tree, never its shell/)).toBeInTheDocument();
    expect(screen.getByText(/Daoris does not run the heavy gates after a landing yet/)).toBeInTheDocument();
  });

  it('says a repository that marks no heavy gate gives its sessions every gate, and nothing is refused', () => {
    draw('light', { heavy: [] });

    expect(screen.getByText('engine marks no heavy gate, so its sessions may run every gate.')).toBeInTheDocument();
    expect(screen.queryByText(/refuses its tree/)).not.toBeInTheDocument();
  });

  it('says nothing of the gates until the driver has read them', () => {
    draw('light', { known: false });

    expect(screen.queryByText(/Light gates:/)).not.toBeInTheDocument();
    expect(screen.queryByText(/marks no heavy gate/)).not.toBeInTheDocument();
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    draw('light', { heavy: [] });

    expect(screen.getByRole('radio', { name: '轻量' })).toBeChecked();
    expect(screen.getByText('engine 没有标出重型检查，所以它的会话可以运行每一项检查。')).toBeInTheDocument();
  });
});

describe("the sessions' checks control", () => {
  it('sends the choice pressed, and none for as before', async () => {
    const onChange = draw(null);
    expect(screen.getByRole('radio', { name: 'As before' })).toBeChecked();

    await userEvent.click(screen.getByRole('radio', { name: 'Light' }));
    expect(onChange).toHaveBeenLastCalledWith('light', expect.anything());

    await userEvent.click(screen.getByRole('radio', { name: 'Heavy at end' }));
    expect(onChange).toHaveBeenLastCalledWith('heavy-at-end', expect.anything());
  });

  it('sends a clear for as before, and nothing for the choice set already', async () => {
    const onChange = draw('light');

    await userEvent.click(screen.getByRole('radio', { name: 'Light' }));
    expect(onChange).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('radio', { name: 'As before' }));
    expect(onChange).toHaveBeenCalledWith(null, expect.anything());
  });

  it("says the driver's refusal in its words", async () => {
    const onChange = vi.fn<Press>((_, answered) => answered.refused('The file does not read.'));
    draw(null, { onChange });

    await userEvent.click(screen.getByRole('radio', { name: 'Light' }));

    expect(await screen.findByText('The file does not read.')).toBeInTheDocument();
  });
});
