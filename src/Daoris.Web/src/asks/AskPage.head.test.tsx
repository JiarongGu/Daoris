import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { Ask } from '../api';
import i18n from '../i18n';
import type { WorkDoor } from '../work/pausing';
import { PAUSABLE_ASK, PAUSED_ASK } from '../work/pausingFixtures';
import { AskPage } from './AskPage';
import { LONG_ASK, PUBLISHED, WITH_GO_AHEADS } from './fixtures';

// An ask's head as a quest's (HEAD2d, D162 §4, the entry-and-heads design §0 and §2): its state leads, its title is
// the request's first line on two lines at most, one line of facts says where it goes, its age and the live fact, its
// primary act is drawn and the rest are in its ⋯ with its id, and *View full request* reads what the title cut.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const door = (over: Partial<WorkDoor> = {}): WorkDoor => ({
  plan: PAUSABLE_ASK, wired: false, busy: false, onPause: vi.fn(), onResume: vi.fn(), onAbandon: vi.fn(), ...over,
});

const page = (ask: Ask = { ...PUBLISHED, deletable: true }, work?: WorkDoor) => {
  render(<AskPage ask={ask} receivers={['engine']} questTitles={{}} onPublish={vi.fn()} onClose={vi.fn()} onDelete={vi.fn()} onOpenQuest={vi.fn()} work={work} />);
  return screen.getByRole('main').querySelector('header')!;
};

const opened = async () => {
  const user = userEvent.setup();
  screen.getByRole('button', { name: i18n.t('work.head.more') }).focus();
  await user.keyboard('{Enter}');
  return (await screen.findAllByRole('menuitem')).map((item) => item.textContent);
};

describe('an ask’s head (HEAD2d)', () => {
  afterEach(async () => { cleanup(); await i18n.changeLanguage('en'); });

  it('leads with its state, then paused, and clamps its title to two lines, whole in its tip', () => {
    const head = page(LONG_ASK, door({ plan: PAUSED_ASK }));
    const title = within(head).getByRole('heading', { level: 1 });
    expect(title.className).toContain('line-clamp-2');
    expect(title).toHaveAttribute('title', LONG_ASK.sentence);
    const text = head.textContent!;
    expect(text.indexOf('proposed')).toBeLessThan(text.indexOf('paused'));
    expect(text.indexOf('paused')).toBeLessThan(text.indexOf(title.textContent!.slice(0, 20)));
  });

  it('says its id only in its ⋯ and its details, never in the head', async () => {
    const head = page();
    expect(head.textContent).not.toContain(`#${PUBLISHED.id}`);
    expect(await opened()).toContain('Copy ask ID');
    expect(screen.getByRole('main')).toHaveTextContent(`#${PUBLISHED.id}`);
  });

  it('says one line of facts: where it goes, its age, and its open quests', () => {
    const head = page({ ...PUBLISHED, quests: ['a1', 'b2'] });
    expect(head).toHaveTextContent('workspace aurora · asked 3h ago · 2 quests');
  });

  it('says it waits on the person where a go-ahead does', () => {
    const waiting: Ask = {
      ...WITH_GO_AHEADS,
      goAheads: [{ ...WITH_GO_AHEADS.goAheads![0]!, state: 'asked', answer: null }],
    };
    expect(page(waiting)).toHaveTextContent('waiting on you');
  });

  it('draws only its primary act, Resume where paused, and folds the rest into its ⋯', async () => {
    const head = page(undefined, door({ plan: PAUSED_ASK }));
    expect(within(head).getAllByRole('button').map((button) => button.textContent || button.getAttribute('aria-label')))
      .toEqual(['Resume', 'More actions']);
    expect(await opened()).toEqual(['Close ask', 'Abandon…', 'Delete…', 'Copy ask ID']);
  });

  it('draws no loud act where nothing is paused: only its ⋯', async () => {
    const head = page(undefined, door());
    expect(within(head).getAllByRole('button').map((button) => button.getAttribute('aria-label'))).toEqual(['More actions']);
    expect(await opened()).toEqual(['Pause…', 'Close ask', 'Abandon…', 'Delete…', 'Copy ask ID']);
  });

  it('moves the focus to the request as written on View full request', async () => {
    const head = page(LONG_ASK);
    await userEvent.click(within(head).getByRole('button', { name: 'View full request' }));
    const request = document.getElementById(`request-${LONG_ASK.id}`)!;
    expect(request).toHaveFocus();
    expect(request).toHaveTextContent(LONG_ASK.sentence);
  });

  it('offers View full request only where the title was long', () => {
    expect(within(page()).queryByRole('button', { name: 'View full request' })).toBeNull();
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    const head = page(LONG_ASK);
    expect(within(head).getByRole('button', { name: '查看完整需求' })).toBeInTheDocument();
    expect(head).toHaveTextContent('工作区 aurora');
    expect(screen.getByRole('button', { name: '更多操作' })).toBeInTheDocument();
  });
});
