import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import { ConversationView } from './ConversationView';
import { CHANNEL_CLAIM_OPENING } from '../channel';
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

  /** WAITSHOWN1: a turn asked and not answered says it waits for its first answer, since when and for how long. */
  it('says a running turn is waiting for its first answer, until the agent does anything', () => {
    const asked = ev({ kind: 'user', origin: 'person', text: 'go on', at: new Date(Date.now() - 7 * 60_000).toISOString() });
    const { unmount } = view([asked], { live: true });
    const line = screen.getByText(/waiting for its first answer since .+ \(7m\)/);
    expect(line).toBeTruthy();
    expect(screen.getByText('working…')).toBeTruthy();
    unmount();

    view([asked, ev({ kind: 'message', text: 'here' })], { live: true });
    expect(screen.queryByText(/waiting for its first answer/)).toBeNull();
  });

  /** WAITSAY1: a call that has run for minutes is named, with how long. */
  it('says a live turn is running a call, and for how long', () => {
    const begun = new Date(Date.now() - 7 * 60_000).toISOString();
    const events = [
      ev({ kind: 'user', origin: 'person', text: 'test it' }),
      ev({ kind: 'tool', id: 'k1', title: 'npm test', toolKind: 'execute', status: 'in_progress', at: begun }),
    ];
    const { unmount } = view(events, { live: true });
    expect(screen.getByText('running npm test for 7m')).toBeTruthy();
    unmount();

    view([...events, ev({ kind: 'tool', id: 'k1', status: 'completed' })], { live: true });
    expect(screen.queryByText(/running npm test for/)).toBeNull();
  });

  /** AUTONOMY1b3e (D184): a call held for the person's answer says it waits, as the rail and the head do; else it runs. */
  it.each([
    ['en', 'waiting for your answer to npm test for 3m', 'waiting for your answer to a tool call for 3m', 'running npm test for 7m', /waiting for your answer/],
    ['zh', 'npm test 等你答复，已 3 分钟', '一个工具调用等你答复，已 3 分钟', '正在运行 npm test（已 7 分钟）', /等你答复/],
  ])('says a held call waits for the answer (%s)', async (lng, named, unnamed, running, waits) => {
    await i18n.changeLanguage(lng);
    try {
      const begun = new Date(Date.now() - 7 * 60_000).toISOString();
      const held = new Date(Date.now() - 3 * 60_000).toISOString();
      const events = [
        ev({ kind: 'user', origin: 'person', text: 'test it' }),
        ev({ kind: 'tool', id: 'k1', title: 'npm test', toolKind: 'execute', status: 'in_progress', at: begun }),
      ];
      const first = view(events, { live: true, callMark: { title: 'npm test', since: held, waiting: true } });
      expect(screen.getByText(named)).toBeTruthy();
      first.unmount();

      // The agent named nothing: the driver's mark still says it waits, as a tool call.
      const second = view([events[0]!], { live: true, callMark: { title: '', since: held, waiting: true } });
      expect(screen.getByText(unnamed)).toBeTruthy();
      second.unmount();

      // The mark gone, or an older host's none: running, as before.
      for (const callMark of [undefined, { title: 'npm test', since: held }, { title: 'npm test', since: held, waiting: false }]) {
        const again = view(events, { live: true, callMark });
        expect(screen.getByText(running)).toBeTruthy();
        expect(screen.queryByText(waits)).toBeNull();
        again.unmount();
      }
    } finally {
      await i18n.changeLanguage('en');
    }
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

  /**
   * SESSONE2c (D167 point 7): another session's message handed to a running session is Daoris's words around that agent's claim,
   * which the record keeps as the driver's, never the person's. It is a claim and not a target, so it is named as one and shown
   * whole, in either language.
   */
  it('names another session’s claim as that, not as the target, and shows it whole', async () => {
    const text = `${CHANNEL_CLAIM_OPENING} (repository db, claude-code, 09:02):\n\n> Which column holds the figure?\n\nThis is that agent's claim, not the person's words.`;
    const { unmount } = view([ev({ kind: 'user', origin: 'target', text })]);

    expect(screen.getByText('Another session’s claim, in Daoris’s words')).toBeTruthy();
    expect(screen.queryByText('The target Daoris composed')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Show all' })).toBeNull();
    expect(screen.getByText(/Which column holds the figure\?/)).toBeTruthy();
    unmount();

    await i18n.changeLanguage('zh');
    view([ev({ kind: 'user', origin: 'target', text })]);
    expect(screen.getByText('另一个会话的说法，附 Daoris 的说明')).toBeTruthy();
    await i18n.changeLanguage('en');
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

  /**
   * ASKHIST1b: the person's words, as asked and as held for the next turn, and the agent's answer that says them again,
   * break a word wider than the column inside it. A pasted URL is one word, and it ran past Ask Daoris's dock, which then
   * scrolled sideways. The answer breaks only a word that would overflow (`break-word`), so a table keeps its columns and
   * scrolls in its own box as before.
   */
  it('breaks a word wider than its column at the column’s edge, the person’s and the agent’s', () => {
    const url = 'https://example.atlassian.net/browse/TK-2205?focusedCommentId=1234567&page=com.example.plugin.tabpanels%3Acomments';
    const { container } = view([
      ev({ kind: 'user', origin: 'person', text: `to complete this ${url}` }),
      ev({ kind: 'message', text: `On it: ${url} is open.` }),
      ev({ kind: 'turn', stopReason: 'end_turn' }),
      ev({ kind: 'user', origin: 'person', id: 'w1', reaches: 'resume', text: `and ${url}#comment-1` }),
    ]);

    expect(screen.getByRole('region', { name: 'conversation' })).toHaveClass('grid-cols-[minmax(0,1fr)]');
    for (const words of [`to complete this ${url}`, `and ${url}#comment-1`]) {
      expect(screen.getByText(words)).toHaveClass('whitespace-pre-wrap', 'wrap-anywhere');
    }
    const answer = container.querySelector('.markdown')!;
    expect(answer.textContent).toContain(url);
    expect(answer).toHaveClass('wrap-break-word');
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

  /**
   * XAGENT1g (the second-agent design §6.3): another agent's findings are a block from that agent, open, never the person's
   * words and never a composed target with what it was composed of.
   */
  it('shows another agent’s findings as their own block, open, never as the person’s', () => {
    const words = 'Another agent, Codex by OpenAI, read your work at 4f9c2a7e and claims what follows.';
    render(
      <Tooltip.Provider>
        <ConversationView turns={toTurns([ev({ kind: 'user', origin: 'target', opinion: 'o1', text: words })]).turns} />
      </Tooltip.Provider>,
    );

    expect(screen.getByText('Another agent\'s findings')).toBeTruthy();
    expect(screen.getByText(words)).toBeTruthy();
    expect(screen.queryByText('you')).toBeNull();
    expect(screen.queryByText('The target Daoris composed')).toBeNull();
    expect(screen.getByRole('button', { name: 'Fold' })).toHaveAttribute('aria-expanded', 'true');
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
    expect(screen.getByText('Held: it reads this at its next step, once the step in flight ends.')).toBeTruthy();
    expect(screen.getByText('Held: it reads this when its turn ends.')).toBeTruthy();
    expect(screen.getByText('level.json')).toBeTruthy();
    unmount();

    render(<Tooltip.Provider><ConversationView turns={settle(toTurns(working).turns, false)} /></Tooltip.Provider>);
    expect(screen.getAllByText('It ended before reading this.')).toHaveLength(2);
    expect(screen.queryByText(/Held:/)).toBeNull();
  });

  /**
   * STEER2b (D136): words *Send now* put into the running turn show where they were taken, saying so, never as waiting; and
   * a call the steer cut reads as cut, in the quiet tone, even after the session ended.
   */
  it('shows words sent now where they were taken, and a call the steer cut as cut', () => {
    const going = [
      ev({ kind: 'user', origin: 'target', text: 'go' }),
      ev({ kind: 'tool', id: 'h1', title: 'npm run gates', toolKind: 'execute', status: 'in_progress' }),
      ev({ kind: 'user', origin: 'person', id: 'said-1', reaches: 'now', text: 'use the blue one' }),
    ];
    const { unmount } = view(going);

    expect(screen.getByText('use the blue one')).toBeTruthy();
    expect(screen.getByText('Sent now: it took this at once, in the turn it was on.')).toBeTruthy();
    expect(screen.queryByText(/Held:/)).toBeNull();
    unmount();

    // Taken as the ask of the turn that goes on, with the cut call closed in the turn it began in.
    render(<Tooltip.Provider><ConversationView turns={toTurns([
      ...going,
      ev({ kind: 'user', origin: 'person', id: 'said-1', text: 'use the blue one' }),
      ev({ kind: 'tool', id: 'h1', status: 'cut' }),
    ]).turns} /></Tooltip.Provider>);
    expect(screen.queryByText('Sent now: it took this at once, in the turn it was on.')).toBeNull();
    expect(screen.getAllByText('use the blue one')).toHaveLength(1);
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

  /** MIXAGENT1b-i: a conversation's last line, where it went on with another agent, presses through to the new conversation. */
  it('gives the handed-over line a press to the conversation it went on in, where one can be opened', async () => {
    const onSession = vi.fn();
    const handed = ev({
      kind: 'note', text: 'The conversation went on with codex-acp, in conversation ab12cd34.',
      parts: [{ code: 'chat.handed-over', values: { agent: 'codex-acp', session: 'ab12cd34' }, text: 'The conversation went on with codex-acp, in conversation ab12cd34.' }],
    });
    const { unmount } = view([ev({ kind: 'user', origin: 'person', text: 'go' }), handed], { onSession });

    await userEvent.click(screen.getByRole('button', { name: 'Open conversation ab12cd34' }));
    expect(onSession).toHaveBeenCalledWith('ab12cd34');
    unmount();

    view([ev({ kind: 'user', origin: 'person', text: 'go' }), handed]);
    expect(screen.queryByRole('button', { name: /Open conversation/ })).toBeNull();
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

    // A reason that names an agent the page was not told, or a code it does not know: the driver's words stand, their
    // backticks drawn as code (UX7d-1).
    const { container } = view([
      ev({ kind: 'user', origin: 'person', id: 'w1', reaches: 'resume', text: 'also cap it' }),
      ev({ kind: 'note', text: '— your words went to session `n3wn3w00`, because it ran on `a`, and starts here now run on `b`.', words: ['w1'], to: 'n3wn3w00', why: 'adapter' }),
    ]);
    expect(container.textContent).toContain('— your words went to session n3wn3w00, because it ran on a, and starts here now run on b.');
    expect(within(container).getByText('n3wn3w00').tagName).toBe('CODE');
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

  /**
   * CONVNOTE1 (D125's SIGNIN1b note): a conversation its agent refused for its sign-in carries the driver's note with its
   * parts, and the page words it from them in the reader's language, as a session record's note is (LANG1b). The driver's
   * English is never shown where the page could word it, and a part the page cannot word shows as recorded, marked.
   */
  it('words a driver’s note from its parts in the reader’s language: a refused sign-in', async () => {
    const english = 'The agent refused the claude-code account it ran on for its sign-in.';
    const refused = () => [
      ev({ kind: 'user', origin: 'person', text: 'hello' }),
      ev({ kind: 'note', text: english, parts: [{ code: 'account.signed-out', values: { owner: 'claude-code' }, text: english }] }),
      ev({ kind: 'turn', stopReason: 'refusal' }),
    ];
    const { unmount } = view(refused());
    expect(screen.getByText(/^The agent refused the claude-code account it ran on for its sign-in, so it reads signed out/))
      .toBeTruthy();
    expect(screen.queryByText(english)).toBeNull();
    expect(screen.getByText('Driver')).toBeTruthy();
    unmount();

    await i18n.changeLanguage('zh');
    try {
      const { unmount: shown } = view(refused());
      expect(screen.getByText(/^智能体以登录为由拒绝了它运行时用的 claude-code 账户/)).toBeTruthy();
      expect(screen.queryByText(/refused/)).toBeNull();
      shown();
    } finally {
      await i18n.changeLanguage('en');
    }

    // A code this page does not know: the part's own English, marked as recorded.
    view([ev({ kind: 'note', text: 'a newer line.', parts: [{ code: 'account.newer', text: 'A newer line.' }] })]);
    expect(screen.getByText('A newer line.')).toBeTruthy();
    expect(screen.getByText('shown as recorded')).toBeTruthy();
  });

  /**
   * UX7d-1 (D152's UX7d-1 note; the UX7 design §1.3): the line a conversation opens with, naming the account it opened on and
   * why, is worded from its codes under 驱动 in 中文, its account drawn as code; and a driver's note with no parts, the English
   * an older driver wrote, draws its backticks as code too, never as backticks.
   */
  it('words the opening line by its codes in the reader’s language, and draws a part-less note’s backticks as code', async () => {
    const english = 'opened on `account-1`: Daoris started on it least recently. No account has said what it has left yet.';
    const opening = () => [
      ev({
        kind: 'note', text: english, parts: [
          { code: 'opening.opened', values: { account: 'account-1', why: 'least-recent' }, text: 'opened on `account-1`: Daoris started on it least recently.' },
          { code: 'opening.unsaid', values: {}, text: 'No account has said what it has left yet.' },
        ],
      }),
      ev({ kind: 'user', origin: 'person', text: 'hello' }),
    ];
    await i18n.changeLanguage('zh');
    try {
      const { container, unmount } = view(opening());
      expect(screen.getByText('驱动')).toBeTruthy();
      expect(container.textContent).toContain('在账户 account-1 上开始：Daoris 最久没有在它上面启动。尚无账户说明它还剩多少用量。');
      expect(within(container).getByText('account-1').tagName).toBe('CODE');
      expect(container.textContent).not.toMatch(/opened on|`/);
      expect(screen.queryByText('按原文显示')).toBeNull();
      unmount();
    } finally {
      await i18n.changeLanguage('en');
    }

    const { container } = view([ev({ kind: 'note', text: "opened on `account-2`: it comes first in `work`'s list." })]);
    expect(container.textContent).toContain("opened on account-2: it comes first in work's list.");
    expect(container.textContent).not.toContain('`');
    expect(within(container).getByText('work').tagName).toBe('CODE');
  });

  /** NOTENAME1: the opening line says an account by the name the roster gives it, and an id the roster lacks as it is. */
  it('names the account in the opening line by the name the person gave it', () => {
    const { container } = view([
      ev({
        kind: 'note', text: 'opened on `account-1`: …', parts: [
          { code: 'opening.opened', values: { account: 'account-1', why: 'near', over: 'account-9' }, text: 'opened on `account-1`: …' },
        ],
      }),
      ev({ kind: 'user', origin: 'person', text: 'hello' }),
    ], { names: new Map([['account-1', 'personal']]) });
    expect(container.textContent).toContain('Opened on personal:');
    expect(container.textContent).toContain('account-9');
    expect(container.textContent).not.toContain('account-1');
  });

  /**
   * MSG1g2 (D137 §2.2, MSG1g's note): words a resume holds while the account its record ran on cools say so, naming its
   * reset, and offer *Go on in a new session*, saying the new session starts without this conversation's context. What the
   * press came to is said in the page's words; where nothing carries the words on by itself, the door is a conversation.
   */
  describe('words a cooling account holds', () => {
    const until = '2026-10-04T13:10:00Z';
    const COOLING = () => [
      ev({ kind: 'user', origin: 'target', text: 'go' }),
      ev({ kind: 'message', text: 'Done, committed.' }),
      ev({ kind: 'turn', stopReason: 'end_turn' }),
      ev({ kind: 'user', origin: 'person', id: 'w1', reaches: 'resume', text: 'first' }),
      ev({ kind: 'user', origin: 'person', id: 'w2', reaches: 'resume', text: 'second' }),
    ];
    const ended = (cooling: Parameters<typeof ConversationView>[0]['cooling'], more: Partial<Parameters<typeof ConversationView>[0]> = {}) =>
      render(<Tooltip.Provider><ConversationView turns={settle(toTurns(COOLING()).turns, false)} cooling={cooling} {...more} /></Tooltip.Provider>);

    it('says the words wait for the account’s reset, and offers a new session without this conversation', async () => {
      const onGoOnNew = vi.fn();
      ended({ until, onGoOnNew });

      const held = screen.getAllByText(/^Held: its account is cooling until .+; it goes on with this then\.$/);
      expect(held).toHaveLength(2);
      expect(held[0]!.textContent).toContain(new Date(until).toLocaleString('en', { month: 'short', day: 'numeric' }));
      expect(screen.queryByText("Held: the same session goes on with this at the driver's next look.")).toBeNull();
      expect(screen.getByText("A new session can take your words now, without this conversation's context.")).toBeTruthy();

      await userEvent.click(screen.getByRole('button', { name: 'Go on in a new session' }));
      expect(onGoOnNew).toHaveBeenCalledTimes(1);
    });

    it('holds the press while it is on its way, and says the held line alone where nothing offers one', () => {
      const { unmount } = ended({ until, onGoOnNew: vi.fn(), pending: true });
      expect(screen.getByRole('button', { name: 'Go on in a new session' })).toBeDisabled();
      unmount();

      ended({ until });
      expect(screen.getAllByText(/^Held: its account is cooling until/)).toHaveLength(2);
      expect(screen.queryByRole('button', { name: 'Go on in a new session' })).toBeNull();
    });

    it('says the choice is kept, and offers the press no more', () => {
      ended({ until, onGoOnNew: vi.fn(), answer: { sent: true, why: null, message: 'it goes on in a new session' } });
      expect(screen.getByText("A new session takes your words at the driver's next look, without this conversation's context.")).toBeTruthy();
      expect(screen.queryByRole('button', { name: 'Go on in a new session' })).toBeNull();
      expect(screen.queryByText("A new session can take your words now, without this conversation's context.")).toBeNull();
    });

    it('points a closed quest’s words at a conversation, with every word waiting', async () => {
      const onStartFrom = vi.fn();
      ended({ until, onGoOnNew: vi.fn(), quest: 'abc123', answer: { sent: false, why: 'closed', message: '#abc123 has closed…' } }, { onStartFrom });

      expect(screen.getByText('#abc123 has closed, so nothing carries its words on by itself; start a conversation with them instead.')).toBeTruthy();
      expect(screen.queryByRole('button', { name: 'Go on in a new session' })).toBeNull();
      await userEvent.click(screen.getByRole('button', { name: 'Start a conversation with these words' }));
      expect(onStartFrom).toHaveBeenCalledWith(['first', 'second']);
    });

    it('says a refusal the press cannot get past, offering no conversation, and the press again for a choice not kept', () => {
      const { unmount } = ended({ until, onGoOnNew: vi.fn(), answer: { sent: false, why: 'not-cooling', message: 'not cooling' } }, { onStartFrom: vi.fn() });
      expect(screen.getByText("Its account is not cooling any more, so the same session goes on with your words at the driver's next look.")).toBeTruthy();
      expect(screen.queryByRole('button', { name: 'Start a conversation with these words' })).toBeNull();
      expect(screen.queryByRole('button', { name: 'Go on in a new session' })).toBeNull();
      unmount();

      ended({ until, onGoOnNew: vi.fn(), answer: { sent: false, why: null, message: 'your choice could not be kept: disk full' } });
      expect(screen.getByText('your choice could not be kept: disk full')).toBeTruthy();
      expect(screen.getByRole('button', { name: 'Go on in a new session' })).toBeTruthy();
    });

    it('says nothing of a cool-off on a chat, or once a driver’s note has settled the words', () => {
      const { unmount } = ended({ until, onGoOnNew: vi.fn() }, { chat: true });
      expect(screen.queryByText(/cooling/)).toBeNull();
      expect(screen.queryByRole('button', { name: 'Go on in a new session' })).toBeNull();
      unmount();

      render(
        <Tooltip.Provider>
          <ConversationView
            turns={settle(toTurns([
              ...COOLING(),
              ev({ kind: 'note', text: '— your words went to session `n3wn3w00`, because you chose a new session.', words: ['w1', 'w2'], to: 'n3wn3w00', why: 'account' }),
            ]).turns, false)}
            cooling={{ until, onGoOnNew: vi.fn() }}
          />
        </Tooltip.Provider>,
      );
      expect(screen.queryByText(/cooling/)).toBeNull();
      expect(screen.queryByRole('button', { name: 'Go on in a new session' })).toBeNull();
    });

    it('says it in 中文', async () => {
      await i18n.changeLanguage('zh');
      try {
        ended({ until, onGoOnNew: vi.fn() });
        expect(screen.getAllByText(/^已暂存：它的账户冷却到 .+，届时会带着这条继续。$/)).toHaveLength(2);
        expect(screen.getByRole('button', { name: '在新会话中继续' })).toBeTruthy();
      } finally {
        await i18n.changeLanguage('en');
      }
    });
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

/**
 * WAITFOOT1c (D168 point 4): the block that raised a wait carries a line saying what waits from it, with *To the wait*; the owner,
 * 2026-10-15, found the waits "not really referencing where the wait is for".
 */
describe('the waits a block raised', () => {
  /**
   * WAITFOOT2b (D168's WAITFOOT2 note): a quiet mark, its words its name and its tip, since the owner found the waits drawn two
   * and three times at once, and a line of words under the block was one more of them.
   */
  it('marks the block that raised a wait, with a way to the wait', async () => {
    const question = ev({ kind: 'message', text: 'Which of the two should I keep?' });
    const onToWait = vi.fn();
    view([ev({ kind: 'user', origin: 'target', text: 'Your target…' }), question, ev({ kind: 'turn', stopReason: 'end_turn' })], {
      marks: { [`e${question.seq}`]: [{ kind: 'question', below: true }] }, onToWait,
    });

    const block = screen.getByText('Which of the two should I keep?').closest<HTMLElement>('[data-block]')!;
    // No line of words: the mark is a glyph, its words are what it is called and what its tip says.
    expect(within(block).queryByText(/Waiting on you from here/)).toBeNull();
    const mark = within(block).getByRole('button', { name: 'To the wait: its question' });
    expect(mark).not.toHaveTextContent(/\S/);
    await userEvent.hover(mark);
    expect((await screen.findAllByText('Waiting on you from here: its question')).length).toBeGreaterThan(0);
    await userEvent.click(mark);
    expect(onToWait).toHaveBeenCalledWith({ kind: 'question', below: true });
  });

  it('marks a call in a folded run on the run\'s line, and on the call once it is open', async () => {
    const asked = ev({ kind: 'tool', id: 'g1', title: 'go_ahead_ask', status: 'completed' });
    view([
      ev({ kind: 'user', origin: 'target', text: 'Your target…' }),
      ev({ kind: 'tool', id: 'r1', title: 'Read the dashboard', status: 'completed' }),
      asked,
      ev({ kind: 'message', text: 'Waiting on go-ahead #3.' }),
      ev({ kind: 'turn', stopReason: 'end_turn' }),
    ], { marks: { [`e${asked.seq}`]: [{ kind: 'goAhead', number: 3 }] }, onToWait: vi.fn() });

    expect(screen.queryByText('go_ahead_ask')).toBeNull();
    expect(screen.getAllByRole('button', { name: 'To the wait: go-ahead #3' })).toHaveLength(1);
    await userEvent.click(screen.getByRole('button', { name: /2 tool calls/ }));
    // Open, the mark is the call's own, said once.
    const block = screen.getByText('go_ahead_ask').closest<HTMLElement>('[data-block]')!;
    expect(within(block).getByRole('button', { name: 'To the wait: go-ahead #3' })).toBeTruthy();
    expect(screen.getAllByRole('button', { name: 'To the wait: go-ahead #3' })).toHaveLength(1);
  });

  it('marks the end of the turn that did the work, which a reveal outlines', () => {
    const asked = ev({ kind: 'user', origin: 'target', text: 'Your target…' });
    view([asked, ev({ kind: 'message', text: 'Done: two commits.' }), ev({ kind: 'turn', stopReason: 'end_turn' })], {
      marks: { [`end-e${asked.seq}`]: [{ kind: 'accept' }] }, onToWait: vi.fn(), reveal: `end-e${asked.seq}`,
    });

    const end = screen.getByRole('button', { name: 'To the wait: its work to accept' }).closest<HTMLElement>('[data-block]')!;
    expect(end.dataset.block).toBe(`end-e${asked.seq}`);
    expect(end.className).toContain('outline-accent');
  });

  it('draws no mark where nothing leads to the wait', () => {
    const question = ev({ kind: 'message', text: 'Which one?' });
    view([question], { marks: { [`e${question.seq}`]: [{ kind: 'question', below: true }] } });
    expect(screen.queryByRole('button', { name: /To the wait/ })).toBeNull();
  });

  it('says its mark in 中文', async () => {
    await i18n.changeLanguage('zh');
    try {
      const question = ev({ kind: 'message', text: '两个留哪一个？' });
      view([question], { marks: { [`e${question.seq}`]: [{ kind: 'goAhead', number: 2 }] }, onToWait: vi.fn() });
      expect(screen.getByRole('button', { name: '到等待处：放行 #2' })).toBeTruthy();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});
