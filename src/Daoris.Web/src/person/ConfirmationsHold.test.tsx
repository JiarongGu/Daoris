import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../locales/i18n';
import { type Confirmation, Confirmations } from './Confirmations';

// A terminal's hold of a conversation: the one Confirm that lets a terminal speak for the person in a
// conversation says so plainly on its card, in each shipped language, for a start that holds and for the pipe's HOLD alike.
const START: Confirmation = {
  source: 'pipe', id: 'h1', module: 'DAORIS.DRIVER', route: 'START_CHAT',
  fields: [{ name: '/repository', type: 'string', value: 'game' }, { name: '/text', type: 'string', value: 'Fix the feed.' }],
  named: [{ role: 'repository', name: '/repository', value: 'game' }, { role: 'words', name: '/text', value: 'Fix the feed.' }],
  asker: { process: 'pwsh', pid: 4120 }, waitedOn: true, hold: true,
};
const HOLD: Confirmation = {
  source: 'pipe', id: 'h2', module: 'DAORIS.PIPE', route: 'HOLD', fields: [{ name: '/session', type: 'string', value: '3f2a91c4' }],
  named: [], asker: { process: 'pwsh', pid: 4120 }, waitedOn: true, hold: true,
};

const draw = (waiting: Confirmation[]) =>
  render(<Confirmations waiting={waiting} onAnswer={vi.fn(async () => ({ answered: true as const }))} notify={vi.fn()} />);

describe("a card whose Confirm makes a terminal's hold", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it.each([
    ['en', 'Confirm also lets this terminal speak for you in this conversation until it lets go.'],
    ['zh', '确认后，这个终端还能在这段对话中代你发言，直到它放开。'],
  ])('says what the Confirm lets through (%s)', async (language, sentence) => {
    await i18n.changeLanguage(language);
    draw([START]);
    expect(screen.getByText(sentence)).toBeInTheDocument();
  });

  it.each([
    ['en', 'A terminal asks to hold a conversation.'],
    ['zh', '一个终端请求持有一段对话。'],
  ])("words the pipe's HOLD and says the hold (%s)", async (language, head) => {
    await i18n.changeLanguage(language);
    draw([HOLD]);
    expect(screen.getAllByText(head).length).toBeGreaterThan(0);
    expect(screen.getByText(i18n.t('person.hold'))).toBeInTheDocument();
  });

  it('says nothing of a hold on a press that makes none', () => {
    draw([{ ...START, hold: false }]);
    expect(screen.queryByText(/speak for you in this conversation/)).toBeNull();
  });
});
