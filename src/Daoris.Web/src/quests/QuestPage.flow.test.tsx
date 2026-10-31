import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import { HELD, OPEN, WORKING } from './fixtures';
import { QuestPage } from './QuestPage';

// REVIEWFLOW1b (the review-flow design §1) on the quest's page: the head's facts lead with what the page reads, and *next* is the
// press that moves it on, a door to its step. Nothing is said that the page cannot read (D137).

const nothing = () => {};
const page = (props: Partial<Parameters<typeof QuestPage>[0]> = {}) => render(
  <QuestPage quest={OPEN} onRespond={nothing} onDismiss={nothing} onOpenQuest={nothing} {...props} />,
);
const head = () => screen.getByRole('main').querySelector('header')!;
const waiting = { ...WORKING, state: 'awaiting-person' as const };

describe('a quest’s now · next', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  for (const [language, now, next] of [
    ['en', /waits on you/, 'next: Open in Sessions'],
    ['zh', /等你/, '下一步：在「会话」中打开'],
  ] as const) {
    it(`says it waits on the person, timed, and names its session as next, in ${language}`, async () => {
      await i18n.changeLanguage(language);
      const onAttend = vi.fn();
      page({ quest: { ...OPEN, status: 'Taken' }, session: waiting, onAttend });

      expect(head()).toHaveTextContent(now);
      await userEvent.click(within(head()).getByRole('button', { name: next }));
      expect(onAttend).toHaveBeenCalledWith(waiting.id);
    });
  }

  it('names the departure’s accept as next where the quest is held for it, and presses it', async () => {
    const onAccept = vi.fn();
    page({ quest: HELD, onAccept });

    await userEvent.click(within(head()).getByRole('button', { name: 'next: Accept the departure' }));
    expect(onAccept).toHaveBeenCalled();
  });

  it('names no next where nothing waits on the person, a working session included', () => {
    page({ quest: { ...OPEN, status: 'Taken' }, session: WORKING, onAttend: nothing });

    expect(head()).not.toHaveTextContent('next:');
  });
});
