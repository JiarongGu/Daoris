import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, describe, expect, it, vi } from 'vitest';

// The second check in the conversation: one line under the last turn a reading covers, each state in the catalogue's words, its
// Details with the reader as reported and its findings, the packet asked only once opened, and the chip beside the access chip.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', async () => ({
  ...(await import('../../test/sliceShell')).mockedShell(invoke),
  useShenoraEvent: () => undefined,
}));

import i18n from '../../locales/i18n';
import { type Reading, readingsAnswer } from '../../bridge/helpReadings';
import { ReadingLine, readingsBeneath } from './ReadingLine';
import { SecondCheckChip } from './SecondCheckChip';

const reading = (over: Partial<Reading>): Reading => ({
  range: 'r1', state: 'read', opened: '2026-11-12T09:00:00Z', closed: '2026-11-12T09:02:00Z', last: 'w3', turns: ['w3'], limits: [],
  why: null, waiting: null, reader: { agent: 'codex-acp', model: null, ran: 'gpt-6.1-sol', tokens: 900 }, independence: 'another-maker',
  findings: [], more: 0, leave: null, reasons: [], ...over,
});

const flag = { index: 0, severity: 'flag' as const, certainty: 'likely' as const, says: 'pushed though only a commit was asked', call: 'p7', instruction: 'w3', seen: null, told: null };

function shown(node: React.ReactNode) {
  return render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>{node}</QueryClientProvider>);
}

describe('the second check in the conversation', () => {
  afterEach(async () => {
    invoke.mockReset();
    await i18n.changeLanguage('en');
  });

  it.each([
    [reading({ state: 'queued', reader: null }), 'Second check: queued'],
    [reading({ state: 'queued', reader: null, waiting: 'every choice is signed out' }), 'Second check: queued: every choice is signed out'],
    [reading({ state: 'running', reader: null }), 'Second check: reading…'],
    [reading({}), 'Second check: nothing found'],
    [reading({ findings: [flag, { ...flag, index: 1, severity: 'note' }] }), 'Second check: flagged 1 · 1 note'],
    [reading({ state: 'partial', limits: [{ code: 'cut', call: 'c1' }] }), 'Second check: partial · nothing found in what was read'],
    [reading({ state: 'left' }), 'Second check: could not judge'],
    [reading({ state: 'unread', reader: null, why: 'Daoris restarted' }), 'Second check: not read: Daoris restarted'],
    [reading({ state: 'disabled', reader: null }), 'Second check: off when this turn ended'],
  ])('says each state in its words', (each, said) => {
    shown(<ReadingLine session="s1" reading={each} />);
    expect(screen.getByText(said)).toBeTruthy();
  });

  it('opens its Details on the reader as reported and each finding, and asks for what was sent only once opened', async () => {
    invoke.mockResolvedValue({ session: 's1', check: { on: true, pending: 0, today: {} }, readings: [], next: null, packet: 'the packet as sent' });
    shown(<ReadingLine session="s1" reading={reading({ findings: [flag], limits: [{ code: 'delegated', call: null }], state: 'partial' })} />);

    await userEvent.click(screen.getByRole('button', { name: 'Details' }));
    expect(screen.getByText('Read by codex-acp (gpt-6.1-sol, as reported), another maker')).toBeTruthy();
    expect(screen.getByText('delegated, not read')).toBeTruthy();
    expect(screen.getByText(/pushed though only a commit was asked/)).toBeTruthy();
    expect(screen.getByText('on call p7 · your words w3')).toBeTruthy();
    expect(invoke).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('button', { name: 'What was sent' }));
    expect(await screen.findByText('the packet as sent')).toBeTruthy();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_READINGS', { payload: { session: 's1', range: 'r1', limit: 1 } });
  });

  it('draws a reading under the last turn it covers and nothing under a turn not selected', () => {
    const beneath = readingsBeneath('s1', [reading({ last: 'w3' })]);
    expect(beneath({ id: 'w2' })).toBeNull();
    shown(<>{beneath({ id: 'w3' })}</>);
    expect(screen.getByText('Second check: nothing found')).toBeTruthy();
  });

  it('reads an answer field by field, leaving out a reading that does not read', () => {
    const page = readingsAnswer({
      check: { on: true, pending: 2, today: { readings: 3, tokens: 2400, unknown: 1 } },
      readings: [{ range: 'r1', state: 'partial', findings: [{ index: 0, severity: 'flag', certainty: 'sure', says: 'x', call: 'p1', instruction: 'none' }] }, { range: 'r2', state: 'clean' }],
      next: 'r1',
    });
    expect(page.check).toEqual({ on: true, pending: 2, today: { readings: 3, tokens: 2400, unknown: 1 } });
    expect(page.readings.map((each) => each.range)).toEqual(['r1']);
    expect(page.readings[0].findings[0].instruction).toBe('none');
    expect(page.next).toBe('r1');
  });

  it('says on the chip whether the check is on and how many of the conversation are pending, and its spend in its popover, in Chinese too', async () => {
    const { rerender } = shown(<SecondCheckChip check={{ on: true, pending: 2, today: { readings: 3, tokens: 2400, unknown: 1 } }} />);
    expect(screen.getByRole('button', { name: 'Second check on · 2 pending' })).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'Second check on · 2 pending' }));
    expect(screen.getByText('3 readings today on this machine')).toBeTruthy();
    expect(screen.getByText('2400 tokens reported')).toBeTruthy();
    expect(screen.getByText('1 reading reported no tokens')).toBeTruthy();

    await i18n.changeLanguage('zh');
    rerender(<QueryClientProvider client={new QueryClient()}><SecondCheckChip check={{ on: false, pending: 0, today: { readings: 0, tokens: 0, unknown: 0 } }} /></QueryClientProvider>);
    expect(screen.getByRole('button', { name: '复核已关闭' })).toBeTruthy();
  });
});
