import { useState } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import { code } from '../test/code';
import { AskHistory, type AskHistoryActs } from './AskHistory';
import type { HelpConversationRow } from './history';
import { ROW_LINE } from './preview';

// ASKHIST1, as ASKHIST1c made it: Ask Daoris's history, a molecule. Its head (what it is, kept on this machine, a new
// conversation, the search), its rows grouped by day, each row's acts, and its states, in both catalogues.

const minutesAgo = (minutes: number) => new Date(Date.now() - minutes * 60_000).toISOString();
const daysAgo = (days: number) => {
  const at = new Date();
  at.setDate(at.getDate() - days);
  at.setHours(12, 0, 0, 0);
  return at.toISOString();
};

const row = (over: Partial<HelpConversationRow> & { session: string }): HelpConversationRow => ({
  title: 'what is a workspace?', name: null, opening: 'what is a workspace?', about: 'A circle of repositories that share a remote.',
  created: minutesAgo(120), last: minutesAgo(1), pinned: null, live: false, resumable: true, from: null, handed: null, found: null,
  foundLine: null, ...over,
});

const ROWS = [
  row({ session: 'h1', title: 'Landing', name: 'Landing', opening: 'how do I land?', pinned: minutesAgo(10), about: 'Repositories → engine → **Setup**.' }),
  row({ session: 'h2', live: true }),
  row({ session: 'h3', title: 'how do I land on a branch?', opening: 'how do I land on a branch?', resumable: false, from: 'h1', last: daysAgo(30) }),
];

function draw(over: Partial<Parameters<typeof AskHistory>[0]> = {}) {
  const acts: AskHistoryActs = {
    onOpen: vi.fn(), onRename: vi.fn(), onPin: vi.fn(), onStartFrom: vi.fn(), onDelete: vi.fn(),
  };
  const onSearch = vi.fn();
  const onNew = vi.fn();
  const onClose = vi.fn();
  const onRetry = vi.fn();
  render(
    <Tooltip.Provider>
      <AskHistory rows={ROWS} search="" onSearch={onSearch} onNew={onNew} onClose={onClose} onRetry={onRetry} {...acts} {...over} />
    </Tooltip.Provider>,
  );
  return { ...acts, onSearch, onNew, onClose, onRetry };
}

const history = () => screen.getByRole('region', { name: i18n.t('help.history.title') });
const searchBox = () => screen.getByRole('searchbox', { name: i18n.t('help.history.search.label') });
/** A row's door: the button that leads with its title, before its ⋯ (whose name in 中文 begins with the same title). */
const door = (title: string) => screen.getAllByRole('button', { name: (name) => name.startsWith(title) })[0]!;
const more = (title: string) => screen.getByRole('button', { name: i18n.t('help.history.menu', { title }) });

/** A row's menu, opened by the keyboard as the rail's tests open theirs: Radix opens on a key in a page with no pointer. */
async function menuOf(title: string) {
  more(title).focus();
  await userEvent.keyboard('{Enter}');
}

describe.each(['en', 'zh'])('Ask Daoris’s history in %s', (language) => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('heads the list with what it is, that it is kept here, a new conversation and the search, left-led', async () => {
    await i18n.changeLanguage(language);
    const { onNew } = draw();

    const head = within(history()).getByRole('heading', { name: i18n.t('help.history.title') });
    expect(head.nextElementSibling).toHaveTextContent(i18n.t('help.history.kept'));
    expect(head.nextElementSibling).toHaveClass('text-small', 'text-ink-soft');
    await userEvent.click(within(history()).getByRole('button', { name: i18n.t('help.new') }));
    expect(onNew).toHaveBeenCalledOnce();
    // The search under the head, the whole width; only the rows scroll, and the head stays.
    expect(searchBox().closest('header')).not.toBeNull();
    expect(screen.getAllByRole('list')[0]!.closest('.overflow-y-auto')).not.toBeNull();
    expect(searchBox().closest('.overflow-y-auto')).toBeNull();
  });

  it('groups the rows by day, the pinned first, and says which is open and which are pinned', async () => {
    await i18n.changeLanguage(language);
    draw({ shown: 'h2' });

    const pinned = screen.getByRole('list', { name: i18n.t('help.history.group.pinned') });
    const today = screen.getByRole('list', { name: i18n.t('help.history.group.today') });
    const older = screen.getByRole('list', { name: i18n.t('help.history.group.older') });
    expect(within(pinned).getByText('Landing')).toBeInTheDocument();
    expect(within(today).getByText('what is a workspace?')).toBeInTheDocument();
    expect(within(older).getByText('how do I land on a branch?')).toBeInTheDocument();
    expect(screen.queryByRole('list', { name: i18n.t('help.history.group.yesterday') })).toBeNull();

    // The open one says so in words; the accent is its selection, never the only sign.
    const open = door('what is a workspace?');
    expect(open).toHaveAttribute('aria-current', 'true');
    expect(within(open).getByText(i18n.t('help.history.current'))).toBeInTheDocument();
    expect(within(door('Landing')).queryByText(i18n.t('help.history.current'))).toBeNull();
    // A pin is a mark in neutral ink, named in words.
    expect(within(door('Landing')).getByText(i18n.t('help.history.pinned'))).toHaveClass('sr-only');
    expect(within(door('Landing')).getByText(i18n.t('help.history.pinned')).parentElement).toHaveClass('text-ink-soft');
    // Its marks: running, and one started from an earlier one. Opening a row only reads it, so no row says it starts anew.
    expect(within(today).getByText(new RegExp(i18n.t('help.history.running')))).toBeInTheDocument();
    expect(within(older).getByText(new RegExp(i18n.t('help.history.fromEarlier')))).toBeInTheDocument();
    expect(screen.queryByText(/starts anew|将开始新对话/)).toBeNull();
  });

  it('shows what a conversation was about as plain words, its Markdown’s marks gone', async () => {
    await i18n.changeLanguage(language);
    draw();
    expect(within(door('Landing')).getByText('Repositories → engine → Setup.')).toBeInTheDocument();
    expect(screen.queryByText(/\*\*/)).toBeNull();
    // Nothing inside a row's door is a link or a button of its own.
    for (const each of screen.getAllByRole('listitem')) {
      const own = each.querySelector('button[data-session]')!;
      expect(own.querySelector('a, button, code')).toBeNull();
    }
  });

  it('opens a conversation by its row, telling where the list was scrolled, and hands the search its words', async () => {
    await i18n.changeLanguage(language);
    const { onOpen, onSearch } = draw();

    await userEvent.click(door('how do I land on a branch?'));
    expect(onOpen).toHaveBeenCalledWith('h3', 0);

    await userEvent.type(searchBox(), 'r');
    expect(onSearch).toHaveBeenCalledWith('r');
  });

  it('marks the words a search found, its find in place of the line, and in a title it found them in', async () => {
    await i18n.changeLanguage(language);
    draw({
      search: 'remote',
      rows: [
        row({ session: 'h1', found: '…circle of `repositories` that share a **remote**.' }),
        row({ session: 'h2', title: 'a remote for the team', opening: 'a remote for the team', found: 'a remote for the team' }),
      ],
    });

    const found = door('what is a workspace?');
    expect(within(found).getByText('remote', { selector: 'mark' })).toBeInTheDocument();
    expect(found).toHaveTextContent('…circle of repositories that share a remote.');
    expect(within(found).queryByText('A circle of repositories that share a remote.')).toBeNull();
    const titled = door('a remote for the team');
    expect(within(titled).getAllByText('remote', { selector: 'mark' })).not.toHaveLength(0);
  });

  it('says a search needs two characters, and keeps the whole list meanwhile', async () => {
    await i18n.changeLanguage(language);
    draw({ search: 'a' });
    expect(screen.getByRole('status')).toHaveTextContent(i18n.t('help.history.short'));
    expect(screen.getAllByRole('listitem')).toHaveLength(3);
  });

  // ASKHIST1d2: one Han character is a word on its own (区, 圈), and the driver searches by it (`SessionEvents.Searchable`).
  it('searches by one Han character', async () => {
    await i18n.changeLanguage(language);
    draw({ search: '树', rows: [] });
    expect(screen.queryByText(i18n.t('help.history.short'))).toBeNull();
    expect(screen.getByText(i18n.t('help.history.none', { words: '树' }))).toBeInTheDocument();
  });

  it('shows a found line parsed before it is cut, from a little before its words, marked', async () => {
    await i18n.changeLanguage(language);
    const before = 'the **feed** keeps `engine` in step; '.repeat(20);
    draw({
      search: 'remote',
      rows: [row({ session: 'h1', found: '…in step; the `engine` **remote** is …', foundLine: `${before}the \`engine\` **remote** is set once.` })],
    });

    const found = door('what is a workspace?');
    expect(within(found).getByText('remote', { selector: 'mark' })).toBeInTheDocument();
    expect(found).toHaveTextContent(/….*in step; the engine remote is set once\./);
    expect(found.textContent).not.toMatch(/[*`]/);
    expect(found.textContent!.length).toBeLessThan(ROW_LINE + 'what is a workspace?'.length + 40);
  });

  it('cuts a whole first line to what a row holds, after its Markdown is read', async () => {
    await i18n.changeLanguage(language);
    draw({ rows: [row({ session: 'h1', about: `**Landing**: ${'a branch lands on its line, '.repeat(40)}` })] });
    const line = within(door('what is a workspace?')).getByText(/^Landing: a branch lands/);
    expect(line.textContent!.length).toBeLessThanOrEqual(ROW_LINE + 1);
    expect(line.textContent).toMatch(/…$/);
  });

  it('draws a first load as skeleton rows with its words, and holds the rows dimmed while a newer answer comes', async () => {
    await i18n.changeLanguage(language);
    draw({ rows: [], loading: true });
    expect(screen.getByText(i18n.t('help.history.loading'))).toBeInTheDocument();
    expect(screen.queryByText(i18n.t('help.history.emptyHeadline'))).toBeNull();

    cleanup();
    draw({ refreshing: true, search: 'land' });
    expect(screen.getAllByRole('listitem')).toHaveLength(3);
    expect(screen.getAllByRole('list')[0]!.closest('.overflow-y-auto')).toHaveClass('opacity-60');
  });

  it('says a list it could not read, with its reason and a retry, never that there are no conversations', async () => {
    await i18n.changeLanguage(language);
    const { onRetry } = draw({ rows: [], error: 'the driver is not running.' });
    expect(screen.getByText(i18n.t('help.history.failed'))).toBeInTheDocument();
    expect(screen.getByText('the driver is not running.')).toBeInTheDocument();
    expect(screen.queryByText(i18n.t('help.history.emptyHeadline'))).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: i18n.t('help.history.retry') }));
    expect(onRetry).toHaveBeenCalledOnce();

    // A refetch that failed over rows already read keeps them, and says so above them.
    cleanup();
    draw({ error: 'the driver is not running.' });
    expect(screen.getAllByRole('listitem')).toHaveLength(3);
    expect(screen.getByRole('alert')).toHaveTextContent('the driver is not running.');
    expect(screen.getByRole('button', { name: i18n.t('help.history.retry') })).toBeInTheDocument();
  });

  it('offers a conversation from an empty list, and clears a search that found nothing, saying every one was searched', async () => {
    await i18n.changeLanguage(language);
    const { onNew } = draw({ rows: [] });
    expect(screen.getByText(i18n.t('help.history.emptyHeadline'))).toBeInTheDocument();
    expect(screen.getByText(i18n.t('help.history.empty'))).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: i18n.t('help.history.start') }));
    expect(onNew).toHaveBeenCalledOnce();

    cleanup();
    const { onSearch } = draw({ rows: [], search: 'nothing like this' });
    expect(screen.getByText(i18n.t('help.history.none', { words: 'nothing like this' }))).toBeInTheDocument();
    expect(screen.getByText(i18n.t('help.history.noneHint'))).toBeInTheDocument();
    // The driver searches every conversation (ASKHIST1d1): none is said to be left unsearched.
    expect(screen.queryByText(/not searched|未被搜索/)).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: i18n.t('help.history.clearSearch') }));
    expect(onSearch).toHaveBeenCalledWith('');
  });

  it('says how many a search found and that it searched every conversation, never over a newer search’s wait', async () => {
    await i18n.changeLanguage(language);
    draw({ search: 'land', total: 3 });
    expect(screen.getByRole('status')).toHaveTextContent(i18n.t('help.history.searched', { count: 3, words: 'land' }));

    cleanup();
    draw({ search: 'land', total: 1, rows: [ROWS[0]!] });
    expect(screen.getByRole('status')).toHaveTextContent(i18n.t('help.history.searched', { count: 1, words: 'land' }));

    // The rows shown are the last search's while the newer one comes, so the count would be theirs.
    cleanup();
    draw({ search: 'landing', total: 3, refreshing: true });
    expect(screen.queryByRole('status')).toBeNull();
    // A list not searched says nothing of a search.
    cleanup();
    draw({ total: 3 });
    expect(screen.queryByRole('status')).toBeNull();
  });

  it('lists more on a press, saying how many of how many are listed, and takes the reader to the first row it brought', async () => {
    await i18n.changeLanguage(language);
    const onMore = vi.fn();
    const acts: AskHistoryActs = { onOpen: vi.fn(), onRename: vi.fn(), onPin: vi.fn(), onStartFrom: vi.fn(), onDelete: vi.fn() };
    const page = (rows: HelpConversationRow[], more: boolean) => (
      <Tooltip.Provider>
        <AskHistory rows={rows} total={5} search="" onSearch={vi.fn()} {...(more ? { onMore } : {})} {...acts} />
      </Tooltip.Provider>
    );
    const { rerender } = render(page(ROWS, true));

    expect(screen.getByText(i18n.t('help.history.listed', { shown: 3, total: 5 }))).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: i18n.t('help.history.more') }));
    expect(onMore).toHaveBeenCalledOnce();

    const older = [row({ session: 'h4', title: 'an older one', last: daysAgo(60) }), row({ session: 'h5', title: 'the oldest', last: daysAgo(90) })];
    rerender(page([...ROWS, ...older], false));
    await waitFor(() => expect(door('an older one')).toHaveFocus());
    // The last page: nothing more to list, and nothing said of it.
    expect(screen.queryByRole('button', { name: i18n.t('help.history.more') })).toBeNull();
    expect(screen.queryByText(i18n.t('help.history.listed', { shown: 5, total: 5 }))).toBeNull();
  });

  it('says it is reading more, and why the next page could not be read, its press asking again', async () => {
    await i18n.changeLanguage(language);
    const onMore = vi.fn();
    draw({ total: 450, onMore, loadingMore: true });
    expect(screen.getByRole('status')).toHaveTextContent(i18n.t('help.history.loadingMore'));
    // Pressed again while it reads, it asks nothing more: the focus stays on it, so it is never taken away.
    await userEvent.click(screen.getByRole('button', { name: i18n.t('help.history.more') }));
    expect(onMore).not.toHaveBeenCalled();

    cleanup();
    draw({ total: 450, onMore, moreError: 'the driver stopped answering.' });
    expect(screen.getByRole('alert')).toHaveTextContent('the driver stopped answering.');
    expect(screen.getAllByRole('listitem')).toHaveLength(3);
    await userEvent.click(screen.getByRole('button', { name: i18n.t('help.history.more') }));
    expect(onMore).toHaveBeenCalledOnce();
  });

  it('pins, unpins and starts a new conversation from a row’s menu', async () => {
    await i18n.changeLanguage(language);
    const { onPin, onStartFrom } = draw();

    await menuOf('Landing');
    await userEvent.click(await screen.findByRole('menuitem', { name: i18n.t('help.history.unpin') }));
    expect(onPin).toHaveBeenCalledWith('h1', false);

    await menuOf('how do I land on a branch?');
    await userEvent.click(await screen.findByRole('menuitem', { name: i18n.t('help.history.pin') }));
    expect(onPin).toHaveBeenCalledWith('h3', true);

    await menuOf('how do I land on a branch?');
    await userEvent.click(await screen.findByRole('menuitem', { name: i18n.t('help.history.startFrom') }));
    expect(onStartFrom).toHaveBeenCalledWith('h3');
  });

  /** UX §6: a 28 px target, in quiet ink at rest, beside the row's words rather than over them. */
  it('draws each row’s ⋯ as a 28 px control beside its door, there at rest', async () => {
    await i18n.changeLanguage(language);
    draw();
    const trigger = more('Landing');
    expect(trigger).toHaveClass('h-7', 'w-7', 'shrink-0', 'text-ink-faint');
    expect(trigger.className).not.toMatch(/\babsolute\b|opacity-0/);
    expect(trigger.closest('li')).toHaveClass('grid-cols-[minmax(0,1fr)_auto]');
  });
});

/** ASKHIST1c: the list's keys, and where the focus goes as the person comes back to it. */
describe.each(['en', 'zh'])('the history’s keys in %s', (language) => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('moves from the search into the rows and along them across groups, and to either end', async () => {
    await i18n.changeLanguage(language);
    draw();

    searchBox().focus();
    await userEvent.keyboard('{ArrowDown}');
    expect(door('Landing')).toHaveFocus();
    await userEvent.keyboard('{ArrowDown}');
    expect(door('what is a workspace?')).toHaveFocus();
    await userEvent.keyboard('{End}');
    expect(door('how do I land on a branch?')).toHaveFocus();
    await userEvent.keyboard('{Home}');
    expect(door('Landing')).toHaveFocus();
    await userEvent.keyboard('{ArrowUp}');
    expect(door('Landing')).toHaveFocus();
  });

  it('goes back to the conversation on Escape, after a search with words is cleared, and never from a menu or a form', async () => {
    await i18n.changeLanguage(language);
    const { onClose, onSearch } = draw({ search: 'land' });

    searchBox().focus();
    await userEvent.keyboard('{Escape}');
    expect(onSearch).toHaveBeenCalledWith('');
    expect(onClose).not.toHaveBeenCalled();

    cleanup();
    const second = draw();
    door('Landing').focus();
    await userEvent.keyboard('{Escape}');
    expect(second.onClose).toHaveBeenCalledOnce();

    // A key composing a word belongs to the input method, the last press of a composition (229) among them (IME1).
    fireEvent.keyDown(searchBox(), { key: 'Escape', isComposing: true });
    fireEvent.keyDown(door('Landing'), { key: 'Escape', keyCode: 229 });
    expect(second.onClose).toHaveBeenCalledOnce();

    await menuOf('Landing');
    await userEvent.keyboard('{Escape}');
    expect(second.onClose).toHaveBeenCalledOnce();
  });

  it('puts the focus back on the row it was left from and the list where it was scrolled, else on the search', async () => {
    await i18n.changeLanguage(language);
    draw({ restore: { row: 'h3', scroll: 240 } });
    expect(door('how do I land on a branch?')).toHaveFocus();
    expect(screen.getAllByRole('list')[0]!.closest('.overflow-y-auto')!.scrollTop).toBe(240);

    cleanup();
    draw({ restore: { row: 'gone', scroll: 0 } });
    expect(searchBox()).toHaveFocus();
  });

  it('keeps the arrows inside a rename’s field, and its Escape for itself', async () => {
    await i18n.changeLanguage(language);
    const { onClose } = draw();

    await menuOf('Landing');
    await userEvent.click(await screen.findByRole('menuitem', { name: i18n.t('help.history.rename') }));
    const field = screen.getByRole('textbox', { name: i18n.t('help.history.name') });
    await waitFor(() => expect(field).toHaveFocus());
    await userEvent.keyboard('{ArrowDown}{End}{Home}{ArrowUp}');
    expect(field).toHaveFocus();
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByRole('form')).toBeNull();
    expect(onClose).not.toHaveBeenCalled();
    expect(more('Landing')).toHaveFocus();
  });
});

/** ASKHIST1c: a rename starts from the title shown, says its wait, and gives the focus back to the row's ⋯. */
describe.each(['en', 'zh'])('a conversation’s rename in %s', (language) => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  async function renameOf(title: string) {
    await menuOf(title);
    await userEvent.click(await screen.findByRole('menuitem', { name: i18n.t('help.history.rename') }));
    return screen.getByRole('form', { name: i18n.t('help.history.renameTitle', { title }) });
  }

  it('starts from the title shown, selected, the field the whole width above its presses, with the terminal’s twin', async () => {
    await i18n.changeLanguage(language);
    const onRename = vi.fn((_id: string, _name: string | null, answered: { done: () => void }) => answered.done());
    draw({ onRename });

    const form = await renameOf('how do I land on a branch?');
    const field = within(form).getByRole('textbox', { name: i18n.t('help.history.name') }) as HTMLInputElement;
    expect(field).toHaveValue('how do I land on a branch?');
    await waitFor(() => expect([field.selectionStart, field.selectionEnd]).toEqual([0, field.value.length]));
    expect(field).toHaveAttribute('maxlength', '80');
    expect(field).toHaveClass('w-full');
    expect(within(form).getByRole('button', { name: i18n.t('help.history.save') })).toBeDisabled();
    // A conversation with no name of its own is already called by its question: nothing to go back to.
    expect(within(form).queryByRole('button', { name: i18n.t('help.history.useOriginal') })).toBeNull();

    await userEvent.clear(field);
    expect(within(form).getByRole('button', { name: i18n.t('help.history.save') })).toBeDisabled();
    await userEvent.type(field, 'Branches');
    expect(within(form).getByText(code('daoris-driver help rename h3 Branches'))).toBeInTheDocument();
    await userEvent.click(within(form).getByRole('button', { name: i18n.t('help.history.save') }));

    expect(onRename).toHaveBeenCalledWith('h3', 'Branches', expect.anything());
    await waitFor(() => expect(screen.queryByRole('form')).toBeNull());
    expect(more('how do I land on a branch?')).toHaveFocus();
  });

  it.each([
    ['a "name"', '<name>'], ['R&D', '<name>'], ['two words', '"two words"'],
  ])('spells the terminal twin for %s safely without changing the saved name', async (name, word) => {
    await i18n.changeLanguage(language);
    const onRename = vi.fn((_id: string, _name: string | null, answered: { done: () => void }) => answered.done());
    draw({ onRename });
    const form = await renameOf('how do I land on a branch?');
    const field = within(form).getByRole('textbox', { name: i18n.t('help.history.name') });
    await userEvent.clear(field);
    await userEvent.type(field, name);
    expect(within(form).getByText(code(`daoris-driver help rename h3 ${word}`))).toBeInTheDocument();
    await userEvent.click(within(form).getByRole('button', { name: i18n.t('help.history.save') }));
    expect(onRename).toHaveBeenCalledWith('h3', name, expect.anything());
  });

  it('gives a named conversation its original question back by a labelled press', async () => {
    await i18n.changeLanguage(language);
    const onRename = vi.fn((_id: string, _name: string | null, answered: { done: () => void }) => answered.done());
    draw({ onRename });

    const form = await renameOf('Landing');
    expect(within(form).getByRole('textbox', { name: i18n.t('help.history.name') })).toHaveValue('Landing');
    await userEvent.click(within(form).getByRole('button', { name: i18n.t('help.history.useOriginal') }));
    expect(onRename).toHaveBeenCalledWith('h1', null, expect.anything());
  });

  it('says its wait, takes no Escape while it waits, and keeps the field when refused', async () => {
    await i18n.changeLanguage(language);
    let answer: { done: () => void; refused: (sentence: string) => void } | null = null;
    draw({ onRename: (_id, _name, answered) => { answer = answered; } });

    const form = await renameOf('how do I land on a branch?');
    const field = within(form).getByRole('textbox', { name: i18n.t('help.history.name') });
    await userEvent.clear(field);
    await userEvent.type(field, 'Branches');
    await userEvent.click(within(form).getByRole('button', { name: i18n.t('help.history.save') }));

    expect(within(form).getByRole('status')).toHaveTextContent(i18n.t('help.history.saving'));
    fireEvent.keyDown(field, { key: 'Escape' });
    expect(screen.getByRole('form')).toBeInTheDocument();

    act(() => answer!.refused('no conversation here is `h3` any more.'));
    expect(await within(form).findByRole('alert')).toHaveTextContent('no conversation here is h3 any more.');
    expect(field).toHaveValue('Branches');
    expect(within(form).getByRole('status')).toHaveTextContent('');
  });

  /** A delete asks once (UXFIX2): its first press says what the second will do; a live conversation is offered none. */
  it('asks once before a delete, and offers none for a conversation still running', async () => {
    await i18n.changeLanguage(language);
    const onDelete = vi.fn((_id: string, answered: { done: () => void }) => answered.done());
    draw({ onDelete });

    await menuOf('what is a workspace?');
    expect(screen.queryByRole('menuitem', { name: i18n.t('help.history.delete') })).toBeNull();
    await userEvent.keyboard('{Escape}');

    await menuOf('Landing');
    await userEvent.click(await screen.findByRole('menuitem', { name: i18n.t('help.history.delete') }));
    const ask = await screen.findByRole('group', { name: i18n.t('help.history.deleteLabel', { title: 'Landing' }) });
    expect(within(ask).getByText(i18n.t('help.history.deleteSays', { title: 'Landing' }))).toBeInTheDocument();
    expect(onDelete).not.toHaveBeenCalled();
    await userEvent.click(within(ask).getByRole('button', { name: i18n.t('help.history.deleteMeanIt') }));

    expect(onDelete).toHaveBeenCalledWith('h1', expect.anything());
    await waitFor(() => expect(screen.queryByRole('group', { name: i18n.t('help.history.deleteLabel', { title: 'Landing' }) })).toBeNull());
  });
});

/**
 * ASKHIST1c: a delete that takes its row gives the focus to what held it, never the page's body: the rows left, or the
 * history once the last one went. The row is really removed, as the organism removes it once the driver answers.
 */
describe('the focus once a delete took its row', () => {
  afterEach(() => cleanup());

  function Kept({ start }: { start: HelpConversationRow[] }) {
    const [rows, setRows] = useState(start);
    return (
      <Tooltip.Provider>
        <AskHistory
          rows={rows} search="" onSearch={() => {}} onOpen={() => {}} onRename={() => {}} onPin={() => {}} onStartFrom={() => {}}
          onDelete={(id, answered) => { setRows((was) => was.filter((each) => each.session !== id)); answered.done(); }}
        />
      </Tooltip.Provider>
    );
  }

  async function deleteRow(title: string) {
    await menuOf(title);
    await userEvent.click(await screen.findByRole('menuitem', { name: i18n.t('help.history.delete') }));
    const ask = await screen.findByRole('group', { name: i18n.t('help.history.deleteLabel', { title }) });
    await userEvent.click(within(ask).getByRole('button', { name: i18n.t('help.history.deleteMeanIt') }));
    await waitFor(() => expect(screen.queryByText(title)).toBeNull());
  }

  it('goes to the rows still listed, then to the history once the last one went', async () => {
    render(<Kept start={[ROWS[0]!, ROWS[2]!]} />);

    await deleteRow('Landing');
    await waitFor(() => expect(document.activeElement).not.toBe(document.body));
    expect(history().contains(document.activeElement)).toBe(true);

    await deleteRow('how do I land on a branch?');
    await waitFor(() => expect(document.activeElement).not.toBe(document.body));
    expect(history().contains(document.activeElement) || history() === document.activeElement).toBe(true);
    expect(screen.getByText(i18n.t('help.history.emptyHeadline'))).toBeInTheDocument();
  });
});

/**
 * ASKHIST1b: the history fits the dock at any width down to its floor. A grid's implicit column is `auto`, which grows to its
 * widest child's min-content: a pasted URL ran the search and every row past the dock's edge, and the panel scrolled
 * sideways. jsdom lays nothing out, so what bounds each part is asserted by its class. ASKHIST1c wraps a title rather than
 * cutting it, so a keyboard user can tell two such URLs apart.
 */
describe('Ask Daoris’s history at the dock’s edge', () => {
  const URL = 'https://example.atlassian.net/browse/TK-2205?focusedCommentId=1234567&page=com.example.plugin.tabpanels%3Acomments';
  const TITLE = `to complete this ${URL} so the sprint closes`;
  const ABOUT = `Opened ${URL}#comment-1234567 and read the ticket's whole description.`;
  const FOUND = `…${URL}&selectedIssue=TK-2205…`;
  const LONG = [row({ session: 'u1', title: TITLE, opening: TITLE, about: ABOUT, found: FOUND, pinned: minutesAgo(5), from: 'h1' })];

  afterEach(() => cleanup());

  /** Every grid between `node` and the history: each must bound its first column, or its widest child sets the width. */
  function gridsAbove(node: HTMLElement) {
    const grids: HTMLElement[] = [];
    for (let at: HTMLElement | null = node; at; at = at === history() ? null : at.parentElement) {
      if (at.classList.contains('grid')) grids.push(at);
    }
    return grids;
  }

  it('bounds every column between a row’s words and the dock, so nothing widens the history', () => {
    draw({ rows: LONG, search: 'TK-2205' });

    for (const node of [screen.getByText(TITLE, { selector: 'span' }), searchBox()]) {
      const grids = gridsAbove(node);
      expect(grids.length).toBeGreaterThan(0);
      for (const grid of grids) expect(grid.className).toMatch(/grid-cols-\[minmax\(0,1fr\)(_auto)?\]/);
    }
  });

  it('wraps a long title whole, its row named by it, and wraps its find at the edge', () => {
    draw({ rows: LONG, search: 'TK-2205' });

    const title = screen.getByText(TITLE, { selector: 'span' });
    expect(title).toHaveClass('wrap-anywhere');
    expect(title).not.toHaveClass('truncate');
    expect(door(TITLE)).toBeInTheDocument();

    // Where the search found it: two lines at most, broken inside a word only where one will not fit.
    const line = screen.getByText((_, node) => node?.tagName === 'SPAN' && node.textContent === FOUND && node.classList.contains('line-clamp-2'));
    expect(line).toHaveClass('line-clamp-2', 'wrap-anywhere');
  });

  it('wraps a search’s words that found nothing at the edge', () => {
    draw({ rows: [], search: URL });
    expect(screen.getByText(i18n.t('help.history.none', { words: URL }))).toHaveClass('wrap-anywhere');
  });

  it('keeps a rename’s terminal twin and a delete’s sentence inside the row, however long the title', async () => {
    const long = `${URL}-and-more`;
    draw({ rows: [row({ session: 'u1', title: TITLE, opening: TITLE, name: long })] });

    await menuOf(TITLE);
    await userEvent.click(await screen.findByRole('menuitem', { name: i18n.t('help.history.rename') }));
    const form = screen.getByRole('form', { name: i18n.t('help.history.renameTitle', { title: TITLE }) });
    const field = within(form).getByRole('textbox', { name: i18n.t('help.history.name') });
    await userEvent.clear(field);
    await userEvent.type(field, long.slice(0, 80));
    // The twin's line: without `min-w-0` it is as wide as its longest word, which its code then never breaks.
    const twin = within(form).getByText(code('daoris-driver help rename u1 <name>')).parentElement;
    expect(twin).toHaveClass('min-w-0', 'wrap-anywhere');
    await userEvent.click(within(form).getByRole('button', { name: i18n.t('common.cancel') }));

    await menuOf(TITLE);
    await userEvent.click(await screen.findByRole('menuitem', { name: i18n.t('help.history.delete') }));
    const ask = await screen.findByRole('group', { name: i18n.t('help.history.deleteLabel', { title: TITLE }) });
    expect(within(ask).getByText(i18n.t('help.history.deleteSays', { title: TITLE }))).toHaveClass('wrap-anywhere');
  });
});
