import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import type { Ask, Quest, Session } from '../data/api';
import { PUBLISHED } from '../asks/fixtures';
import { DONE, OPEN, TAKEN, WORKING } from '../quests/fixtures';
import { type HelpPublishedEntry, publishedCounts, publishedItems, type PublishedLists } from './published';
import { PublishedList } from './PublishedList';

// ASKQUESTS1 (the Ask Daoris design §9 item 12): what a conversation published, in a strip of its own, folded to one line of what
// stands, opened in place, each row one press that opens its record on Quests.

const ASK: Ask = { ...PUBLISHED, id: 'a1b2c3d4', sentence: 'cap the chunk streamer’s hydration', short: null, quests: [OPEN.id, TAKEN.id] };
const WORKED: Session = { ...WORKING, id: 's9w8v7u6', quest: TAKEN.id, state: 'working' };

const entries: HelpPublishedEntry[] = [
  { at: '2026-10-22T10:00:00+00:00', proposal: 'p2', kind: 'setup', workspace: 'work', ask: null, quest: DONE.id },
  { at: '2026-10-22T09:00:00+00:00', proposal: 'p1', kind: 'ask', workspace: 'work', ask: ASK.id, quest: OPEN.id },
];

function show(lists: PublishedLists, open = false, onOpen = vi.fn(), onToggle = vi.fn(), failed = false) {
  const items = publishedItems(entries, lists);
  render(<PublishedList items={items} counts={publishedCounts(items)} failed={failed} open={open} onToggle={onToggle} onOpen={onOpen} />);
  return { onOpen, onToggle };
}

const LISTS: PublishedLists = { quests: [OPEN, TAKEN, DONE], asks: [ASK], sessions: [WORKED] };

describe('what a conversation published', () => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('folds to one line of what stands, each count with its noun, and opens in place', async () => {
    const { onToggle } = show(LISTS);

    const head = screen.getByRole('button', { name: /Published from this conversation/ });
    expect(head).toHaveAttribute('aria-expanded', 'false');
    expect(head).toHaveTextContent('Published from this conversation · 3 quests · 1 open · 1 taken · 1 done');
    expect(screen.queryByRole('list')).not.toBeInTheDocument();
    await userEvent.click(head);
    expect(onToggle).toHaveBeenCalledOnce();
  });

  it('opened, each ask holds the quests it became, each row with its state, and a press opens its record', async () => {
    const onOpen = vi.fn();
    show(LISTS, true, onOpen);

    const list = screen.getByRole('list', { name: 'What this conversation published' });
    expect(within(list).getByRole('button', { name: `Open quest ${DONE.title}` })).toHaveTextContent('done');
    const taken = within(list).getByRole('button', { name: `Open quest ${TAKEN.title}` });
    expect(taken).toHaveTextContent('taken');
    // Taken by which session: the one working it here, by its state.
    expect(taken).toHaveTextContent('session working');
    expect(taken).toHaveTextContent(`to ${TAKEN.to}`);

    await userEvent.click(within(list).getByRole('button', { name: `Open ask ${ASK.sentence}` }));
    await userEvent.click(taken);
    expect(onOpen.mock.calls).toEqual([[{ view: 'quests', item: `ask:${ASK.id}` }], [{ view: 'quests', item: TAKEN.id }]]);
  });

  it('says reading while the lists are not answered, never nothing, and a record gone opens nothing', () => {
    show({ quests: undefined, asks: undefined, sessions: undefined }, true);
    expect(screen.getByRole('button', { name: /Published from this conversation/ })).toHaveTextContent('reading where each stands…');
    cleanup();

    show({ quests: [OPEN, TAKEN], asks: [], sessions: [] }, true);
    expect(screen.getByRole('button', { name: /Published from this conversation/ })).toHaveTextContent('2 no longer on this machine');
    expect(screen.queryByRole('button', { name: `Open quest #${DONE.id}` })).not.toBeInTheDocument();
    expect(screen.getAllByText('no longer on this machine')).toHaveLength(2);
  });

  it('says a failed read in place of its counts', () => {
    show(LISTS, false, vi.fn(), vi.fn(), true);
    expect(screen.getByRole('button', { name: /Published from this conversation/ })).toHaveTextContent('where each stands could not be read');
  });

  it('counts what waits on the person first: a done a departure holds, in the words of its own list', () => {
    const held: Quest = { ...DONE, held: true, hold: 'departed' };
    show({ quests: [OPEN, TAKEN, held], asks: [ASK], sessions: [] }, true);

    expect(screen.getByRole('button', { name: /Published from this conversation/ })).toHaveTextContent('1 waits on you');
    expect(screen.getByRole('button', { name: `Open quest ${DONE.title}` })).toHaveTextContent('awaits your yes');
  });

  it('is worded in Chinese by the glossary’s names', async () => {
    await i18n.changeLanguage('zh');
    show(LISTS);

    expect(screen.getByRole('button', { name: /本对话发布的/ })).toHaveTextContent('本对话发布的 · 3 项委托 · 1 项待接 · 1 项已接下 · 1 项已完成');
  });
});
