import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import { settingOf } from '../DriverDomain';
import { ChecksSetting } from './ChecksSetting';

// How many checks the machine runs at once: auto a switch, a pinned number beside it, each change written once, and neither
// control pressed again while a change is being written.

afterEach(async () => { await i18n.changeLanguage('en'); });

const row = (held: number | null, onSet = vi.fn(), busy = false) => render(
  <Tooltip.Provider>
    <ChecksSetting setting={settingOf('checks')} held={held} onSet={onSet} busy={busy} />
  </Tooltip.Provider>,
);

describe("the checks row", () => {
  it('writes a pinned number typed, and auto from its switch', () => {
    const onSet = vi.fn();
    row(2, onSet);
    const input = screen.getByRole('spinbutton', { name: 'Checks at once' });
    fireEvent.change(input, { target: { value: '3' } });
    fireEvent.blur(input);
    fireEvent.click(screen.getByRole('checkbox', { name: 'As many as the memory admits' }));

    expect(onSet.mock.calls).toEqual([[3], [null]]);
  });

  it('holds both controls while a change is being written', () => {
    const onSet = vi.fn();
    row(2, onSet, true);

    expect(screen.getByRole('spinbutton', { name: 'Checks at once' })).toBeDisabled();
    const auto = screen.getByRole('checkbox', { name: 'As many as the memory admits' });
    expect(auto).toBeDisabled();
    fireEvent.click(auto);
    expect(onSet).not.toHaveBeenCalled();
  });
});
