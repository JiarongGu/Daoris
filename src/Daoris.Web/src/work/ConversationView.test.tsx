import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import '../i18n';
import { ConversationView } from './ConversationView';
import { type SessionEvent, toTurns } from './conversation';

// The conversation's molecule (D76, CONV2), props in: every state it can be in is a turn list built
// by the same fold the organism uses, so what is asserted here is what a session would show.

const at = '2026-09-25T00:00:00Z';
let seq = 0;
const ev = (over: Partial<SessionEvent>): SessionEvent => ({ seq: ++seq, at, kind: 'message', ...over });
const view = (events: SessionEvent[], props: Partial<Parameters<typeof ConversationView>[0]> = {}) =>
  render(<ConversationView turns={toTurns(events).turns} {...props} />);

const FINISHED = () => [
  ev({ kind: 'user', origin: 'person', text: 'cap the hydration' }),
  ev({ kind: 'thought', text: 'the cap belongs in the streamer' }),
  ev({ kind: 'tool', id: 'c1', title: 'Read src/chunk.rs', toolKind: 'read', status: 'completed' }),
  ev({ kind: 'tool', id: 'c2', title: 'Edit src/chunk.rs', toolKind: 'edit', status: 'completed',
    content: [{ type: 'diff', path: 'src/chunk.rs', oldText: 'let cap = 0;', newText: 'let cap = 4;\n// per frame' }] }),
  ev({ kind: 'message', text: 'Capped at **4** per frame.' }),
  ev({ kind: 'turn', stopReason: 'end_turn' }),
];

describe('ConversationView', () => {
  it('shows what was asked and the answer, and folds a finished turn\'s work into one counted row', async () => {
    view(FINISHED());

    expect(screen.getByText('you')).toBeTruthy();
    expect(screen.getByText('cap the hydration')).toBeTruthy();
    // The answer is open, and Markdown.
    expect(screen.getByText('4').tagName).toBe('STRONG');
    // The work is one row that counts it, and nothing of it is drawn yet.
    const fold = screen.getByRole('button', { name: /2 tool calls · thought/ });
    expect(screen.queryByText('Read src/chunk.rs')).toBeNull();

    await userEvent.click(fold);
    expect(screen.getByText('Read src/chunk.rs')).toBeTruthy();
    expect(screen.getByText('Edit src/chunk.rs')).toBeTruthy();
    // An edit says its size closed: one line removed, two added.
    expect(screen.getByText('+2')).toBeTruthy();
    expect(screen.getByText('−1')).toBeTruthy();
  });

  it('keeps a running turn open, and says it is working', () => {
    view([
      ev({ kind: 'user', origin: 'person', text: 'and test it' }),
      ev({ kind: 'tool', id: 'c3', title: 'Run the tests', toolKind: 'execute', status: 'in_progress' }),
    ], { live: true });

    expect(screen.getByText('Run the tests')).toBeTruthy();
    expect(screen.getByText('running')).toBeTruthy();
    expect(screen.getByText('working…')).toBeTruthy();
  });

  /** The composed target is long and machine-written: two lines, and the rest on a press. */
  it('folds the target the driver composed, and names it as that', async () => {
    view([ev({ kind: 'user', origin: 'target', text: 'take quest #q1\n\nthe whole target…' })]);

    expect(screen.getByText('the target Daoris composed')).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'show all' }));
    expect(screen.getByRole('button', { name: 'fold' })).toBeTruthy();
  });

  it('opens a failed tool call by itself, since that is the line a reader came for', () => {
    view([ev({ kind: 'tool', id: 'c4', title: 'git push', toolKind: 'execute', status: 'failed', output: 'rejected' })]);

    expect(screen.getByText('failed')).toBeTruthy();
    expect(screen.getByText('rejected')).toBeTruthy();
  });

  /** D76 §5: the agent's text is content, never markup — raw HTML is shown as the text it is. */
  it('never renders the agent\'s HTML as markup, and opens links outside the window', () => {
    view([ev({ kind: 'message', text: 'see <img src=x onerror="alert(1)"> and [the docs](https://docs.example)' })]);

    expect(document.querySelector('img')).toBeNull();
    const link = screen.getByRole('link', { name: 'the docs' });
    expect(link.getAttribute('target')).toBe('_blank');
  });

  it('highlights a fenced block in its language, names it, and offers a copy', () => {
    const { container } = view([ev({ kind: 'message', text: '```rust\nlet cap = 4;\n```' })]);

    const figure = container.querySelector('figure')!;
    expect(within(figure).getByText('rust')).toBeTruthy();
    expect(within(figure).getByRole('button', { name: /copy/ })).toBeTruthy();
    expect(figure.querySelector('.hljs-keyword')?.textContent).toBe('let');
  });

  it('says a text-only session has no conversation, pointing at the console', () => {
    view([]);

    expect(screen.getByText(/carries only text/)).toBeTruthy();
  });

  /**
   * 🔴 Seen on the window (CONV2's look): a parked chat on the pipe door read "Nothing said yet."
   * A structured door records the target the moment it starts, so an empty record is never a
   * conversation that has not begun — running or not.
   */
  it('never tells a running text-only session that nothing has been said', () => {
    view([], { live: true });

    expect(screen.getByText(/carries only text/)).toBeTruthy();
    expect(screen.queryByText(/Nothing said yet/)).toBeNull();
  });

  it('marks a finished step of the plan with a check, never a strike-through', () => {
    view([ev({ kind: 'plan', entries: [{ content: 'Read the streamer', status: 'completed' }] })]);

    expect(screen.getByText('Read the streamer').className).not.toContain('line-through');
    expect(screen.getByText('✓')).toBeTruthy();
  });

  it('claims nothing about a record that has not answered yet', () => {
    const { container } = view([], { loaded: false });

    expect(container.textContent).toBe('');
  });

  it('offers earlier turns when the record holds more than the page', async () => {
    let asked = 0;
    view(FINISHED(), { earlier: true, onLoadEarlier: () => { asked += 1; } });

    await userEvent.click(screen.getByRole('button', { name: 'load earlier' }));
    expect(asked).toBe(1);
  });

  /**
   * 🔴 What an empty record means is the DOOR's to say, never a guess from the emptiness (CONV3b). A
   * fresh chat on a structured door was told its door carries only text.
   */
  it('tells a fresh chat on a structured door that nothing has been said yet', () => {
    view([], { structured: true, chat: true, live: true });

    expect(screen.getByText(/Nothing said yet/)).toBeTruthy();
    expect(screen.queryByText(/carries only text/)).toBeNull();
  });

  it('says a finished chat on a structured door had nothing said in it', () => {
    view([], { structured: true, chat: true, live: false });

    expect(screen.getByText('Nothing was said in this conversation.')).toBeTruthy();
  });

  it('keeps the console sentence for a text door, and wherever the door is not known', () => {
    view([], { structured: false, chat: true, live: true });
    expect(screen.getByText(/carries only text/)).toBeTruthy();
  });

  it('names a tool\'s paths relative to the session\'s tree', async () => {
    view([
      ev({ kind: 'tool', id: 'c5', title: 'Edit /srv/engine/src/chunk.rs', toolKind: 'edit', status: 'completed',
        locations: ['/srv/engine/src/chunk.rs'],
        content: [{ type: 'diff', path: '/srv/engine/src/chunk.rs', oldText: 'a', newText: 'b' }] }),
    ], { tree: '/srv/engine' });

    await userEvent.click(screen.getByRole('button', { name: /Edit src\/chunk\.rs/ }));
    expect(screen.queryByText(/\/srv\/engine/)).toBeNull();
    expect(screen.getByText('src/chunk.rs')).toBeTruthy();
  });

  it('marks a turn that ended for a reason other than finishing', () => {
    view([ev({ kind: 'message', text: 'stopping' }), ev({ kind: 'turn', stopReason: 'cancelled' })]);

    expect(screen.getByText('the turn ended: cancelled')).toBeTruthy();
  });
});
