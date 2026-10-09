import { createRef } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import { code } from '../test/code';
import { type AskConversationSlot, AskPanel as Panel } from './AskPanel';
import { starters } from './starters';

// HELP1d (D89): Ask Daoris's panel — what the machine lacks, each with its door and its command.

/** The panel as the application holds it, under the one tooltip provider the page mounts. */
const AskPanel = (props: Parameters<typeof Panel>[0]) => <Tooltip.Provider><Panel {...props} /></Tooltip.Provider>;

const LACKING = starters({
  repositories: ['engine'], drivable: [], tools: [], waiting: 1, unnamedLines: [], helper: null,
});

describe('Ask Daoris', () => {
  it('lists what the machine lacks, each with the command that does the same', () => {
    render(<AskPanel starters={LACKING} helper={null} onGo={vi.fn()} onClose={vi.fn()} />);

    const panel = screen.getByRole('complementary', { name: 'Ask Daoris' });
    expect(within(panel).getByText('1 session is waiting on you.')).toBeInTheDocument();
    expect(within(panel).getByText(/No repository is driven on this machine/)).toBeInTheDocument();
    expect(within(panel).getByText(code('daoris driver drive <repository>'))).toBeInTheDocument();
    expect(within(panel).getByText(/Name an agent under AI features/)).toBeInTheDocument();
  });

  it('goes to the screen that fixes a starter, and closes', async () => {
    const onGo = vi.fn();
    const onClose = vi.fn();
    render(<AskPanel starters={LACKING} helper={null} onGo={onGo} onClose={onClose} />);

    await userEvent.click(screen.getByRole('button', { name: "Open AI features" }));
    expect(onGo).toHaveBeenCalledWith({ view: 'settings', section: 'ai' });
    await userEvent.click(screen.getByRole('button', { name: 'Close Ask Daoris' }));
    expect(onClose).toHaveBeenCalledOnce();
  });

  /** In the right dock it is the dock's tab: no frame of its own, no title and no close — the dock has those. */
  it('drops its own frame, title and close when a dock holds it, and keeps starting again', () => {
    render(
      <AskPanel
        framed={false}
        starters={[]}
        helper="claude-code-acp"
        conversation={{ body: <p>the conversation</p>, composer: <p>the box</p>, ended: false, onNew: vi.fn() }}
        onGo={vi.fn()}
        onClose={vi.fn()}
      />,
    );

    expect(screen.queryByRole('complementary')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Close Ask Daoris' })).toBeNull();
    expect(screen.getByRole('button', { name: 'New conversation' })).toBeInTheDocument();
    expect(screen.getByText('the conversation')).toBeInTheDocument();
    expect(screen.getByText('the box')).toBeInTheDocument();
  });

  /** On every other view it is the one right region, resized by its left edge and remembered by the caller. */
  it('is resized by its left edge where it stands alone', async () => {
    const onResize = vi.fn();
    render(<AskPanel starters={[]} helper={null} width={420} range={{ min: 320, max: 800 }} onResize={onResize} onGo={vi.fn()} onClose={vi.fn()} />);

    expect(screen.getByRole('complementary', { name: 'Ask Daoris' })).toHaveStyle({ width: '420px' });
    expect(screen.getByRole('separator', { name: 'resize Ask Daoris' })).toBeInTheDocument();
  });

  /**
   * SETUP1a (D97 §2): the starters lead to the setup guide while its required steps are not all done —
   * what is missing in order, beside what is missing now.
   */
  it('leads to the setup guide while setup is not done, and not after', async () => {
    const onGo = vi.fn();
    const { rerender } = render(
      <AskPanel starters={LACKING} helper={null} setup={{ done: 2, of: 5 }} onGo={onGo} onClose={vi.fn()} />,
    );

    await userEvent.click(screen.getByRole('button', { name: 'Set up Daoris step by step: 2 of 5 done' }));
    expect(onGo).toHaveBeenCalledWith({ view: 'settings', section: 'start' });

    rerender(<AskPanel starters={LACKING} helper={null} setup={{ done: 5, of: 5 }} onGo={onGo} onClose={vi.fn()} />);
    expect(screen.queryByRole('button', { name: /set up Daoris/ })).toBeNull();
  });

  /** ASKHIST1: open, the history takes the conversation's place, its head, its words and its box together. */
  it('draws the history in the conversation’s place while it is open, with no box to write in', () => {
    const slot = {
      body: <p>the conversation</p>, composer: <p>the box</p>, ended: false, onNew: vi.fn(), title: 'what is a workspace?',
      history: <p>the history</p>, onHistory: vi.fn(), historyOpen: true,
    };
    render(<AskPanel starters={[]} helper="claude-code-acp" conversation={slot} onGo={vi.fn()} onClose={vi.fn()} />);

    expect(screen.getByText('the history')).toBeInTheDocument();
    expect(screen.queryByText('the conversation')).toBeNull();
    expect(screen.queryByText('the box')).toBeNull();
    expect(screen.queryByRole('heading', { name: 'what is a workspace?' })).toBeNull();
  });

  it('says so when the machine lacks nothing, and names the agent it runs on', () => {
    render(<AskPanel starters={[]} helper="claude-code-acp" onGo={vi.fn()} onClose={vi.fn()} />);

    expect(screen.getByText(/lacks nothing Ask Daoris knows to look for/)).toBeInTheDocument();
    expect(screen.getByText(/runs on claude-code-acp/)).toBeInTheDocument();
  });
});

/**
 * ASKHIST1c: the open conversation's head (the way back to the history, the conversation's title, a new one), the strips
 * between its words and its box (the way back to the tail, then what the next words do), and its Escape back to the list.
 */
describe.each(['en', 'zh'])('Ask Daoris’s open conversation in %s', (language) => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  const TITLE = 'to complete this https://example.atlassian.net/browse/TK-2205?focusedCommentId=1234567 so the sprint closes';
  const slot = (over: Partial<AskConversationSlot> = {}): AskConversationSlot => ({
    body: <p>the conversation</p>, composer: <form aria-label="the box"><textarea aria-label="words" /></form>, ended: false,
    title: TITLE, onNew: vi.fn(), onHistory: vi.fn(), history: <p>the history</p>, ...over,
  });
  const draw = (conversation: AskConversationSlot, framed = true) =>
    render(<AskPanel framed={framed} starters={[]} helper="claude-code-acp" conversation={conversation} onGo={vi.fn()} onClose={vi.fn()} />);

  it('heads it with the way back to the history, its whole title and a new conversation, docked or framed', async () => {
    await i18n.changeLanguage(language);
    for (const framed of [true, false]) {
      const conversation = slot();
      draw(conversation, framed);

      const back = screen.getByRole('button', { name: i18n.t('help.history.back') });
      expect(back).toHaveTextContent(i18n.t('help.history.open'));
      expect(back).toHaveClass('h-7');
      const title = screen.getByRole('heading', { name: TITLE });
      expect(title).toHaveClass('wrap-anywhere');
      expect(title).not.toHaveClass('truncate');
      // Left-led: the way back, the title, then the new conversation at its end.
      const head = title.closest('header')!;
      expect(within(head).getAllByRole('button').map((each) => each.getAttribute('aria-label'))).toEqual([
        i18n.t('help.history.back'), i18n.t('help.new'),
      ]);
      await userEvent.click(back);
      expect(conversation.onHistory).toHaveBeenCalledOnce();
      await userEvent.click(within(head).getByRole('button', { name: i18n.t('help.new') }));
      expect(conversation.onNew).toHaveBeenCalledOnce();
      cleanup();
    }
  });

  it('calls a conversation with nothing said yet a new one, and offers no new one over it', async () => {
    await i18n.changeLanguage(language);
    draw(slot({ body: null, title: null, onNew: undefined }));
    expect(screen.getByRole('heading', { name: i18n.t('help.new') })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: i18n.t('help.new') })).toBeNull();
  });

  it('puts the focus on the conversation’s heading when it is opened from the history', async () => {
    await i18n.changeLanguage(language);
    const { rerender } = draw(slot());
    expect(screen.getByRole('heading', { name: TITLE })).not.toHaveFocus();
    rerender(<AskPanel starters={[]} helper="claude-code-acp" conversation={slot({ focusHead: 1 })} onGo={vi.fn()} onClose={vi.fn()} />);
    expect(screen.getByRole('heading', { name: TITLE })).toHaveFocus();
  });

  it('draws the way back to the tail and what the next words do between the words and the box, outside the scroll', async () => {
    await i18n.changeLanguage(language);
    const scroller = createRef<HTMLDivElement>();
    render(
      <AskPanel
        starters={[]} helper="claude-code-acp" scroller={scroller} onGo={vi.fn()} onClose={vi.fn()}
        conversation={slot({ ended: true, tail: <button type="button">to the tail</button>, note: <p>what the next words do</p> })}
      />,
    );

    const tail = screen.getByRole('button', { name: 'to the tail' });
    const note = screen.getByText('what the next words do');
    const box = screen.getByRole('form', { name: 'the box' });
    for (const strip of [tail, note]) expect(scroller.current!.contains(strip)).toBe(false);
    // In the order a reader meets them: the words, the way back to the tail, the line, the box.
    expect(scroller.current!.compareDocumentPosition(tail) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(tail.compareDocumentPosition(note) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(note.compareDocumentPosition(box) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('goes back to the history on Escape where it was opened from it, never from a field, a form or a word being composed', async () => {
    await i18n.changeLanguage(language);
    const conversation = slot({ escapeToList: true });
    draw(conversation);

    screen.getByRole('heading', { name: TITLE }).focus();
    await userEvent.keyboard('{Escape}');
    expect(conversation.onHistory).toHaveBeenCalledOnce();

    fireEvent.keyDown(screen.getByRole('textbox', { name: 'words' }), { key: 'Escape' });
    fireEvent.keyDown(screen.getByRole('heading', { name: TITLE }), { key: 'Escape', isComposing: true });
    expect(conversation.onHistory).toHaveBeenCalledOnce();

    cleanup();
    const elsewhere = slot({ escapeToList: false });
    draw(elsewhere);
    screen.getByRole('heading', { name: TITLE }).focus();
    await userEvent.keyboard('{Escape}');
    expect(elsewhere.onHistory).not.toHaveBeenCalled();
  });
});
