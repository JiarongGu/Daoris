import { afterEach, describe, expect, it } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import i18n from '../i18n';
import { OPEN, REFILED, REFILED_NAMED, REFILED_UNNAMED, STARTING } from './fixtures';
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
    expect(within(header).getByText('to engine · from ask #f6d947 · filed 4h ago · a session is starting here')).toBeInTheDocument();
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

  it('keeps a ⋯ with its id on a quest that has closed, and nothing else', () => {
    page({ quest: { ...OPEN, status: 'Done' } });
    const header = screen.getByRole('heading', { level: 1 }).closest('header')!;
    expect(within(header).getAllByRole('button').map((button) => button.getAttribute('aria-label'))).toEqual(['More actions']);
  });

  it('says its facts in 中文', async () => {
    await i18n.changeLanguage('zh');
    page({ quest: REFILED_UNNAMED, session: STARTING });
    expect(screen.getByText('致 engine · 来自 ask #f6d947 · 4 小时前发起 · 一个会话正在这里启动')).toBeInTheDocument();
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
