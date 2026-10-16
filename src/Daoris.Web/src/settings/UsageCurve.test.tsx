import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../locales/i18n';
import { CurveChart, CurvePoints } from './UsageCurve';

// The usage curve as the switch's setting draws and edits it (SWITCH1d; D165 point 2; the switch design §5): a small chart a
// window, the curve and each account of the list on it now, and the point editor *My own curve* opens. Molecules: props in,
// presses out.

describe('a window’s curve, drawn', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('draws the curve across the whole window, flat beyond its points, and names the window', () => {
    render(<CurveChart window="weekly" points={[[0, 20], [75, 95], [100, 95]]} own={false} accounts={[]} />);

    const chart = screen.getByRole('img', { name: "The weekly window: its curve, and each account's use now" });
    expect(chart.querySelector('[data-curve]')!.getAttribute('data-curve')).toBe('0:20 75:95 100:95');
    // Daoris's curve is no one's points: no marker is drawn on it.
    expect(chart.querySelectorAll('[data-point]')).toHaveLength(0);
    expect(screen.getByText("Across, the share of the window's time gone; up, the share used.")).toBeTruthy();
  });

  it('extends a curve of one point flat across the window, and marks the person’s own points', () => {
    render(<CurveChart window="session" points={[[20, 40], [60, 80]]} own accounts={[]} />);

    const chart = screen.getByRole('img');
    expect(chart.querySelector('[data-curve]')!.getAttribute('data-curve')).toBe('0:40 20:40 60:80 100:80');
    expect(chart.querySelectorAll('[data-point]')).toHaveLength(2);
  });

  /** Each account of the list where it stands now: named beside its dot, and over its curve in the warning hue. */
  it('places each account where it stands now, named, and over its curve in the warning hue', () => {
    render(
      <CurveChart
        window="weekly"
        points={[[0, 20], [75, 95], [100, 95]]}
        own={false}
        accounts={[
          { name: 'account-1', label: 'Gmail', gone: 29, used: 62, over: true },
          { name: 'account-2', label: 'work@example.invalid', gone: 50, used: 30, over: false },
        ]}
      />,
    );

    const over = document.querySelector('[data-account="account-1"]')!;
    expect(over.querySelector('circle')!.getAttribute('class')).toContain('fill-warn');
    expect(over.querySelector('title')!.textContent).toBe('Gmail: 62% used, 29% of the window gone');
    expect(within(over as HTMLElement).getByText('Gmail')).toBeTruthy();
    const under = document.querySelector('[data-account="account-2"]')!;
    expect(under.querySelector('circle')!.getAttribute('class')).toContain('fill-ink-soft');
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<CurveChart window="session" points={[[0, 90]]} own={false} accounts={[{ name: 'a', label: 'a', gone: 40, used: 20, over: false }]} />);
    expect(screen.getByRole('img', { name: '5 小时窗口：它的曲线，以及每个账户此刻的用量' })).toBeTruthy();
    expect(document.querySelector('title')!.textContent).toBe('a：已用 20%，窗口时间已过 40%');
  });
});

describe('a window’s own points, edited', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says a window it leaves out takes Daoris’s curve, and starts its own from Daoris’s points', async () => {
    const onSave = vi.fn();
    render(<CurvePoints window="weekly" points={null} hint="`daoris agent profile use claude-code --curve weekly=<elapsed>:<used>,…`" onSave={onSave} />);

    expect(screen.getByText("The weekly window takes Daoris's curve.")).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'Set my own curve' }));

    const editor = screen.getByRole('group', { name: 'Your points for the weekly window' });
    expect(within(editor).getAllByRole('spinbutton').map((field) => (field as HTMLInputElement).value)).toEqual(['0', '20', '75', '95', '100', '95']);
    await userEvent.clear(within(editor).getByRole('spinbutton', { name: 'Used, % · point 1' }));
    await userEvent.type(within(editor).getByRole('spinbutton', { name: 'Used, % · point 1' }), '30');
    await userEvent.click(within(editor).getByRole('button', { name: 'Save the curve' }));

    expect(onSave).toHaveBeenCalledWith([[0, 30], [75, 95], [100, 95]]);
  });

  /** A point added after the last, and one taken out; the terminal's twin of what is saved. */
  it('adds a point after the last and takes one out', async () => {
    const onSave = vi.fn();
    render(<CurvePoints window="session" points={[[0, 50], [80, 90]]} hint="`daoris agent profile use claude-code --curve session=0:50,80:90`" onSave={onSave} />);

    const editor = screen.getByRole('group', { name: 'Your points for the five-hour window' });
    expect(document.body.textContent).toContain('daoris agent profile use claude-code --curve session=0:50,80:90');
    await userEvent.click(within(editor).getByRole('button', { name: 'Add a row' }));
    await userEvent.click(within(editor).getByRole('button', { name: 'Remove point 1' }));
    await userEvent.click(within(editor).getByRole('button', { name: 'Save the curve' }));

    expect(onSave).toHaveBeenCalledWith([[80, 90], [90, 90]]);
  });

  /** Points that break a rule are said in the door's own sentence, and nothing is saved. */
  it('says what is wrong in the door’s sentence, and saves nothing', async () => {
    const onSave = vi.fn();
    render(<CurvePoints window="weekly" points={[[0, 30], [50, 70]]} hint="" onSave={onSave} />);

    const editor = screen.getByRole('group', { name: 'Your points for the weekly window' });
    await userEvent.clear(within(editor).getByRole('spinbutton', { name: 'Used, % · point 2' }));
    await userEvent.type(within(editor).getByRole('spinbutton', { name: 'Used, % · point 2' }), '20');
    await userEvent.click(within(editor).getByRole('button', { name: 'Save the curve' }));

    expect(screen.getByRole('alert').textContent)
      .toBe("`weekly`'s point 2 has used 20%, less than point 1's 30% — used never falls.");
    expect(onSave).not.toHaveBeenCalled();

    await userEvent.clear(within(editor).getByRole('spinbutton', { name: 'Used, % · point 2' }));
    await userEvent.click(within(editor).getByRole('button', { name: 'Save the curve' }));
    expect(screen.getByRole('alert').textContent).toBe("`weekly`'s point 2 is not two whole numbers [elapsed, used].");
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<CurvePoints window="weekly" points={null} hint="" onSave={() => {}} />);
    expect(screen.getByText('每周窗口沿用 Daoris 的曲线。')).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: '设定我的曲线' }));
    expect(screen.getByRole('group', { name: '你为每周窗口设的点' })).toBeTruthy();
  });
});
