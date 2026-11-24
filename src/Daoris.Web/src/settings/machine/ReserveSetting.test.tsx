import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import { settingOf } from '../DriverDomain';
import { type ReserveFacts, ReserveSetting } from './ReserveSetting';

// The floor of what the machine keeps free for the person's own work: auto a switch, a number of GB beside it, the machine's use and Daoris's own
// share said under it, each change written once, and neither control pressed again while a change is being written.

afterEach(async () => { await i18n.changeLanguage('en'); });

const auto: ReserveFacts = { gb: null, kept: 21, total: 64, freeCommit: 20.5, daoris: 3.2 };

const row = (held: ReserveFacts, onSet = vi.fn(), busy = false) => render(
  <Tooltip.Provider>
    <ReserveSetting setting={settingOf('reserve')} held={held} onSet={onSet} busy={busy} />
  </Tooltip.Provider>,
);

describe('the reserve row', () => {
  it('says the reserve in force beside how the machine stands and what Daoris holds', () => {
    row(auto);

    expect(screen.getByText("Keeping 21 GB free. This machine: 64 GB of memory, 20.5 GB free commit; Daoris's own processes hold 3.2 GB.")).toBeTruthy();
  });

  it('says why the room is what it is: the person\'s share and swing, the limit with its signal, and what a gate needs', () => {
    row({
      ...auto,
      kept: 5.3,
      why: { person: 24.5, swing: 4, margin: 1.3, floor: 0, keepBy: 'headroom', limit: 2, ceiling: 4, signal: 'cpu', busy: 0.95, need: 6.6, needSource: 'learned', needRuns: 5 },
    });

    expect(screen.getByText('Why: your own work holds 24.5 GB and has stood 4 GB higher in the last ten minutes, plus a 1.3 GB margin; the measured headroom sets what is kept.')).toBeTruthy();
    expect(screen.getByText('Room: 2 of 4 heavy runs at once (backed off: the processors saturated, processors 95% busy); a gate needs 6.6 GB (learned from 5 runs on this machine).')).toBeTruthy();
  });

  it('says the same in Chinese', async () => {
    await i18n.changeLanguage('zh');
    row({
      ...auto,
      why: { person: 24.5, swing: 4, margin: 1.3, floor: 0, keepBy: 'floor', limit: 4, ceiling: 4, signal: 'start', busy: null, need: 8, needSource: 'declared', needRuns: 0 },
    });

    expect(screen.getByText(/^余量：同时 4 个（上限 4 个）重型运行（未见压力）；一个检查需要 8 GB（声明值，在本机运行前使用）。$/)).toBeTruthy();
  });

  it('writes a number typed, and auto from its switch', () => {
    const onSet = vi.fn();
    row({ ...auto, gb: 24, kept: 24 }, onSet);
    const input = screen.getByRole('spinbutton', { name: 'Kept free for your own work' });
    fireEvent.change(input, { target: { value: '30' } });
    fireEvent.blur(input);
    fireEvent.click(screen.getByRole('checkbox', { name: "Measured from your own work (no floor)" }));

    expect(onSet.mock.calls).toEqual([[30], [null]]);
  });

  it('takes none: 0 keeps nothing back, and says so', () => {
    const onSet = vi.fn();
    row({ ...auto, gb: 24, kept: 24 }, onSet);
    const input = screen.getByRole('spinbutton', { name: 'Kept free for your own work' });
    fireEvent.change(input, { target: { value: '0' } });
    fireEvent.blur(input);

    expect(onSet.mock.calls).toEqual([[0]]);
  });

  it('says nothing is kept back where the reserve is none', () => {
    row({ ...auto, gb: 0, kept: 0 });

    expect(screen.getByText(/^Keeping nothing back\./)).toBeTruthy();
  });

  it('holds both controls while a change is being written', () => {
    const onSet = vi.fn();
    row({ ...auto, gb: 24, kept: 24 }, onSet, true);

    expect(screen.getByRole('spinbutton', { name: 'Kept free for your own work' })).toBeDisabled();
    const switchAuto = screen.getByRole('checkbox', { name: "Measured from your own work (no floor)" });
    expect(switchAuto).toBeDisabled();
    fireEvent.click(switchAuto);
    expect(onSet).not.toHaveBeenCalled();
  });
});
