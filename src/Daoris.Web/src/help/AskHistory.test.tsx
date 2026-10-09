import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import { code } from '../test/code';
import { AskHistory, type AskHistoryActs } from './AskHistory';
import type { HelpConversationRow } from './history';

// ASKHIST1: Ask Daoris's history, a molecule: its conversations as rows, a search, and each one's acts, in both catalogues.

const ago = (minutes: number) => new Date(Date.now() - minutes * 60_000).toISOString();

const row = (over: Partial<HelpConversationRow> & { session: string }): HelpConversationRow => ({
  title: 'what is a workspace?', name: null, opening: 'what is a workspace?', about: 'A circle of repositories that share a remote.',
  created: ago(120), last: ago(90), pinned: null, live: false, resumable: true, from: null, handed: null, found: null, ...over,
});

const ROWS = [
  row({ session: 'h1', title: 'Landing', name: 'Landing', pinned: ago(10), about: 'Repositories → engine → Setup.' }),
  row({ session: 'h2', live: true, last: ago(2) }),
  row({ session: 'h3', title: 'how do I land on a branch?', opening: 'how do I land on a branch?', resumable: false, from: 'h1' }),
];

function draw(over: Partial<Parameters<typeof AskHistory>[0]> = {}) {
  const acts: AskHistoryActs = {
    onOpen: vi.fn(), onRename: vi.fn(), onPin: vi.fn(), onStartFrom: vi.fn(), onDelete: vi.fn(),
  };
  const onSearch = vi.fn();
  render(
    <Tooltip.Provider>
      <AskHistory rows={ROWS} search="" onSearch={onSearch} {...acts} {...over} />
    </Tooltip.Provider>,
  );
  return { ...acts, onSearch };
}

/** A row's menu, opened by the keyboard as the rail's tests open theirs: Radix opens on a key in a page with no pointer. */
async function menuOf(title: string) {
  screen.getByRole('button', { name: i18n.t('help.history.menu', { title }) }).focus();
  await userEvent.keyboard('{Enter}');
}

describe.each(['en', 'zh'])('Ask Daoris’s history in %s', (language) => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('lists each conversation with its title, when, its marks and its line, and says they are kept on this machine', async () => {
    await i18n.changeLanguage(language);
    draw();

    const list = screen.getByRole('list', { name: i18n.t('help.history.title') });
    const items = within(list).getAllByRole('listitem');
    expect(items).toHaveLength(3);
    expect(within(items[0]).getByText('Landing')).toBeInTheDocument();
    expect(within(items[0]).getByText(new RegExp(i18n.t('help.history.pinned')))).toBeInTheDocument();
    expect(within(items[0]).getByText('Repositories → engine → Setup.')).toBeInTheDocument();
    expect(within(items[1]).getByText(new RegExp(i18n.t('help.history.running')))).toBeInTheDocument();
    expect(within(items[2]).getByText(new RegExp(i18n.t('help.history.startsAnew')))).toBeInTheDocument();
    expect(within(items[2]).getByText(new RegExp(i18n.t('help.history.fromEarlier')))).toBeInTheDocument();
    expect(screen.getByText(i18n.t('help.history.kept'))).toBeInTheDocument();
  });

  it('opens a conversation by its row, and hands the search its words', async () => {
    await i18n.changeLanguage(language);
    const { onOpen, onSearch } = draw();

    // The row's door comes before its ⋯, whose name in 中文 begins with the same title.
    await userEvent.click(screen.getAllByRole('button', { name: /^how do I land on a branch\?/ })[0]);
    expect(onOpen).toHaveBeenCalledWith('h3');

    await userEvent.type(screen.getByRole('searchbox', { name: i18n.t('help.history.search.label') }), 'r');
    expect(onSearch).toHaveBeenCalledWith('r');
  });

  it('says what a search found, and that one found nothing, in the reader’s words', async () => {
    await i18n.changeLanguage(language);
    draw({ rows: [row({ session: 'h1', found: '…circle of repositories that share a remote.' })], search: 'remote' });
    expect(screen.getByText('…circle of repositories that share a remote.')).toBeInTheDocument();

    cleanup();
    draw({ rows: [], search: 'nothing like this' });
    expect(screen.getByText(i18n.t('help.history.none', { words: 'nothing like this' }))).toBeInTheDocument();
  });

  it('says the list is empty before any conversation, and that older ones were left out where they were', async () => {
    await i18n.changeLanguage(language);
    draw({ rows: [] });
    expect(screen.getByText(i18n.t('help.history.empty'))).toBeInTheDocument();

    cleanup();
    draw({ cut: true });
    expect(screen.getByText(i18n.t('help.history.cut', { count: 3 }))).toBeInTheDocument();
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

  /** A rename is asked where its row is, one line of a title's length, with the terminal's twin beside it (D50). */
  it('renames a conversation in place, saying the terminal’s twin, and closes once saved', async () => {
    await i18n.changeLanguage(language);
    const onRename = vi.fn((_id: string, _name: string | null, answered: { done: () => void }) => answered.done());
    draw({ onRename });

    await menuOf('how do I land on a branch?');
    await userEvent.click(await screen.findByRole('menuitem', { name: i18n.t('help.history.rename') }));
    const form = screen.getByRole('form', { name: i18n.t('help.history.renameTitle', { title: 'how do I land on a branch?' }) });
    const field = within(form).getByRole('textbox', { name: i18n.t('help.history.name') });
    expect(field).toHaveAttribute('maxlength', '80');
    await userEvent.type(field, 'Branches');
    expect(within(form).getByText(code('daoris-driver help rename h3 "Branches"'))).toBeInTheDocument();
    await userEvent.click(within(form).getByRole('button', { name: i18n.t('help.history.save') }));

    expect(onRename).toHaveBeenCalledWith('h3', 'Branches', expect.anything());
    await waitFor(() => expect(screen.queryByRole('form')).toBeNull());
  });

  it('says a refused rename where it was asked, and keeps the field', async () => {
    await i18n.changeLanguage(language);
    draw({ onRename: (_id, _name, answered) => answered.refused('no conversation here is `h3` any more.') });

    await menuOf('how do I land on a branch?');
    await userEvent.click(await screen.findByRole('menuitem', { name: i18n.t('help.history.rename') }));
    await userEvent.type(screen.getByRole('textbox', { name: i18n.t('help.history.name') }), 'Branches');
    await userEvent.click(screen.getByRole('button', { name: i18n.t('help.history.save') }));

    expect(await screen.findByRole('alert')).toHaveTextContent('no conversation here is h3 any more.');
    expect(screen.getByRole('textbox', { name: i18n.t('help.history.name') })).toHaveValue('Branches');
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
