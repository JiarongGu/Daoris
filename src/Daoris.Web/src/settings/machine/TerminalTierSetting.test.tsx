import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../../locales/i18n';
import { TerminalTierSetting } from './TerminalTierSetting';

// The tier of a terminal's presses, a row of Settings → Agents and sessions: ask, the default, or act, the same `terminal`
// `daoris settings terminal ask|act` sets.

const draw = (held: 'ask' | 'act', onSet = vi.fn()) => render(
  <Tooltip.Provider>
    <TerminalTierSetting held={held} onSet={onSet} />
  </Tooltip.Provider>,
);

describe("the terminal's tier row", () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it.each([
    ['en', "A terminal's presses", 'Run at once as yours', 'Ask: each press from a terminal on this machine waits for your Confirm here.'],
    ['zh', '终端的操作', '直接以你的名义执行', '询问：本机终端发来的每个操作都在这里等你确认。'],
  ])('says the tier held and what it governs (%s)', async (language, label, act, state) => {
    await i18n.changeLanguage(language);
    draw('ask');

    expect(screen.getAllByText(label).length).toBeGreaterThan(0);
    expect(screen.getByRole('checkbox', { name: act })).not.toBeChecked();
    expect(screen.getByText(state)).toBeInTheDocument();
    expect(document.body.textContent).not.toMatch(/\{\{|undefined/);
  });

  it('turns act on and back to ask', async () => {
    const onSet = vi.fn();
    const { unmount } = draw('ask', onSet);
    await userEvent.click(screen.getByRole('checkbox', { name: 'Run at once as yours' }));
    expect(onSet).toHaveBeenLastCalledWith('act');
    unmount();

    draw('act', onSet);
    expect(screen.getByText(/Act: a press from a terminal on this machine that no Daoris session owns runs at once/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('checkbox', { name: 'Run at once as yours' }));
    expect(onSet).toHaveBeenLastCalledWith('ask');
  });
});
