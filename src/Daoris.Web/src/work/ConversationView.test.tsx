import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { ConversationView } from './ConversationView';
import { type SessionEvent, settle, toTurns } from './conversation';

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

    const first = screen.getByText('4.8s · 67.8K in · 80 out');
    expect(screen.getByText('1s')).toBeTruthy();
    // The running turn has said nothing about itself yet.
    expect(screen.queryByText(/on it.*·/)).toBeNull();

    await userEvent.hover(first);
    expect((await screen.findAllByText(/the first answer came after 1\.5s/))[0]).toBeTruthy();
    expect(screen.getAllByText(/4 new, 51,061 read from its cache, 16,717 written to it/)[0]).toBeTruthy();
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

    expect(screen.getByText('the target Daoris composed')).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'show all' }));
    expect(screen.getByRole('button', { name: 'fold' })).toBeTruthy();
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
  // UX5 U16 (the owner): a maximized window showed the conversation at half its width.
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

    const target = screen.getByText('the target Daoris composed');
    const earlier = screen.getByRole('button', { name: 'load earlier' });
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

  /** SESS1 S6: a record from before the refusal named its call carries the request's JSON; two lines, the rest on a press. */
  it('shows a long note of the driver\'s as two lines, and the rest on a press', async () => {
    const long = `permission refused: {"toolCallId":"toolu_01","rawInput":{"command":"${'x'.repeat(300)}"}} — the repository's own configuration governs`;
    view([ev({ kind: 'note', text: long })]);

    const show = screen.getByRole('button', { name: 'show all' });
    expect(show).toHaveAttribute('aria-expanded', 'false');
    await userEvent.click(show);
    expect(show).toHaveAttribute('aria-expanded', 'true');
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
