import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import { ConversationView } from './ConversationView';
import { type SessionEvent, settle, toTurns } from './conversation';
import { CLAIMED } from './handedFixtures';

// The conversation's molecule (D76, CONV2), props in: every state it can be in is a turn list built
// by the same fold the organism uses, so what is asserted here is what a session would show.

const at = '2026-09-25T00:00:00Z';
let seq = 0;
const ev = (over: Partial<SessionEvent>): SessionEvent => ({ seq: ++seq, at, kind: 'message', ...over });
const view = (events: SessionEvent[], props: Partial<Parameters<typeof ConversationView>[0]> = {}) =>
  render(<Tooltip.Provider><ConversationView turns={toTurns(events).turns} {...props} /></Tooltip.Provider>);

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

  /**
   * CONV5: a finished turn says how long it took and what it consumed — the driver's clock and the
   * harness's counts — on one quiet line, with the breakdown on hover. A turn whose wire reported no
   * tokens says only its time, and a running turn has no line yet.
   */
  it('says under a finished turn how long it took and what it consumed', async () => {
    const stamp = (seconds: number) => new Date(Date.UTC(2026, 8, 26, 10) + seconds * 1000).toISOString();
    view([
      ev({ kind: 'user', origin: 'person', text: 'read it', at: stamp(0) }),
      ev({ kind: 'tool', id: 't9', title: 'Read a.md', toolKind: 'read', status: 'completed', at: stamp(1.5) }),
      ev({ kind: 'message', text: 'hello', at: stamp(4) }),
      ev({ kind: 'turn', stopReason: 'end_turn', at: stamp(4.75), tokens: { input: 4, output: 80, cacheRead: 51_061, cacheWrite: 16_717 } }),
      ev({ kind: 'user', origin: 'person', text: 'again', at: stamp(10) }),
      ev({ kind: 'message', text: 'done', at: stamp(10.4) }),
      ev({ kind: 'turn', stopReason: 'end_turn', at: stamp(11) }),
      ev({ kind: 'user', origin: 'person', text: 'and more', at: stamp(20) }),
      ev({ kind: 'message', text: 'on it', at: stamp(21) }),
    ], { live: true });

    // METER1: what it read anew (new and written to its cache) is "in"; what it re-read from its cache is
    // said apart, since a long turn re-reads its whole context on every call and would bury the rest.
    const first = screen.getByText('4.8s · 16.7K in · 51.1K cached · 80 out');
    expect(screen.getByText('1s')).toBeTruthy();
    // The running turn has said nothing about itself yet.
    expect(screen.queryByText(/on it.*·/)).toBeNull();

    await userEvent.hover(first);
    expect((await screen.findAllByText(/the first answer came after 1\.5s/))[0]).toBeTruthy();
    expect(screen.getAllByText(/16,721 tokens in \(4 new, 16,717 written to its cache\), 51,061 read from its cache, and 80 out/)[0]).toBeTruthy();
  });

  /** REVIEW2: an edit's lines are the review's lines — highlighted in the file's own language, and signed. */
  it('highlights an edit in the language of the file it changed', async () => {
    view(FINISHED());
    await userEvent.click(screen.getByRole('button', { name: /2 tool calls/ }));
    await userEvent.click(screen.getByText('Edit src/chunk.rs'));

    const keyword = document.querySelector('.hljs-keyword');
    expect(keyword?.textContent).toBe('let');
    expect(screen.getAllByText('+')).toHaveLength(2);
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

  /**
   * The agent speaks after its turn has ended when its background work finishes (CONSOLE2a). Those
   * words are no turn running: with the driver saying none is in flight, they carry no *working…*,
   * which looking at CONSOLE2 found under them for good, with nothing running.
   */
  it('says working only while a turn is in flight, not under words the agent said after its turn', () => {
    const events = [
      ev({ kind: 'user', origin: 'person', text: 'start the ticker' }),
      ev({ kind: 'message', text: 'DONE' }),
      ev({ kind: 'turn', stopReason: 'end_turn' }),
      ev({ kind: 'message', text: 'The background ticker finished.' }),
    ];

    const { unmount } = view(events, { live: true, turnRunning: false });
    expect(screen.getByText('The background ticker finished.')).toBeTruthy();
    expect(screen.queryByText('working…')).toBeNull();
    unmount();

    view(events, { live: true, turnRunning: true });
    expect(screen.getByText('working…')).toBeTruthy();
  });

  /** The composed target is long and machine-written: two lines, and the rest on a press. */
  it('folds the target the driver composed, and names it as that', async () => {
    view([ev({ kind: 'user', origin: 'target', text: 'take quest #q1\n\nthe whole target…' })]);

    expect(screen.getByText('The target Daoris composed')).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'Show all' }));
    expect(screen.getByRole('button', { name: 'Fold' })).toBeTruthy();
  });

  /**
   * CONTEXT1: beneath the target, what it was composed of, from the account its event keeps — folded to its size and its
   * sections; a target from before the account was kept says so; the person's own words carry none.
   */
  it('shows beneath the target what it was handed, section by section, and says when that was not kept', async () => {
    view([
      ev({ kind: 'user', origin: 'target', text: 'take quest #abc123', account: CLAIMED }),
      ev({ kind: 'turn', stopReason: 'end_turn' }),
      ev({ kind: 'user', origin: 'person', text: 'and the changelog' }),
    ]);

    const handed = screen.getByRole('button', { name: /What it was handed/ });
    expect(handed).toHaveTextContent('6,786 characters in 15 sections');
    await userEvent.click(handed);
    expect(screen.getByRole('rowheader', { name: 'The quest' })).toBeInTheDocument();
    expect(screen.getAllByRole('region', { name: 'What it was handed' })).toHaveLength(1);

    view([ev({ kind: 'user', origin: 'target', text: 'take quest #q1' })]);
    expect(screen.getByText('Its sections were not kept: it was handed before Daoris kept them.')).toBeInTheDocument();
  });

  /**
   * UX5 U63, seen on the window: the protocol door's Claude Code adapter wraps a read's text in a
   * Markdown fence, and the card showed the fence as the output's first and last lines. A text that
   * is one fenced block shows what is inside it; anything else is shown as it came.
   */
  it('shows a read’s text without the fence the adapter wrapped it in', async () => {
    view([ev({
      kind: 'tool', id: 'c6', title: 'Read README.md', toolKind: 'read', status: 'completed',
      content: [{ type: 'content', text: '```\n1\t# examples/game\n2\tThe content half\n```' }],
    })]);
    await userEvent.click(screen.getByRole('button', { name: /Read README\.md/ }));

    const shown = screen.getByText(/# examples\/game/);
    expect(shown.textContent).toBe('1\t# examples/game\n2\tThe content half');

    view([ev({
      kind: 'tool', id: 'c7', title: 'Run it', toolKind: 'execute', status: 'completed',
      content: [{ type: 'content', text: 'before\n```\ninside\n```\nafter' }],
    })]);
    await userEvent.click(screen.getByRole('button', { name: /Run it/ }));
    expect(screen.getByText(/before/).textContent).toBe('before\n```\ninside\n```\nafter');
  });

  it('opens a failed tool call by itself, since that is the line a reader came for', () => {
    view([ev({ kind: 'tool', id: 'c4', title: 'git push', toolKind: 'execute', status: 'failed', output: 'rejected' })]);

    expect(screen.getByText('failed')).toBeTruthy();
    expect(screen.getByText('rejected')).toBeTruthy();
  });

  /**
   * HELP4: a call the driver refused is not the harness failing: it reads as not allowed, in the quiet
   * tone, closed, where a failure is the alarm and opens itself.
   */
  it('reads a call the driver refused as not allowed, not as a failure', () => {
    const { container } = view([ev({ kind: 'tool', id: 'c6', title: 'ls data', toolKind: 'execute', status: 'refused', output: 'denied' })]);

    expect(screen.getByText('not allowed here')).toBeTruthy();
    expect(screen.queryByText('failed')).toBeNull();
    expect(screen.queryByText('denied')).toBeNull();
    expect(container.querySelector('.border-warn')).toBeNull();
  });

  /**
   * REV3 web-work F9: a call is drawn while it runs and fails later, on the same card. The card read
   * "failed" once, when it first drew, so a call that failed mid-turn stayed closed over the line the
   * reader came for. It opens when it fails — unless the person has already opened or closed it.
   */
  it('opens a tool call that fails after it was drawn running', () => {
    const running = [ev({ kind: 'tool', id: 'c5', title: 'npm test', toolKind: 'execute', status: 'in_progress' })];
    const { rerender } = view(running);
    expect(screen.queryByText('3 failing')).toBeNull();

    const failed = [...running, ev({ kind: 'tool', id: 'c5', status: 'failed', output: '3 failing' })];
    rerender(<Tooltip.Provider><ConversationView turns={toTurns(failed).turns} /></Tooltip.Provider>);

    expect(screen.getByText('3 failing')).toBeTruthy();
  });

  /** D76 §5: the agent's text is content, never markup — raw HTML is shown as the text it is. */
  it('never renders the agent\'s HTML as markup, and opens links outside the window', () => {
    view([ev({ kind: 'message', text: 'see <img src=x onerror="alert(1)"> and [the docs](https://docs.example)' })]);

    expect(document.querySelector('img')).toBeNull();
    const link = screen.getByRole('link', { name: 'the docs' });
    expect(link.getAttribute('target')).toBe('_blank');
  });

  /**
   * REV3: a Markdown IMAGE is not raw HTML, so the test above never saw it. Rendered, it is a request
   * the page makes with no click — whatever an agent puts in the URL leaves the machine, even when
   * its harness's own network tools are refused (D47 §4, D52). It is a link now, to be opened on
   * purpose.
   */
  it('never loads an agent\'s Markdown image — it is a link a person may choose to open', () => {
    view([ev({ kind: 'message', text: '![build log](https://example.invalid/p.png?d=C%3A%5Cwork)' })]);

    expect(document.querySelector('img')).toBeNull();
    const link = screen.getByRole('link', { name: /build log/ });
    expect(link.getAttribute('target')).toBe('_blank');
    expect(link.getAttribute('href')).toBe('https://example.invalid/p.png?d=C%3A%5Cwork');
  });

  /**
   * UX5 U4, seen on the window (CONV4b): an agent answered one item per line and the page drew one
   * paragraph, because Markdown makes a single newline a space. The agent wrote for a terminal, where
   * a newline is a newline, so a line it ended stays ended. A blank line is still a paragraph, and a
   * fenced block keeps its own lines.
   */
  // UX5 U16: a maximized window showed the conversation at half its width.
  it('follows the width it is given, as the head and the composer do', () => {
    view(FINISHED());
    expect(screen.getByRole('region', { name: 'conversation' }).className).not.toMatch(/max-w-/);
  });

  it('keeps a line the agent ended, and still makes a paragraph of a blank line', () => {
    const { container } = view([ev({
      kind: 'message', text: 'one\ntwo\nthree\n\nafter\n\n```\na\nb\n```',
    })]);

    const [lines, after] = [...container.querySelectorAll('.markdown p')];
    expect(lines!.querySelectorAll('br')).toHaveLength(2);
    expect(lines!.textContent).toBe('one\ntwo\nthree');
    expect(after!.textContent).toBe('after');
    expect(container.querySelector('figure code')!.textContent).toBe('a\nb');
  });

  it('highlights a fenced block in its language, names it, and offers a copy', () => {
    const { container } = view([ev({ kind: 'message', text: '```rust\nlet cap = 4;\n```' })]);

    const figure = container.querySelector('figure')!;
    expect(within(figure).getByText('rust')).toBeTruthy();
    expect(within(figure).getByRole('button', { name: /Copy/ })).toBeTruthy();
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

  /**
   * SESS1 S3, from the first real workspace: a driven session is one turn, so folding the turn whole hid
   * the agent's account of hundreds of calls behind one row. Its words stay; the work between them folds.
   */
  it('keeps every word the agent said, and folds the work between them into runs that count failures', async () => {
    view([
      ev({ kind: 'user', origin: 'target', text: 'Your target is the quest…' }),
      ev({ kind: 'message', text: 'Looking at the loader first.' }),
      ev({ kind: 'tool', id: 'r1', title: 'Read loader.rs', toolKind: 'read', status: 'completed' }),
      ev({ kind: 'tool', id: 'r2', title: 'git stash list', toolKind: 'execute', status: 'failed', output: 'refused' }),
      ev({ kind: 'message', id: 'm2', text: 'Clean. Now the gates.' }),
      ev({ kind: 'message', id: 'm3', text: 'Landed.' }),
      ev({ kind: 'turn', stopReason: 'end_turn' }),
    ]);

    expect(screen.getByText('Looking at the loader first.')).toBeTruthy();
    expect(screen.getByText('Clean. Now the gates.')).toBeTruthy();
    const fold = screen.getByRole('button', { name: /2 tool calls · 1 failed/ });
    expect(screen.queryByText('Read loader.rs')).toBeNull();

    await userEvent.click(fold);
    expect(screen.getByText('Read loader.rs')).toBeTruthy();
    expect(screen.getByText('refused')).toBeTruthy();
  });

  /** SESS1 S1: a long run is read from its ask, and *load earlier* sits where the missing events belong. */
  it('reads a long run from what it was asked, with load earlier where the gap is', async () => {
    let asked = 0;
    const opening = ev({ kind: 'user', origin: 'target', text: 'Your target is the quest…' });
    const page = [ev({ kind: 'message', text: 'Clean. Now the gates.' })];
    render(
      <Tooltip.Provider>
        <ConversationView turns={toTurns(page, { opening }).turns} earlier onLoadEarlier={() => { asked += 1; }} />
      </Tooltip.Provider>,
    );

    const target = screen.getByText('The target Daoris composed');
    const earlier = screen.getByRole('button', { name: 'Load earlier' });
    const words = screen.getByText('Clean. Now the gates.');
    // In the order a reader reads them: the ask, the gap, then the page.
    expect(target.compareDocumentPosition(earlier) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(earlier.compareDocumentPosition(words) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();

    await userEvent.click(earlier);
    expect(asked).toBe(1);
  });

  /** SESS1 S4: the session ended inside the turn, so its open call never finished — never *running* for good. */
  it('says a session ended inside the turn it cut, and the call it left open stopped', () => {
    const events = [
      ev({ kind: 'user', origin: 'target', text: 'go' }),
      ev({ kind: 'tool', id: 'g1', title: 'npm run gates', toolKind: 'execute', status: 'in_progress' }),
    ];
    render(<Tooltip.Provider><ConversationView turns={settle(toTurns(events).turns, false)} /></Tooltip.Provider>);

    expect(screen.getByText('stopped')).toBeTruthy();
    expect(screen.queryByText('running')).toBeNull();
    expect(screen.getByText(/The session ended here/)).toBeTruthy();
  });

  /**
   * STEER1 (D136): the person's words to a working session show the moment they are said, as theirs and waiting, saying
   * when the session reads them — and, once the session has ended without taking them, that it never did.
   */
  it('shows the person\'s words waiting in the turn, saying when the session reads them', () => {
    const working = [
      ev({ kind: 'user', origin: 'target', text: 'go' }),
      ev({ kind: 'tool', id: 'h1', title: 'npm run gates', toolKind: 'execute', status: 'in_progress' }),
      ev({ kind: 'user', origin: 'person', id: 'said-1', reaches: 'next-step', text: 'the budget is in level.json', files: ['level.json'] }),
      ev({ kind: 'user', origin: 'person', id: 'said-2', reaches: 'turn-end', text: 'and cap it at 64 KiB' }),
    ];
    const { unmount } = view(working);

    expect(screen.getByText('the budget is in level.json')).toBeTruthy();
    expect(screen.getByText('Held: it reads this at its next step.')).toBeTruthy();
    expect(screen.getByText('Held: it reads this when its turn ends.')).toBeTruthy();
    expect(screen.getByText('level.json')).toBeTruthy();
    unmount();

    render(<Tooltip.Provider><ConversationView turns={settle(toTurns(working).turns, false)} /></Tooltip.Provider>);
    expect(screen.getAllByText('It ended before reading this.')).toHaveLength(2);
    expect(screen.queryByText(/Held:/)).toBeNull();
  });

  /**
   * MSG1f (D137 §3.1): words said to a session after it parked or ended show at once, as a turn waiting at its foot,
   * saying the same session goes on with them — a chat's conversation as it opens again. The session's end is not
   * theirs: no *it ended before reading this*, no *working…* while they wait, and no cut turn of their own.
   */
  it('shows words said after the end waiting at its foot, saying the same session goes on with them', () => {
    const ended = [
      ev({ kind: 'user', origin: 'target', text: 'go' }),
      ev({ kind: 'message', text: 'Done, committed.' }),
      ev({ kind: 'turn', stopReason: 'end_turn' }),
      ev({ kind: 'user', origin: 'person', id: 'w1', reaches: 'resume', text: 'also cap it at 64 KiB' }),
    ];
    const { unmount } = render(
      <Tooltip.Provider><ConversationView turns={settle(toTurns(ended).turns, false)} /></Tooltip.Provider>,
    );
    expect(screen.getByText('also cap it at 64 KiB')).toBeTruthy();
    expect(screen.getByText("Held: the same session goes on with this at the driver's next look.")).toBeTruthy();
    expect(screen.queryByText('It ended before reading this.')).toBeNull();
    expect(screen.queryByText(/before this turn did/)).toBeNull();
    unmount();

    // Going on, the record is live again, and nothing runs until its first prompt takes the words.
    const { unmount: gone } = render(
      <Tooltip.Provider><ConversationView turns={toTurns(ended).turns} live /></Tooltip.Provider>,
    );
    expect(screen.queryByText('working…')).toBeNull();
    gone();

    render(<Tooltip.Provider><ConversationView turns={settle(toTurns(ended).turns, false)} chat /></Tooltip.Provider>);
    expect(screen.getByText('Held: the same conversation goes on with this as it opens again.')).toBeTruthy();
  });

  /**
   * MSG1f (D137 §3.1): where a fallback handed the words to a new session, the conversation says so once, where they
   * were shown, the session a door to it; the driver's own English line is not said beside it.
   */
  it('says where the words went, the session a door, once', async () => {
    const onSession = vi.fn();
    view([
      ev({ kind: 'user', origin: 'target', text: 'go' }),
      ev({ kind: 'turn', stopReason: 'end_turn' }),
      ev({ kind: 'user', origin: 'person', id: 'w1', reaches: 'resume', text: 'also cap it' }),
      ev({ kind: 'note', text: '— your words went to session `n3wn3w00`, because its tree is gone.', words: ['w1'], to: 'n3wn3w00', why: 'tree' }),
    ], { onSession });

    const went = screen.getByText((_, element) => element?.tagName === 'P'
      && element.textContent === 'Your words went to session n3wn3w00, because its tree is gone.');
    expect(screen.queryByText(/Held:/)).toBeNull();
    expect(screen.queryByText(/— your words went/)).toBeNull();
    await userEvent.click(within(went).getByRole('button', { name: 'n3wn3w00' }));
    expect(onSession).toHaveBeenCalledWith('n3wn3w00');
  });

  it('names the session as text where nothing here can open it, and lets the driver’s line stand for a reason it cannot word', () => {
    const { unmount } = view([
      ev({ kind: 'user', origin: 'person', id: 'w1', reaches: 'resume', text: 'also cap it' }),
      ev({ kind: 'note', text: '— your words went to session `n3wn3w00`, because its tree is gone.', words: ['w1'], to: 'n3wn3w00', why: 'tree' }),
    ]);
    expect(screen.queryByRole('button', { name: 'n3wn3w00' })).toBeNull();
    expect(screen.getByText((_, element) => element?.tagName === 'P'
      && element.textContent === 'Your words went to session n3wn3w00, because its tree is gone.')).toBeTruthy();
    unmount();

    // A reason that names an agent the page was not told, or a code it does not know: the driver's words stand.
    view([
      ev({ kind: 'user', origin: 'person', id: 'w1', reaches: 'resume', text: 'also cap it' }),
      ev({ kind: 'note', text: '— your words went to session `n3wn3w00`, because it ran on `a`, and starts here now run on `b`.', words: ['w1'], to: 'n3wn3w00', why: 'adapter' }),
    ]);
    expect(screen.getByText(/because it ran on `a`/)).toBeTruthy();
  });

  /**
   * MSG1f (D137 §2.2): words a closed quest's session or a chat cannot go on with stay as said, the conversation says
   * why in the page's words, and one press starts a new conversation with them — the person's, since a new conversation
   * has none of this one's context.
   */
  it('says the words cannot go on here and offers one press that starts a conversation with them', async () => {
    const onStartFrom = vi.fn();
    view([
      ev({ kind: 'user', origin: 'target', text: 'go' }),
      ev({ kind: 'turn', stopReason: 'end_turn' }),
      ev({ kind: 'user', origin: 'person', id: 'w1', reaches: 'resume', text: 'first' }),
      ev({ kind: 'user', origin: 'person', id: 'w2', reaches: 'resume', text: 'second' }),
      ev({ kind: 'note', text: '— It cannot go on in this session, because its conversation could not be resumed.', words: ['w1', 'w2'], why: 'refused' }),
    ], { onStartFrom });

    expect(screen.getByText('It cannot go on in this session, because its conversation could not be continued.')).toBeTruthy();
    expect(screen.queryByText(/Held:/)).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Start a conversation with these words' }));
    expect(onStartFrom).toHaveBeenCalledWith(['first', 'second']);
  });

  it('offers no press where the page holds not every word, or nothing could start one', () => {
    const { unmount } = view([
      ev({ kind: 'user', origin: 'person', id: 'w2', reaches: 'resume', text: 'second' }),
      ev({ kind: 'note', text: '— It cannot go on in this session, because its tree is gone.', words: ['w1', 'w2'], why: 'tree' }),
    ], { onStartFrom: vi.fn() });
    expect(screen.getByText('It cannot go on in this session, because its tree is gone.')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Start a conversation with these words' })).toBeNull();
    unmount();

    view([
      ev({ kind: 'user', origin: 'person', id: 'w1', reaches: 'resume', text: 'first' }),
      ev({ kind: 'note', text: '— It cannot go on in this session, because its tree is gone.', words: ['w1'], why: 'tree' }),
    ]);
    expect(screen.queryByRole('button', { name: 'Start a conversation with these words' })).toBeNull();
  });

  it('words a reason that names an agent from what it is handed', () => {
    view([
      ev({ kind: 'user', origin: 'person', id: 'w1', reaches: 'resume', text: 'first' }),
      ev({ kind: 'note', text: '— It cannot go on in this session, because its conversation is open in another client of its agent.', words: ['w1'], why: 'elsewhere' }),
    ], { reasons: { agent: 'Codex' } });
    expect(screen.getByText('It cannot go on in this session, because its conversation is open in another client of Codex.')).toBeTruthy();
  });

  /**
   * MSG1c3 (D142 point 1): a word the person's stop cut off on its way is said in the page's words, in the reader's
   * language, from the note's code: once, where the word was shown, which waits no longer. A code the page does not know
   * leaves the driver's English line.
   */
  it('says in the reader’s language that the agent may have read a word the stop cut off', async () => {
    const stopped = () => [
      ev({ kind: 'user', origin: 'person', text: 'read the five files' }),
      ev({ kind: 'user', origin: 'person', id: 'said-1', reaches: 'next-step', text: 'PINEAPPLE' }),
      ev({ kind: 'note', text: '— it may have read what you added; its answer was not kept.', words: ['said-1'], code: 'lost' }),
      ev({ kind: 'turn', stopReason: 'cancelled' }),
    ];
    const { unmount } = view(stopped());
    expect(screen.getByText('It may have read this; its answer was not kept.')).toBeTruthy();
    expect(screen.getByText('PINEAPPLE')).toBeTruthy();
    expect(screen.queryByText(/— it may have read/)).toBeNull();
    expect(screen.queryByText(/Held:/)).toBeNull();
    unmount();

    await i18n.changeLanguage('zh');
    try {
      const { unmount: shown } = view(stopped());
      expect(screen.getByText('它可能已读到这条，但它的回复没有保留。')).toBeTruthy();
      expect(screen.queryByText(/may have read/)).toBeNull();
      shown();
    } finally {
      await i18n.changeLanguage('en');
    }

    view([ev({ kind: 'note', text: '— a line a newer driver coded.', code: 'newer' })]);
    expect(screen.getByText('— a line a newer driver coded.')).toBeTruthy();
  });

  /** SESS1 S5: a call the page holds only the updates of began earlier, and says so, never its id. */
  it('says a call began earlier where the page holds only its updates, never naming it by its id', () => {
    view([ev({ kind: 'tool', id: 'toolu_01ExampleCallId', status: 'completed' })]);

    expect(screen.queryByText(/toolu_/)).toBeNull();
    expect(screen.getByText('a call begun earlier')).toBeTruthy();
  });

  /** SESS1 S9: a jump that lands inside a fold opens it and marks the block; the person may fold it again. */
  it('opens the fold a jump lands in, marks the block, and lets the person fold it again', async () => {
    const events = [
      ev({ kind: 'user', origin: 'target', text: 'go' }),
      ev({ kind: 'message', id: 'w1', text: 'Starting.' }),
      ev({ kind: 'tool', id: 'j1', title: 'Read a', status: 'completed' }),
      ev({ kind: 'tool', id: 'j2', title: 'npm test', status: 'failed', output: '3 failing' }),
      ev({ kind: 'message', id: 'w2', text: 'Done.' }),
      ev({ kind: 'turn', stopReason: 'end_turn' }),
    ];
    const { turns, where } = toTurns(events);
    const failedAt = events[3]!.seq;
    render(<Tooltip.Provider><ConversationView turns={turns} reveal={where[failedAt]} /></Tooltip.Provider>);

    expect(screen.getByText('npm test')).toBeTruthy();
    const block = screen.getByText('npm test').closest('[data-block]')!;
    expect(block.className).toContain('outline-accent');

    await userEvent.click(screen.getByRole('button', { name: /2 tool calls/ }));
    expect(screen.queryByText('npm test')).toBeNull();
  });

  /**
   * SESS1: a tool's output read well — how long the call ran, by the driver's clock, and, closed, how
   * much it carried. An exit code is not on the wire, and nothing is read out of the output for one.
   */
  it('says how long a call ran and, closed, how many lines it carried', () => {
    view([
      ev({ kind: 'tool', id: 'd1', title: 'npm run gates', toolKind: 'execute', status: 'in_progress', at: '2026-09-25T00:00:00Z' }),
      ev({ kind: 'tool', id: 'd1', status: 'completed', output: 'one\ntwo\nthree', at: '2026-09-25T00:01:05Z' }),
    ]);

    expect(screen.getByText('1m 5s')).toBeTruthy();
    expect(screen.getByText('3 lines')).toBeTruthy();
  });

  /** SESS1 S6: a record from before the refusal named its call carries the request's JSON; two lines, the rest on a press. */
  it('shows a long note of the driver\'s as two lines, and the rest on a press', async () => {
    const long = `permission refused: {"toolCallId":"toolu_01","rawInput":{"command":"${'x'.repeat(300)}"}} — the repository's own configuration governs`;
    view([ev({ kind: 'note', text: long })]);

    const show = screen.getByRole('button', { name: 'Show all' });
    expect(show).toHaveAttribute('aria-expanded', 'false');
    await userEvent.click(show);
    expect(show).toHaveAttribute('aria-expanded', 'true');
  });

  it('offers earlier turns when the record holds more than the page', async () => {
    let asked = 0;
    view(FINISHED(), { earlier: true, onLoadEarlier: () => { asked += 1; } });

    await userEvent.click(screen.getByRole('button', { name: 'Load earlier' }));
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

  it('names what the person attached under what they asked', () => {
    view([ev({ kind: 'user', origin: 'person', text: 'what does this log say?', files: ['run.log'] })]);

    const attached = screen.getByRole('list', { name: 'attached' });
    expect(within(attached).getByText('run.log')).toBeTruthy();
  });

  it('marks a turn that ended for a reason other than finishing', () => {
    view([ev({ kind: 'message', text: 'out of room' }), ev({ kind: 'turn', stopReason: 'max_tokens' })]);

    expect(screen.getByText('the turn ended: max_tokens')).toBeTruthy();
  });

  /**
   * CONV4b: a stopped turn reads as stopped — in the passive, because a driven session's timeout
   * cancels a turn too, and the page cannot know it was the person — never as the wire's word, and
   * never as a failure.
   */
  it('says a cancelled turn was stopped, even one stopped before it said anything', () => {
    view([ev({ kind: 'user', origin: 'person', text: 'count to a thousand' }), ev({ kind: 'turn', stopReason: 'cancelled' })]);

    expect(screen.getByText('count to a thousand')).toBeTruthy();
    expect(screen.getByText('the turn was stopped here')).toBeTruthy();
    expect(screen.queryByText(/cancelled/)).toBeNull();
  });

  /**
   * CONV4b: the call a stop cut is drawn as stopped, in the quiet tone, and stays closed — Claude Code
   * answers it as failed, and the alarm's colour with its sentence open would read as something that
   * went wrong. What the harness said is still there when the person opens it.
   */
  it('draws the call a stop cut as stopped, closed, and still holding what the harness said', async () => {
    view([
      ev({ kind: 'user', origin: 'person', text: 'run the tests' }),
      ev({ kind: 'tool', id: 'c1', title: 'Run the tests', toolKind: 'execute', status: 'failed',
        content: [{ type: 'text', text: "The user doesn't want to proceed with this tool use." }] }),
      ev({ kind: 'turn', stopReason: 'cancelled' }),
    ]);

    // A run of one is shown as itself (SESS1): folding it would save nothing.
    const card = screen.getByRole('button', { name: /Run the tests/ });
    expect(within(card).getByText('stopped')).toBeTruthy();
    expect(within(card).queryByText('failed')).toBeNull();
    expect(screen.queryByText(/doesn't want to proceed/)).toBeNull();

    await userEvent.click(card);
    expect(screen.getByText(/doesn't want to proceed/)).toBeTruthy();
  });
});
