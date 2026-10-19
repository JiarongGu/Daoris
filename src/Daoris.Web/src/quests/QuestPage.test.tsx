import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import { HELD, OPEN, REFILED, REFILED_NAMED, REFILED_UNNAMED, STARTING, WORKING } from './fixtures';
import { bodyUnderHead, QuestPage } from './QuestPage';

// A quest's head, titles first (UX7c, D152 §7; the UX7 design §5.3), as a molecule: its state leads, its name follows on
// two lines at most, one facts line, its id in its ⋯ and its Details, and a body that does not say the title again.

const NOTE = '(Re-filed from ask #39c495, whose quest was taken outside the driver with no session.)';
const nothing = () => {};

const page = (props: Partial<Parameters<typeof QuestPage>[0]> = {}) => render(
  <QuestPage quest={OPEN} onRespond={nothing} onDismiss={nothing} onOpenQuest={nothing} {...props} />,
);

describe('a quest’s head', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('leads with its state, then its name on two lines at most, then its facts, and no id or state line', () => {
    page({ quest: REFILED_UNNAMED, session: STARTING });
    const header = screen.getByRole('heading', { level: 1 }).closest('header')!;

    const title = within(header).getByRole('heading', { level: 1, name: NOTE });
    expect(title).toHaveClass('line-clamp-2');
    const state = within(header).getByText('open');
    expect(state.compareDocumentPosition(title) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(state).toHaveAttribute('title', 'published — nobody has taken it');
    expect(within(header).getByText('to engine · filed 4h ago · a session is starting here')).toBeInTheDocument();
    expect(within(header).queryByText(/· from ask/)).toBeNull();
    expect(within(header).queryByText('published — nobody has taken it')).toBeNull();
    expect(within(header).queryByText('#e67690366b56')).toBeNull();
  });

  /** The install's quest page said the title twice: in its head, and as its body's first line. */
  it('drops the body’s first line where it is the title the head said', () => {
    const { container } = page({ quest: REFILED_UNNAMED });
    const body = [...container.querySelectorAll('p.whitespace-pre-wrap')].map((p) => p.textContent);
    expect(body).toHaveLength(1);
    expect(body[0]).toMatch(/^continue the prod half of the ticket/);
    expect(screen.getAllByText(NOTE)).toHaveLength(1);
  });

  /** With a short title the head says the name, and the body opens on the whole title, said once. */
  it('says its short title in the head, and the whole title once, at the body’s head', () => {
    const { container } = page({ quest: REFILED_NAMED });
    expect(screen.getByRole('heading', { level: 1, name: 'TK-2203 continue the production half' })).toBeInTheDocument();
    const body = container.querySelector('p.whitespace-pre-wrap')!.textContent!;
    expect(body.startsWith(NOTE)).toBe(true);
    expect(screen.queryAllByText(NOTE, { exact: false })).toHaveLength(1);
  });

  it('keeps its id, whom it asks, its lanes and its full times in a folded Details, the id with Copy', () => {
    page({ quest: REFILED, lanes: 'core (Core)' });
    const fold = screen.getByRole('button', { name: /^Details/ });
    expect(fold).toHaveAttribute('aria-expanded', 'false');
    expect(fold).toHaveTextContent('#e67690366b56');

    fireEvent.click(fold);
    const details = screen.getByRole('region', { name: 'Details' });
    for (const label of ['ID', 'From', 'To', 'Lanes', 'Filed', 'Short title']) expect(within(details).getByText(label)).toBeInTheDocument();
    expect(within(details).getByText('continue the prod half of the ticket…')).toBeInTheDocument();
    expect(within(details).getByRole('button', { name: 'Copy' })).toBeInTheDocument();
  });

  it('draws its next step loud, Decline… beside it, and the rest in its ⋯', () => {
    page({ quest: { ...OPEN, deletable: true }, onDelete: nothing });
    const header = screen.getByRole('heading', { level: 1 }).closest('header')!;
    expect(within(header).getAllByRole('button').map((button) => button.textContent || button.getAttribute('aria-label')))
      .toEqual(['Take', 'Decline…', 'More actions']);
  });

  /** HEAD2b: the primary holds still in `acts`, Decline… in the fold (shown above 640 px), the ⋯ last. */
  it('puts the primary in the acts, Decline… in the fold and the ⋯ last', () => {
    page();
    const take = screen.getByRole('button', { name: 'Take' });
    const decline = screen.getByRole('button', { name: 'Decline…' });
    expect(decline.parentElement).toHaveClass('hidden');
    const more = screen.getByRole('button', { name: 'More actions' });
    expect(take.compareDocumentPosition(decline) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(decline.compareDocumentPosition(more) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  /** The ⋯ is a portal, outside the main area's container query, so the page reads the width itself. */
  it('says Decline… again in the ⋯ at the narrow width', async () => {
    const width = vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockReturnValue(600);
    try {
      page();
      await userEvent.click(screen.getByRole('button', { name: 'More actions' }));
      expect(await screen.findByRole('menuitem', { name: 'Decline…' })).toBeInTheDocument();
      // Closed, so the open menu's body lock does not reach the next test.
      await userEvent.keyboard('{Escape}');
    } finally {
      width.mockRestore();
    }
  });

  it('leaves Decline… to the fold above 640 px', async () => {
    const width = vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockReturnValue(900);
    try {
      page();
      screen.getByRole('button', { name: 'More actions' }).focus();
      await userEvent.keyboard('{Enter}');
      await screen.findByRole('menuitem', { name: 'Copy quest ID' });
      expect(screen.queryByRole('menuitem', { name: 'Decline…' })).toBeNull();
    } finally {
      width.mockRestore();
    }
  });

  it('moves the focus to the request as written on View full request, where the title is long', async () => {
    page({ quest: REFILED_UNNAMED });
    await userEvent.click(screen.getByRole('button', { name: 'View full request' }));
    const request = document.getElementById('quest-request')!;
    expect(request).toHaveAttribute('tabindex', '-1');
    expect(request).toHaveFocus();
    expect(request.textContent).toMatch(/^continue the prod half of the ticket/);
  });

  it('offers one way to the whole request: View full request, and no Show all beside it (HEAD2f)', () => {
    const tall = vi.spyOn(HTMLElement.prototype, 'scrollHeight', 'get').mockReturnValue(120);
    const fits = vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockReturnValue(60);
    try {
      page({ quest: REFILED_UNNAMED });
      expect(screen.getByRole('button', { name: 'View full request' })).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Show all' })).toBeNull();
    } finally {
      tall.mockRestore();
      fits.mockRestore();
    }
  });

  it('offers no View full request for a short title', () => {
    page({ quest: { ...OPEN, title: 'Fix it', body: 'Fix it\n\nBecause.' } });
    expect(screen.queryByRole('button', { name: 'View full request' })).toBeNull();
  });

  it('puts Held and Unreviewed on the facts line, not beside the state', () => {
    const { unmount } = page({ quest: HELD });
    const header = screen.getByRole('heading', { level: 1 }).closest('header')!;
    expect(within(header).getByText(/^awaits your yes · to /)).toBeInTheDocument();
    unmount();
    page({ quest: { ...OPEN, held: true, hold: 'unreviewed' } });
    expect(within(screen.getByRole('heading', { level: 1 }).closest('header')!).getByText(/^awaits review · to /)).toBeInTheDocument();
  });

  it('keeps who asked in Details, with the rest', () => {
    page({ quest: REFILED });
    fireEvent.click(screen.getByRole('button', { name: /^Details/ }));
    const details = screen.getByRole('region', { name: 'Details' });
    expect(within(details).getByText('ask #f6d947')).toBeInTheDocument();
  });

  it('says View full request in 中文', async () => {
    await i18n.changeLanguage('zh');
    page({ quest: REFILED_UNNAMED });
    expect(screen.getByRole('button', { name: '查看完整需求' })).toBeInTheDocument();
  });

  it('keeps a ⋯ with its id on a quest that has closed, and nothing else', () => {
    page({ quest: { ...OPEN, status: 'Done' } });
    const header = screen.getByRole('heading', { level: 1 }).closest('header')!;
    expect(within(header).getAllByRole('button').map((button) => button.getAttribute('aria-label'))).toEqual(['More actions']);
  });

  it('says its facts in 中文', async () => {
    await i18n.changeLanguage('zh');
    page({ quest: REFILED_UNNAMED, session: STARTING });
    expect(screen.getByText('致 engine · 4 小时前发起 · 一个会话正在这里启动')).toBeInTheDocument();
  });
});

/**
 * WAITSHOWN1: its driven session's record, read as the session list's reader reads it (D126 §2.4). A session whose quest waits
 * on another repository's answer ended as completed, and read so beside a quest that has not finished; a working session
 * carries its last end in its note, which the page leaves out while it works.
 */
describe('a quest’s session', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });
  const ended = { ...WORKING, state: 'completed' as const, note: 'It finished.' };

  it('reads awaiting reply, and what it waits on, where the reader derived it', () => {
    page({
      quest: { ...OPEN, status: 'Taken', awaits: 'b2c3d4' }, session: ended,
      grouping: { session: ended.id, group: 'later', shown: 'awaiting-reply', archived: false, teammate: false, awaits: 'b2c3d4', awaitsOf: 'billing' },
    });
    const section = screen.getByRole('region', { name: 'Session' });
    expect(within(section).getByText('awaiting reply')).toBeInTheDocument();
    expect(within(section).queryByText('completed')).toBeNull();
    expect(within(section).getByText('waits on #b2c3d4, asked of billing')).toBeInTheDocument();
  });

  it('reads no awaiting reply on a quest that closed, whose question the service cleared (AWAITSTALE1)', () => {
    page({ quest: { ...OPEN, status: 'Done', awaits: null }, session: ended });
    const section = screen.getByRole('region', { name: 'Session' });
    expect(within(section).queryByText('awaiting reply')).toBeNull();
    expect(within(section).queryByText(/waits on/)).toBeNull();
    expect(within(section).getByText('completed')).toBeInTheDocument();
  });

  it('keeps the record’s state where the reader says nothing of it', () => {
    page({ quest: { ...OPEN, status: 'Taken' }, session: ended });
    expect(within(screen.getByRole('region', { name: 'Session' })).getByText('completed')).toBeInTheDocument();
  });

  it('leaves out what a working session’s past ending said, and keeps what it says now', () => {
    const parts = [
      { code: 'ended.went-on', values: {}, text: 'It went on with your words and ended; its quest stays as it closed.' },
      { code: 'working.goes-on', values: {}, text: 'It goes on with your words in its own conversation.' },
    ];
    page({ quest: { ...OPEN, status: 'Taken' }, session: { ...WORKING, note: parts[0]!.text, noteParts: parts } });
    expect(screen.queryByText(/ended; its quest stays as it closed/)).toBeNull();
    expect(screen.getByText(/goes on with your words in its own/)).toBeInTheDocument();
  });
});

describe('a quest’s body under its head', () => {
  it('leaves out a first line that is the title, and the blank line after it', () => {
    expect(bodyUnderHead('Fix it', 'Fix it\n\nBecause.', false)).toBe('Because.');
    expect(bodyUnderHead('Fix it', 'Because.', false)).toBe('Because.');
    expect(bodyUnderHead('Fix it', 'Fix it', false)).toBe('');
  });

  it('opens on the whole title where the head said a short one', () => {
    expect(bodyUnderHead('Fix it', 'Fix it\n\nBecause.', true)).toBe('Fix it\n\nBecause.');
    expect(bodyUnderHead('Fix it', 'Because.', true)).toBe('Fix it\n\nBecause.');
  });
});

describe('a quest’s files as the shell pushes them (PUSH1d3)', () => {
  /** The push carries no path, only whether this machine keeps the bytes: a kept one opens, one kept elsewhere says so. */
  it('opens a file pushed as kept here, and says one pushed as not kept is elsewhere', () => {
    const [kept, elsewhere] = OPEN.attachments!;
    page({ quest: { ...OPEN, attachments: [{ ...kept!, kept: true }, { ...elsewhere!, kept: false }] } });

    expect(screen.getByRole('link', { name: /before\.png/ })).toHaveAttribute(
      'href', expect.stringContaining(`/api/quests/abc123/attachments/ab12cd34ef56${'0'.repeat(52)}`));
    expect(screen.queryByRole('link', { name: /trace\.log/ })).toBeNull();
    expect(screen.getByText(/kept on the machine that published it/)).toBeInTheDocument();
  });
});
