import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, within } from '@testing-library/react';
import i18n from '../locales/i18n';
import { phaseTime } from '../locales/format';
import { Elapsed, PhaseList, type PhaseRow } from './phases';

/**
 * R2 (the UI/UX design, SIGNIN1): a step that waits says its phase and how long. Its time steps by its own clock, seconds in
 * the first minute and minutes after, and stops when its phase ends; its phases are a list, each named by its words and its
 * state in words, so a reader hears where it is, and never by hue alone.
 */
const START = Date.parse('2026-10-20T09:00:00.000Z');

describe('a phase\'s time', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(START + 5_000);
  });
  afterEach(() => vi.useRealTimers());

  it('is seconds in the first minute, then minutes, then hours', () => {
    expect([phaseTime(0), phaseTime(5_400), phaseTime(59_999), phaseTime(60_000), phaseTime(119_000), phaseTime(3_660_000)])
      .toEqual(['0s', '5s', '59s', '1m', '1m', '1h 1m']);
  });

  it('steps each second while under a minute and each minute after, and an ended one stands still', () => {
    render(<p><Elapsed from={START} /> · <Elapsed from={START} to={START + 12_000} /></p>);
    expect(screen.getByText('5s so far')).toBeTruthy();
    expect(screen.getByText('12s')).toBeTruthy();

    act(() => { vi.advanceTimersByTime(1_000); });
    expect(screen.getByText('6s so far')).toBeTruthy();

    act(() => { vi.advanceTimersByTime(54_000); });
    expect(screen.getByText('1m so far')).toBeTruthy();
    // Past the minute it moves no more than once a minute.
    act(() => { vi.advanceTimersByTime(30_000); });
    expect(screen.getByText('1m so far')).toBeTruthy();
    act(() => { vi.advanceTimersByTime(30_000); });
    expect(screen.getByText('2m so far')).toBeTruthy();
    expect(screen.getByText('12s')).toBeTruthy();
    // Nothing at rest keeps a timer.
    expect(vi.getTimerCount()).toBe(1);
  });

  it('says so far in Chinese too', async () => {
    await i18n.changeLanguage('zh');
    try {
      render(<Elapsed from={START} />);
      expect(screen.getByText('已用 5 秒')).toBeTruthy();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});

const PHASES: PhaseRow[] = [
  { key: 'start', label: 'Starting Codex', state: 'done', from: START, to: START + 1_000 },
  { key: 'browser', label: 'Waiting on your browser', state: 'waits', from: START + 1_000, body: <p>ABCD-12345</p> },
  { key: 'who', label: 'Checking who signed in', state: 'to-come' },
  { key: 'lists', label: 'Joining the lists', state: 'to-come' },
];

describe('the phase list', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(START + 48_000);
  });
  afterEach(() => vi.useRealTimers());

  it('names each phase by its words and its state in words, the done one with its time and the one now with its body', () => {
    render(<PhaseList label="Signing in to work-2" phases={PHASES} />);

    const list = screen.getByRole('list', { name: 'Signing in to work-2' });
    const items = within(list).getAllByRole('listitem');
    expect(items.map((item) => item.textContent)).toEqual([
      expect.stringMatching(/^Starting Codex.*done.*1s$/),
      expect.stringMatching(/^Waiting on your browser.*waits on you.*47s so far.*ABCD-12345$/),
      expect.stringMatching(/^Checking who signed in.*to come$/),
      expect.stringMatching(/^Joining the lists.*to come$/),
    ]);
    // The phase now is told once as a status, by its words and its state: never its ticking time.
    expect(screen.getByRole('status').textContent).toBe('Waiting on your browser, waits on you');
  });

  it('says a failed phase\'s sentence whole on its line, the earlier ones done', () => {
    render(<PhaseList label="Signing in" phases={[
      PHASES[0]!,
      { key: 'browser', label: 'Waiting on your browser', state: 'failed', from: START + 1_000, to: START + 9_000, sentence: 'Sign-in did not finish (the tool exited 1).' },
      PHASES[2]!,
    ]} />);

    expect(screen.getByRole('alert').textContent).toBe('Sign-in did not finish (the tool exited 1).');
    expect(screen.getAllByRole('listitem')[1]!.textContent).toMatch(/^Waiting on your browser.*failed.*8s/);
  });

  it('as a line alone says the phase now with its body, and the whole\'s time, and lists none of the others', () => {
    render(<PhaseList label="Signing in" phases={PHASES} shown="line" />);

    expect(screen.queryByRole('list')).toBeNull();
    expect(screen.getByText('Waiting on your browser')).toBeTruthy();
    expect(screen.getByText('47s so far')).toBeTruthy();
    expect(screen.getByText('48s so far')).toBeTruthy();
    expect(screen.getByText('ABCD-12345')).toBeTruthy();
    expect(screen.queryByText('Checking who signed in')).toBeNull();
  });

  it('says the states in Chinese', async () => {
    await i18n.changeLanguage('zh');
    try {
      render(<PhaseList label="登录" phases={PHASES} />);
      const items = screen.getAllByRole('listitem');
      expect(items[0]!.textContent).toMatch(/已完成/);
      expect(items[1]!.textContent).toMatch(/等你操作/);
      expect(items[2]!.textContent).toMatch(/待进行/);
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});
