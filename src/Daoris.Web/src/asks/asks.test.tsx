import { type ReactElement, useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { NO_CARRY } from '../compose/carry';
import { AskCard } from './AskCard';
import { AskComposer, type AskDraft } from './AskComposer';
import { AskRecord } from './AskRecord';
import { asksInOrder } from './AsksSection';
import {
  BY_INTAKE, CLOSED, INTAKE_ASKED, INTAKE_PARKED, INTAKE_SESSION, NAMED, PROPOSED, PUBLISHED, REFUSED, UNKNOWN_TIER,
  UNMATCHED,
} from './fixtures';

// An ask's three molecules (INT4c), props in and states out: no service and no shell, so every state
// the record can be in is an ordinary assertion (components §2).

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const record = (ask = PROPOSED, over: Partial<Parameters<typeof AskRecord>[0]> = {}) => {
  const props = {
    ask, receivers: ['engine', 'game', 'lantern'], questTitles: {},
    onPublish: vi.fn(), onClose: vi.fn(), onOpenQuest: vi.fn(), onDismiss: vi.fn(), ...over,
  };
  render(<AskRecord {...props} />);
  return { ...props, drawer: screen.getByRole('dialog') };
};

describe('AskCard', () => {
  it('leads with its state and its first line, and says what answered and what it proposed', () => {
    render(<AskCard ask={PROPOSED} onOpen={() => {}} />);

    expect(screen.getByText('proposed')).toBeInTheDocument();
    expect(screen.getByText('The chunk streamer stalls on a cold cache — cap its hydration per frame.')).toBeInTheDocument();
    expect(screen.getByText(/by declarations/)).toBeInTheDocument();
    expect(screen.getByText(/proposed engine, game/)).toBeInTheDocument();
  });

  /**
   * 🔴 Seen on the window (POLISH4): a bare `default` beside `game → engine` reads as one more
   * repository — the band's own rule (INT4d), which the card had not followed.
   */
  it('names its place as a workspace, never as a bare name', () => {
    render(<AskCard ask={PROPOSED} onOpen={() => {}} />);
    expect(screen.getByText('workspace aurora')).toBeInTheDocument();
  });

  /**
   * 🔴 Seen on the window (POLISH4): five cards read "proposed" alike while one had an intake reading
   * it and one had its intake waiting on the person — which the band above said and the card did not.
   */
  it('says when an intake is reading it, and when its intake asked the person', () => {
    const { rerender } = render(<AskCard ask={INTAKE_ASKED} intake="working" onOpen={() => {}} />);
    expect(screen.getByText(/an intake is reading it/)).toBeInTheDocument();

    rerender(<Tooltip.Provider><AskCard ask={INTAKE_ASKED} intake="awaiting-person" onOpen={() => {}} /></Tooltip.Provider>);
    expect(screen.getByText('its intake asked you')).toBeInTheDocument();
    expect(screen.queryByText(/an intake is reading it/)).toBeNull();

    // An intake that ended without publishing leaves an ordinary proposal, and says nothing more.
    rerender(<Tooltip.Provider><AskCard ask={INTAKE_ASKED} intake="completed" onOpen={() => {}} /></Tooltip.Provider>);
    expect(screen.queryByText('its intake asked you')).toBeNull();
    expect(screen.queryByText(/an intake is reading it/)).toBeNull();
  });

  it('names the quests an ask became, and opens on a press or a key', () => {
    const onOpen = vi.fn();
    render(<AskCard ask={PUBLISHED} onOpen={onOpen} />);

    expect(screen.getByText(/became #9a8b7c, #1f2e3d/)).toBeInTheDocument();
    fireEvent.keyDown(screen.getByRole('button'), { key: 'Enter' });
    expect(onOpen).toHaveBeenCalledWith(PUBLISHED);
  });
});

describe('AskRecord', () => {
  /** The sentence the tier writes about itself is what a person reads: which tier answered, never implied. */
  it('says which tier answered, in the words for it, beside the ask\'s own words whole', () => {
    const { drawer } = record(PROPOSED);
    expect(within(drawer).getByText('by declarations only; no intake harness ran')).toBeInTheDocument();
    // The first line is the title, and the rest follows it — each once (POLISH4).
    expect(within(drawer).getAllByText(/The chunk streamer stalls on a cold cache/)).toHaveLength(1);
    expect(within(drawer).getByText(/Seen on the test rig after a fresh install/)).toBeInTheDocument();
  });

  /** 🔴 Seen on the window (POLISH4): "answered by" above "by declarations…" read "answered by by". */
  it('labels the tier so that no tier\'s words repeat the label', () => {
    const { drawer } = record(PROPOSED);
    expect(within(drawer).queryByText('answered by')).toBeNull();
    expect(within(drawer).getByText('answered')).toBeInTheDocument();
  });

  /** 🔴 Seen on the window (POLISH4): a one-line ask was its drawer's title and then its body. */
  it('does not repeat a one-line ask as its own body', () => {
    const { drawer } = record(UNMATCHED);
    expect(within(drawer).getAllByText('Tidy the release notes.')).toHaveLength(1);
  });

  it('names the workspace a receiver is chosen from as a workspace', () => {
    const { drawer } = record(UNMATCHED);
    expect(within(drawer).getByText('any repository in workspace aurora that can be asked')).toBeInTheDocument();
  });

  it('shows a tier it has no word for as the service wrote it', () => {
    const { drawer } = record(UNKNOWN_TIER);
    expect(within(drawer).getByText('intake-session')).toBeInTheDocument();
  });

  it('offers each proposal as a publish, and publishes to the one pressed', async () => {
    const { drawer, onPublish } = record(PROPOSED);

    expect(within(drawer).getByText(/chunk, stream, frame/)).toBeInTheDocument();
    await userEvent.click(within(drawer).getByRole('button', { name: 'publish to engine' }));

    expect(onPublish).toHaveBeenCalledWith('engine');
  });

  it('says when no declarations share its words, and still offers any receiver in its circle', async () => {
    const { drawer, onPublish } = record(UNMATCHED);

    expect(within(drawer).getByText(/No repository's declarations share its words/)).toBeInTheDocument();
    const user = userEvent.setup();
    within(drawer).getByRole('combobox', { name: 'publish to another' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('option', { name: 'lantern' }));
    await user.click(within(drawer).getByRole('button', { name: 'publish' }));

    expect(onPublish).toHaveBeenCalledWith('lantern');
  });

  it('lists the quests it became and opens the one chosen, by its title where the page holds it', async () => {
    const { drawer, onOpenQuest } = record(PUBLISHED, { questTitles: { '9a8b7c6d5e4f': 'Cap the hydration' } });

    await userEvent.click(within(drawer).getByRole('button', { name: /Cap the hydration/ }));
    expect(onOpenQuest).toHaveBeenCalledWith('9a8b7c6d5e4f');
  });

  it('names a quest the page does not hold yet, and offers no door that would open nothing', () => {
    // Just after a publish, the answer names the quest before the list has asked again for it.
    const { drawer } = record(PUBLISHED, { questTitles: { '9a8b7c6d5e4f': 'Cap the hydration' } });

    expect(within(drawer).getByText('#1f2e3d')).toBeInTheDocument();
    expect(within(drawer).queryByRole('button', { name: /#1f2e3d/ })).toBeNull();
  });

  it('carries the named receiver\'s refusal verbatim, and keeps its proposal to go on with', () => {
    const { drawer } = record(REFUSED);

    expect(within(drawer).getByText(REFUSED.note!)).toBeInTheDocument();
    expect(within(drawer).getByRole('button', { name: 'publish to engine' })).toBeInTheDocument();
  });

  it('closes with a reason, and not without one', async () => {
    const { drawer, onClose } = record(PROPOSED);

    await userEvent.click(within(drawer).getByRole('button', { name: 'close the ask' }));
    const confirm = within(drawer).getByRole('button', { name: 'close with this reason' });
    expect(confirm).toBeDisabled();
    await userEvent.type(within(drawer).getByLabelText('why — what became of it'), 'Answered elsewhere.');
    await userEvent.click(confirm);

    expect(onClose).toHaveBeenCalledWith('Answered elsewhere.');
  });

  /** A closed ask becomes nothing more (INT4a): its reason stays, and no verb is offered. */
  it('offers nothing to do on a closed ask, and says why it closed', () => {
    const { drawer } = record(CLOSED);

    expect(within(drawer).getByText('Answered in the design review instead.')).toBeInTheDocument();
    expect(within(drawer).queryByRole('button', { name: /publish/ })).toBeNull();
    expect(within(drawer).queryByRole('button', { name: 'close the ask' })).toBeNull();
  });

  it('names its files and never the path this machine keeps them at', () => {
    const { drawer } = record(PROPOSED);

    expect(within(drawer).getByText('trace.log')).toBeInTheDocument();
    expect(drawer.textContent).not.toContain('somewhere');
    expect(within(drawer).getByRole('link', { name: /tickets\.example/ })).toHaveAttribute('href', 'https://tickets.example/T-42');
  });

  it('says a named receiver answered, and offers publishing it further', () => {
    const { drawer } = record(NAMED);

    expect(within(drawer).getByText('the asker named the receiver')).toBeInTheDocument();
    expect(within(drawer).getByRole('combobox', { name: 'publish to another' })).toBeInTheDocument();
  });

  // ——— Who answered (INT4d): the tier INT4b added, and the intake session that served it.

  it('says an intake session answered, in the words for it', () => {
    const { drawer } = record(BY_INTAKE, { intake: INTAKE_SESSION });
    expect(within(drawer).getByText('an intake session read it and published it')).toBeInTheDocument();
  });

  /** On the desktop the session is a door into Sessions, where its transcript and console are. */
  it('names its intake session by state and tool, and opens it where Sessions exists', async () => {
    const onAttend = vi.fn();
    const { drawer } = record(INTAKE_ASKED, { intake: INTAKE_PARKED, onAttend });
    const line = within(drawer).getByRole('region', { name: 'intake session' });

    expect(within(line).getByText('awaiting person')).toBeInTheDocument();
    await userEvent.click(within(line).getByRole('button', { name: 'claude-code · 2.1.4' }));
    expect(onAttend).toHaveBeenCalledWith('i9n8t7k6a5b4');
  });

  /** A browser has no Sessions: the session is named, and nothing pretends to open it. */
  it('names its intake session without a door where Sessions does not exist', () => {
    const { drawer } = record(BY_INTAKE, { intake: INTAKE_SESSION });
    const line = within(drawer).getByRole('region', { name: 'intake session' });

    expect(within(line).getByText('claude-code · 2.1.4')).toBeInTheDocument();
    expect(within(line).getByText('completed')).toBeInTheDocument();
    expect(within(line).queryByRole('button')).toBeNull();
  });

  it('names an intake session the page has not loaded by its id, and offers no door that would open nothing', () => {
    const { drawer } = record(BY_INTAKE, { onAttend: vi.fn() });
    const line = within(drawer).getByRole('region', { name: 'intake session' });

    expect(within(line).getByText('#i9n8t7')).toBeInTheDocument();
    expect(within(line).queryByRole('button')).toBeNull();
  });

  /**
   * 🔴 Seen on the window: an intake that parks has published nothing, so the tier stays the
   * declarations' — and its words, "no intake harness ran", sat directly above the intake that ran.
   */
  it('never says no intake ran above the intake that did', () => {
    const { drawer } = record(INTAKE_ASKED, { intake: INTAKE_PARKED });
    expect(within(drawer).queryByText('by declarations only; no intake harness ran')).toBeNull();
    expect(within(drawer).getByText('by declarations; an intake read it and has not published')).toBeInTheDocument();
  });

  it('has no intake line for an ask no intake served', () => {
    const { drawer } = record(PROPOSED);
    expect(within(drawer).queryByRole('region', { name: 'intake session' })).toBeNull();
  });
});

describe('AskComposer', () => {
  function Holder({ fixed, circles, onSubmit = () => {}, intake }: {
    fixed: string | null; circles: string[]; onSubmit?: (draft: AskDraft) => void; intake?: boolean | null;
  }) {
    const [draft, setDraft] = useState<AskDraft>({ circle: '', sentence: '', to: '', ...NO_CARRY });
    return (
      <AskComposer
        draft={draft} onChange={setDraft} fixed={fixed} circles={circles} receivers={['engine', 'game']}
        onSubmit={() => onSubmit(draft)} onCancel={() => {}} intake={intake}
      />
    );
  }

  /**
   * 🔴 UX5 U34: the composer said *nothing is published until you name a receiver or accept a
   * proposal*, and on a machine with an intake agent the intake publishes on its own, asking the
   * person only when it cannot tell (intake design §1). What the composer promises follows the
   * machine: an intake set, none set, or a door that cannot know, which a browser is.
   */
  it('says what will become of an ask on this machine, and never promises what an intake would break', () => {
    const hint = () => screen.getByRole('dialog').textContent ?? '';

    const { unmount } = render(<Holder fixed="aurora" circles={['aurora']} intake />);
    expect(hint()).toMatch(/intake agent reads it and publishes/);
    expect(hint()).not.toMatch(/nothing is published/);
    unmount();

    const off = render(<Holder fixed="aurora" circles={['aurora']} intake={false} />);
    expect(hint()).toMatch(/nothing is published until you name a receiver/);
    off.unmount();

    render(<Holder fixed="aurora" circles={['aurora']} />);
    expect(hint()).not.toMatch(/nothing is published/);
    expect(hint()).toMatch(/Naming a receiver publishes it at once/);
    // The line under the title says only what is true in every case, and never the title again.
    expect(hint()).toMatch(/kept by the service until it becomes quests/);
  });

  it('is made in the circle the page is scoped to, and asks for its words before it will send', async () => {
    const onSubmit = vi.fn();
    render(<Holder fixed="aurora" circles={['aurora']} onSubmit={onSubmit} />);
    const drawer = screen.getByRole('dialog');

    expect(within(drawer).getByText('Asked in workspace aurora')).toBeInTheDocument();
    expect(within(drawer).queryByRole('combobox', { name: 'workspace' })).toBeNull();
    const send = within(drawer).getByRole('button', { name: 'ask' });
    expect(send).toBeDisabled();

    await userEvent.type(within(drawer).getByLabelText('what is wanted, and why'), 'Cap the hydration.');
    await userEvent.click(send);
    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ sentence: 'Cap the hydration.', to: '' }));
  });

  /** "Every circle" with several held has no single circle: the person says which, and it is not assumed. */
  it('asks which workspace when the page is scoped to none of several', async () => {
    render(<Holder fixed={null} circles={['aurora', 'tools']} />);
    const drawer = screen.getByRole('dialog');

    await userEvent.type(within(drawer).getByLabelText('what is wanted, and why'), 'Cap the hydration.');
    expect(within(drawer).getByRole('button', { name: 'ask' })).toBeDisabled();

    const user = userEvent.setup();
    within(drawer).getByRole('combobox', { name: 'workspace' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('option', { name: 'tools' }));

    expect(within(drawer).getByRole('button', { name: 'ask' })).toBeEnabled();
  });

  /**
   * A machine with no repository holds no circle, and the composer offered an empty choice of one:
   * a sentence could be written in full and never sent. Seen on the installed window, 2026-09-24.
   */
  it('says there is nowhere to ask yet, rather than a form that can never send', () => {
    render(<Holder fixed={null} circles={[]} />);
    const drawer = screen.getByRole('dialog');

    expect(within(drawer).getByText('There is no workspace to ask yet')).toBeInTheDocument();
    expect(within(drawer).queryByLabelText('what is wanted, and why')).toBeNull();
    expect(within(drawer).queryByRole('button', { name: 'ask' })).toBeNull();
  });

  it('carries links and files like the quest composer, from the same fields', async () => {
    render(<Holder fixed="aurora" circles={['aurora']} />);
    const drawer = screen.getByRole('dialog');

    await userEvent.upload(within(drawer).getByLabelText('choose files…'), new File(['x'], 'trace.log'));
    expect(within(drawer).getByText('trace.log')).toBeInTheDocument();
    expect(within(drawer).getByText(/links — a ticket/)).toBeInTheDocument();
  });
});

/**
 * 🔴 UX5 U32: the asks group ran newest first, the service's order, while the quests beneath it and
 * *What needs you* run oldest first, so one screen held two orders for one idea. What waits longest
 * leads, and a closed ask comes after every live one, as a closed quest does.
 */
describe('the asks, in the order they are read', () => {
  it('puts the ask that has waited longest first, and a closed one after every live one', () => {
    const older = { ...PROPOSED, id: 'a1', asked: '2026-09-20T09:00:00Z' };
    const newer = { ...PUBLISHED, id: 'b2', asked: '2026-09-24T09:00:00Z' };
    const closedEarliest = { ...CLOSED, id: 'c3', asked: '2026-09-01T09:00:00Z' };

    expect(asksInOrder([newer, closedEarliest, older]).map((ask) => ask.id)).toEqual(['a1', 'b2', 'c3']);
  });
});
