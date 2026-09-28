import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import i18n from '../i18n';
import type { Quest, Session } from '../api';
import { SessionHead } from './SessionHead';

// Props-only, like every molecule here: a parked session with its analysis, a record read over a
// remote with no tree and no account, and an ended one are all reached by passing them.

const NOW = new Date('2026-09-21T12:00:00Z');

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4',
  quest: '7a82cc',
  repository: 'engine',
  adapter: 'claude-code',
  state: 'working',
  kind: 'driven',
  harnessVersion: '2.1.4',
  profile: 'owner',
  created: '2026-09-21T09:46:00Z',
  updated: '2026-09-21T11:56:00Z',
  ...over,
});

/** An intake parked asking the person (INT4b): a chat in the ask's name, in Daoris's own room. */
const INTAKE: Partial<Session> = {
  id: 'i9n8t7k6',
  quest: null,
  kind: 'chat',
  repository: 'ask #0fda18',
  ask: '0fda18',
  state: 'awaiting-person',
  tree: 'C:/somewhere/data/intake/default',
};

const quest = (over: Partial<Quest> = {}): Quest => ({
  id: '7a82cc',
  from: 'platform',
  to: 'engine',
  title: 'Expose a streaming budget on the chunk API',
  body: 'World streaming needs a per-frame cap.',
  status: 'Taken',
  filed: '2026-09-14T09:00:00Z',
  updated: '2026-09-21T09:00:00Z',
  ...over,
});

describe('the attended session\'s head', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
  });
  afterEach(() => vi.useRealTimers());

  /** REV3: a half-written decline reason carried into the next parked session, ready to decline it. */
  it('starts each parked session\'s decline empty — a reason written for one never carries to another', () => {
    const parked: Partial<Session> = { state: 'awaiting-person', note: 'needs a person' };
    const { rerender } = render(<SessionHead session={session(parked)} onResolve={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: 'decline…' }));
    fireEvent.change(screen.getByLabelText(/the reason/), { target: { value: 'the chunk API is being replaced' } });

    rerender(<SessionHead session={session({ ...parked, id: 's9f8e7d6' })} onResolve={vi.fn()} />);
    expect(screen.queryByLabelText(/the reason/)).toBeNull();
    expect(screen.getByRole('button', { name: 'decline…' })).toBeInTheDocument();
  });

  /** SESS1 S10: what its own tree left — the branch, and whether its work is on a branch of the person's. */
  it('says what its own tree left: the branch, and whether its work landed', () => {
    const { rerender } = render(<SessionHead session={session({ state: 'completed' })} branch={{
      repository: 'engine', workspace: 'default', branch: 'daoris/s-a900f1ad', hasTree: true,
      kind: 'unlanded', commits: 3, removable: false,
    }} />);
    expect(screen.getByText('daoris/s-a900f1ad')).toBeInTheDocument();
    expect(screen.getByText('3 commits no branch of yours holds')).toBeInTheDocument();

    rerender(<SessionHead session={session({ state: 'completed' })} branch={{
      repository: 'engine', workspace: 'default', branch: 'daoris/s-a900f1ad', hasTree: true,
      kind: 'landed', commits: 3, where: 'feature/0fda18-fix', removable: true,
    }} />);
    expect(screen.getByText('landed on feature/0fda18-fix')).toBeInTheDocument();

    rerender(<SessionHead session={session({ state: 'completed' })} />);
    expect(screen.queryByText('its work')).toBeNull();
  });

  it('is the record: identity, state, and what the session ran on and as', () => {
    render(<SessionHead session={session()} quest={quest()} />);

    expect(screen.getByRole('heading', { name: 'Expose a streaming budget on the chunk API' }))
      .toBeInTheDocument();
    expect(screen.getByText('working')).toBeInTheDocument();
    expect(screen.getByText('engine')).toBeInTheDocument();
    expect(screen.getByText('claude-code · 2.1.4 · as owner')).toBeInTheDocument();
    expect(screen.getByText('#7a82cc')).toBeInTheDocument();
  });

  /**
   * The state stays beside the title it names, however wide the head is (platform language §4:
   * status leads). The head follows the centre's width now (UX5 U16), and pushed to the far edge the
   * pill sat a thousand pixels from its title on a wide window, which is what U10 had capped around.
   */
  /** UX5 U17: the head says a live chat between turns is idle, as the rail does. */
  it('says a live chat between turns is idle', () => {
    const { rerender } = render(<SessionHead session={session({ kind: 'chat' })} taking={false} />);
    expect(screen.getByText('idle')).toBeInTheDocument();

    rerender(<SessionHead session={session({ kind: 'chat' })} taking />);
    expect(screen.getByText('working')).toBeInTheDocument();
  });

  it('keeps the state beside the title rather than at the far edge', () => {
    render(<SessionHead session={session()} quest={quest()} />);

    const row = screen.getByRole('heading', { level: 2 }).parentElement!;
    expect(row).toContainElement(screen.getByText('working'));
    expect(row.className).not.toContain('justify-between');
  });

  it('measures a running session to now and a finished one to where it ended', () => {
    const { unmount } = render(<SessionHead session={session()} />);
    expect(screen.getByText('running')).toBeInTheDocument();
    expect(screen.getByText('2h 14m')).toBeInTheDocument();
    unmount();

    render(<SessionHead session={session({ state: 'completed' })} />);
    expect(screen.getByText('ran for')).toBeInTheDocument();
  });

  /**
   * The design puts the FULL path here — the rail has no room for one and this does (D51 §9 makes
   * it machine-local, which is a guard at the door, not a reason for the machine's own surface to
   * hide it from the person who owns the checkout).
   */
  it('shows the whole tree path, breakable at its separators rather than overflowing', () => {
    const tree = 'C:/somewhere/.daoris/trees/default/engine/streaming-budget';
    render(<SessionHead session={session({ tree })} />);

    // Whole, and broken after a separator before inside a name (UX5 U8: `family\g` / `ame`).
    const path = screen.getByText((_, element) => element?.tagName === 'SPAN' && element.textContent === tree);
    expect(path.className).toContain('wrap-anywhere');
    expect(path.querySelectorAll('wbr')).toHaveLength(6);
  });

  /**
   * An absence means something real on this record and a dash would read as a bug in all of them:
   * no tree is the registered root, no profile is the harness's own configuration home, and a
   * browser over a keyed remote is told neither.
   */
  it('omits what it was not told rather than rendering a blank', () => {
    render(<SessionHead session={session({ tree: null, profile: null, quest: null })} />);

    expect(screen.queryByText('tree')).not.toBeInTheDocument();
    expect(screen.queryByText('quest')).not.toBeInTheDocument();
    expect(screen.getByText('claude-code · 2.1.4')).toBeInTheDocument();
  });

  it('names the machine when the record came from another one', () => {
    render(<SessionHead session={session({ id: 'person@machine-a/s1a2b3c4' })} />);

    expect(screen.getByText('machine')).toBeInTheDocument();
    expect(screen.getByText('person@machine-a')).toBeInTheDocument();
  });

  /**
   * `AwaitingPerson` has meant "only the person can clear this" since D46 and has never been
   * rendered anywhere. Its analysis belongs at the TOP of the head (design §4) — above the record,
   * because it is the reason the person is looking.
   */
  it('puts a parked session\'s analysis above its record, verbatim', () => {
    const analysis = 'Two ways forward.\n1. Cap in the scheduler.\n2. Cap in the chunk API.\nI recommend the second: it keeps the budget where the API already is.';
    render(<SessionHead session={session({ state: 'awaiting-person', note: analysis })} />);

    const waiting = screen.getByText('This one is waiting on you');
    expect(screen.getByText(/I recommend the second/)).toBeInTheDocument();
    // Above the record: the analysis precedes the repository in document order.
    expect(waiting.compareDocumentPosition(screen.getByText('engine')))
      .toBe(Node.DOCUMENT_POSITION_FOLLOWING);
  });

  /**
   * The timeline below carries the driver's note WITH the time it was observed, which is strictly
   * more than a bare sentence here — and one screen saying the same thing twice teaches a reader
   * to skim both. A park is the exception: there the analysis is the reason the person is looking.
   */
  it('leaves a driver observation to the timeline rather than repeating it', () => {
    render(<SessionHead session={session({ state: 'failed', note: 'the process exited 1' })} />);

    expect(screen.queryByText('This one is waiting on you')).not.toBeInTheDocument();
    expect(screen.queryByText('the process exited 1')).not.toBeInTheDocument();
  });

  /**
   * INT4g: an intake serves an ask and runs in Daoris's own room, which is not a repository's tree
   * (INT4b). Its record says so rather than reading `repository: ask #…`.
   */
  it('names an intake\'s ask and room rather than a repository and a tree', () => {
    render(<SessionHead session={session({ ...INTAKE, state: 'working' })} />);

    expect(screen.getByRole('heading', { name: 'intake for ask #0fda18' })).toBeInTheDocument();
    expect(screen.getByText('ask')).toBeInTheDocument();
    expect(screen.getByText('#0fda18')).toBeInTheDocument();
    expect(screen.getByText('room')).toBeInTheDocument();
    expect(screen.queryByText('repository')).toBeNull();
    expect(screen.queryByText('tree')).toBeNull();
  });

  /**
   * INT4g: a parked intake's answer is on its ask. Finish, decline and stop each ended the record
   * without answering the ask, which then fell back to a proposal — so the head leads to the ask.
   */
  it('gives a parked intake its ask as the answer, not a parked session\'s three moves', () => {
    const onAnswerAsk = vi.fn();
    render(
      <SessionHead
        session={session({ ...INTAKE, note: 'published nothing: it asks you rather than guess.' })}
        onResolve={() => {}}
        onAnswerAsk={onAnswerAsk}
      />,
    );

    expect(screen.getByText('published nothing: it asks you rather than guess.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'finish it' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'decline…' })).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'answer ask #0fda18' }));
    expect(onAnswerAsk).toHaveBeenCalledWith('0fda18');
  });

  it('speaks the active catalog', async () => {
    await i18n.changeLanguage('zh');
    render(<SessionHead session={session()} quest={quest()} />);

    expect(screen.getByText('仓库')).toBeInTheDocument();
    expect(screen.getByText('工作中')).toBeInTheDocument();
    await i18n.changeLanguage('en');
  });
});
