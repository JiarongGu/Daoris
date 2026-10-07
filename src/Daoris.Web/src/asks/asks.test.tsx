import { type ReactElement, useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { act, render as rtlRender, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { NO_CARRY } from '../compose/carry';
import type { Answered } from '../work/InlineConfirm';
import { AskComposer, type AskDraft } from './AskComposer';
import { AskPage } from './AskPage';
import { AskRow, asksInOrder } from './AskRow';
import {
  BY_INTAKE, CLOSED, DONE, INTAKE_ASKED, INTAKE_PARKED, INTAKE_SESSION, NAMED, PROPOSED, PUBLISHED, REFUSED, UNKNOWN_TIER,
  UNMATCHED, WITH_GO_AHEADS,
} from './fixtures';

// An ask's three molecules (INT4c; FRAME1d): its row in Quests' list, its page in the main area, and the composer's
// drawer — props in and states out: no service and no shell, so every state the record can be in is an ordinary
// assertion (components §2).

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

/** A row is an item of its list. */
const row = (node: ReactElement) => <Tooltip.Provider><ul>{node}</ul></Tooltip.Provider>;

const record = (ask = PROPOSED, over: Partial<Parameters<typeof AskPage>[0]> = {}) => {
  const props = {
    ask, receivers: ['engine', 'game', 'lantern'], questTitles: {},
    onPublish: vi.fn(), onClose: vi.fn(), onOpenQuest: vi.fn(), ...over,
  };
  render(<AskPage {...props} />);
  return { ...props, page: screen.getByRole('main') };
};

describe('AskRow', () => {
  it('leads with its state and its first line, and says what answered and what it proposed', () => {
    rtlRender(row(<AskRow ask={PROPOSED} onOpen={() => {}} />));

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
    rtlRender(row(<AskRow ask={PROPOSED} onOpen={() => {}} />));
    expect(screen.getByText('workspace aurora')).toBeInTheDocument();
  });

  /**
   * 🔴 Seen on the window (POLISH4): five cards read "proposed" alike while one had an intake reading
   * it and one had its intake waiting on the person — which the band above said and the card did not.
   */
  it('says when an intake is reading it, and when its intake asked the person', () => {
    const { rerender } = rtlRender(row(<AskRow ask={INTAKE_ASKED} intake="working" onOpen={() => {}} />));
    expect(screen.getByText(/an intake is reading it/)).toBeInTheDocument();

    rerender(row(<AskRow ask={INTAKE_ASKED} intake="awaiting-person" onOpen={() => {}} />));
    expect(screen.getByText('its intake asked you')).toBeInTheDocument();
    expect(screen.queryByText(/an intake is reading it/)).toBeNull();

    // An intake that ended without publishing leaves an ordinary proposal, and says nothing more.
    rerender(row(<AskRow ask={INTAKE_ASKED} intake="completed" onOpen={() => {}} />));
    expect(screen.queryByText('its intake asked you')).toBeNull();
    expect(screen.queryByText(/an intake is reading it/)).toBeNull();
  });

  it('names the quests an ask became, and opens on a press or a key', async () => {
    const onOpen = vi.fn();
    rtlRender(row(<AskRow ask={PUBLISHED} onOpen={onOpen} />));

    expect(screen.getByText(/became #9a8b7c, #1f2e3d/)).toBeInTheDocument();
    screen.getByRole('button').focus();
    await userEvent.keyboard('{Enter}');
    expect(onOpen).toHaveBeenCalledWith(PUBLISHED);
  });

  /** FRAME1d: a row of its list, so the list's arrows move to it, wearing the list's choice. */
  it('is a row of its list, and says when the list has it chosen', () => {
    rtlRender(row(<AskRow ask={PROPOSED} chosen onOpen={() => {}} />));
    expect(screen.getByRole('listitem')).toHaveAttribute('data-list-row');
    expect(screen.getByRole('button')).toHaveAttribute('aria-current', 'true');
  });
});

describe('AskPage', () => {
  /** The sentence the tier writes about itself is what a person reads: which tier answered, never implied. */
  it('says which tier answered, in the words for it, beside the ask\'s own words whole', () => {
    const { page } = record(PROPOSED);
    expect(within(page).getByText('by declarations only; no intake agent ran')).toBeInTheDocument();
    // The first line is the title, and the rest follows it — each once (POLISH4).
    expect(within(page).getAllByText(/The chunk streamer stalls on a cold cache/)).toHaveLength(1);
    expect(within(page).getByText(/Seen on the test rig after a fresh install/)).toBeInTheDocument();
  });

  /** 🔴 Seen on the window (POLISH4): "answered by" above "by declarations…" read "answered by by". */
  it('labels the tier so that no tier\'s words repeat the label', () => {
    const { page } = record(PROPOSED);
    expect(within(page).queryByText('answered by')).toBeNull();
    expect(within(page).getByText('Answered')).toBeInTheDocument();
  });

  /** 🔴 Seen on the window (POLISH4): a one-line ask was its page's title and then its body. */
  it('does not repeat a one-line ask as its own body', () => {
    const { page } = record(UNMATCHED);
    expect(within(page).getAllByText('Tidy the release notes.')).toHaveLength(1);
  });

  it('names the workspace a receiver is chosen from as a workspace', () => {
    const { page } = record(UNMATCHED);
    // NAME2: the list offers only those that can be asked, so the hint no longer says it.
    expect(within(page).getByText('any repository in workspace aurora')).toBeInTheDocument();
  });

  it('shows a tier it has no word for as the service wrote it', () => {
    const { page } = record(UNKNOWN_TIER);
    expect(within(page).getByText('intake-session')).toBeInTheDocument();
  });

  it('offers each proposal as a publish, and publishes to the one pressed', async () => {
    const { page, onPublish } = record(PROPOSED);

    expect(within(page).getByText(/chunk, stream, frame/)).toBeInTheDocument();
    await userEvent.click(within(page).getByRole('button', { name: 'Publish to engine' }));

    expect(onPublish).toHaveBeenCalledWith('engine');
  });

  it('says when no declarations share its words, and still offers any receiver in its circle', async () => {
    const { page, onPublish } = record(UNMATCHED);

    expect(within(page).getByText(/No repository's declarations share its words/)).toBeInTheDocument();
    const user = userEvent.setup();
    within(page).getByRole('combobox', { name: 'publish to another' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('option', { name: 'lantern' }));
    await user.click(within(page).getByRole('button', { name: 'Publish' }));

    expect(onPublish).toHaveBeenCalledWith('lantern');
  });

  it('lists the quests it became and opens the one chosen, by its title where the page holds it', async () => {
    const { page, onOpenQuest } = record(PUBLISHED, { questTitles: { '9a8b7c6d5e4f': 'Cap the hydration' } });

    await userEvent.click(within(page).getByRole('button', { name: /Cap the hydration/ }));
    expect(onOpenQuest).toHaveBeenCalledWith('9a8b7c6d5e4f');
  });

  it('names a quest the page does not hold yet, and offers no door that would open nothing', () => {
    // Just after a publish, the answer names the quest before the list has asked again for it.
    const { page } = record(PUBLISHED, { questTitles: { '9a8b7c6d5e4f': 'Cap the hydration' } });

    expect(within(page).getByText('#1f2e3d')).toBeInTheDocument();
    expect(within(page).queryByRole('button', { name: /#1f2e3d/ })).toBeNull();
  });

  it('carries the named receiver\'s refusal verbatim, and keeps its proposal to go on with', () => {
    const { page } = record(REFUSED);

    expect(within(page).getByText(REFUSED.note!)).toBeInTheDocument();
    expect(within(page).getByRole('button', { name: 'Publish to engine' })).toBeInTheDocument();
  });

  it('closes with a reason, and not without one', async () => {
    const { page, onClose } = record(PROPOSED);

    await userEvent.click(within(page).getByRole('button', { name: 'Close ask' }));
    const confirm = within(page).getByRole('button', { name: 'Close with this reason' });
    expect(confirm).toBeDisabled();
    await userEvent.type(within(page).getByLabelText('why — what became of it'), 'Answered elsewhere.');
    await userEvent.click(confirm);

    expect(onClose).toHaveBeenCalledWith('Answered elsewhere.');
  });

  /** A closed ask becomes nothing more (INT4a): its reason stays, and no verb is offered. */
  it('offers nothing to do on a closed ask, and says why it closed', () => {
    const { page } = record(CLOSED);

    expect(within(page).getByText('Answered in the design review instead.')).toBeInTheDocument();
    expect(within(page).queryByRole('button', { name: /publish/i })).toBeNull();
    expect(within(page).queryByRole('button', { name: 'Close ask' })).toBeNull();
  });

  it('names its files and never the path this machine keeps them at', () => {
    const { page } = record(PROPOSED);

    expect(within(page).getByText('trace.log')).toBeInTheDocument();
    expect(page.textContent).not.toContain('somewhere');
    expect(within(page).getByRole('link', { name: /tickets\.example/ })).toHaveAttribute('href', 'https://tickets.example/T-42');
  });

  it('says a named receiver answered, and offers publishing it further', () => {
    const { page } = record(NAMED);

    expect(within(page).getByText('the asker named the receiver')).toBeInTheDocument();
    expect(within(page).getByRole('combobox', { name: 'publish to another' })).toBeInTheDocument();
  });

  // ——— Who answered (INT4d): the tier INT4b added, and the intake session that served it.

  it('says an intake session answered, in the words for it', () => {
    const { page } = record(BY_INTAKE, { intake: INTAKE_SESSION });
    expect(within(page).getByText('an intake session read it and published it')).toBeInTheDocument();
  });

  /** On the desktop the session is a door into Sessions, where its transcript and console are. */
  it('names its intake session by state and tool, and opens it where Sessions exists', async () => {
    const onAttend = vi.fn();
    const { page } = record(INTAKE_ASKED, { intake: INTAKE_PARKED, onAttend });
    const line = within(page).getByRole('region', { name: 'Intake session' });

    expect(within(line).getByText('waiting on you')).toBeInTheDocument();
    await userEvent.click(within(line).getByRole('button', { name: 'claude-code · 2.1.4' }));
    expect(onAttend).toHaveBeenCalledWith('i9n8t7k6a5b4');
  });

  /** A browser has no Sessions: the session is named, and nothing pretends to open it. */
  it('names its intake session without a door where Sessions does not exist', () => {
    const { page } = record(BY_INTAKE, { intake: INTAKE_SESSION });
    const line = within(page).getByRole('region', { name: 'Intake session' });

    expect(within(line).getByText('claude-code · 2.1.4')).toBeInTheDocument();
    expect(within(line).getByText('completed')).toBeInTheDocument();
    expect(within(line).queryByRole('button')).toBeNull();
  });

  it('names an intake session the page has not loaded by its id, and offers no door that would open nothing', () => {
    const { page } = record(BY_INTAKE, { onAttend: vi.fn() });
    const line = within(page).getByRole('region', { name: 'Intake session' });

    expect(within(line).getByText('#i9n8t7')).toBeInTheDocument();
    expect(within(line).queryByRole('button')).toBeNull();
  });

  /**
   * 🔴 Seen on the window: an intake that parks has published nothing, so the tier stays the
   * declarations' — and its words, "no intake harness ran", sat directly above the intake that ran.
   */
  it('never says no intake ran above the intake that did', () => {
    const { page } = record(INTAKE_ASKED, { intake: INTAKE_PARKED });
    expect(within(page).queryByText('by declarations only; no intake agent ran')).toBeNull();
    expect(within(page).getByText('by declarations; an intake read it and has not published')).toBeInTheDocument();
  });

  it('has no intake line for an ask no intake served', () => {
    const { page } = record(PROPOSED);
    expect(within(page).queryByRole('region', { name: 'Intake session' })).toBeNull();
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
    expect(within(drawer).queryByRole('combobox', { name: 'Workspace' })).toBeNull();
    const send = within(drawer).getByRole('button', { name: 'Ask' });
    expect(send).toBeDisabled();

    await userEvent.type(within(drawer).getByLabelText('What is wanted, and why'), 'Cap the hydration.');
    await userEvent.click(send);
    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ sentence: 'Cap the hydration.', to: '' }));
  });

  /** "Every circle" with several held has no single circle: the person says which, and it is not assumed. */
  it('asks which workspace when the page is scoped to none of several', async () => {
    render(<Holder fixed={null} circles={['aurora', 'tools']} />);
    const drawer = screen.getByRole('dialog');

    await userEvent.type(within(drawer).getByLabelText('What is wanted, and why'), 'Cap the hydration.');
    expect(within(drawer).getByRole('button', { name: 'Ask' })).toBeDisabled();

    const user = userEvent.setup();
    within(drawer).getByRole('combobox', { name: 'Workspace' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('option', { name: 'tools' }));

    expect(within(drawer).getByRole('button', { name: 'Ask' })).toBeEnabled();
  });

  /**
   * A machine with no repository holds no circle, and the composer offered an empty choice of one:
   * a sentence could be written in full and never sent. Seen on the installed window, 2026-09-24.
   */
  it('says there is nowhere to ask yet, rather than a form that can never send', () => {
    render(<Holder fixed={null} circles={[]} />);
    const drawer = screen.getByRole('dialog');

    expect(within(drawer).getByText('There is no workspace to ask yet')).toBeInTheDocument();
    expect(within(drawer).queryByLabelText('What is wanted, and why')).toBeNull();
    expect(within(drawer).queryByRole('button', { name: 'Ask' })).toBeNull();
  });

  it('carries links and files like the quest composer, from the same fields', async () => {
    render(<Holder fixed="aurora" circles={['aurora']} />);
    const drawer = screen.getByRole('dialog');

    await userEvent.upload(within(drawer).getByLabelText('Choose files…'), new File(['x'], 'trace.log'));
    expect(within(drawer).getByText('trace.log')).toBeInTheDocument();
    expect(within(drawer).getByText(/Links — a ticket/)).toBeInTheDocument();
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

  /** USE1c: an ask whose work is finished is read with the closed ones — it is not waiting on anyone. */
  it('puts a done ask with the closed ones, after every live one', () => {
    const live = { ...PUBLISHED, id: 'b2', asked: '2026-09-24T09:00:00Z' };
    const doneEarliest = { ...DONE, id: 'd4', asked: '2026-09-01T09:00:00Z' };
    const closed = { ...CLOSED, id: 'c3', asked: '2026-09-02T09:00:00Z' };

    expect(asksInOrder([closed, doneEarliest, live]).map((ask) => ask.id)).toEqual(['b2', 'd4', 'c3']);
  });
});

/**
 * QUEST1 (D95): an ask made by mistake is deleted with every quest asked by it — offered only where the
 * service says the ask may go, and asked once, because it cannot be undone.
 */
describe('deleting an ask', () => {
  it('offers no delete on an ask the service does not say may go', () => {
    const { page } = record(PUBLISHED, { onDelete: vi.fn() });
    expect(within(page).queryByRole('button', { name: 'Delete…' })).toBeNull();
  });

  it('asks once, saying its quests go with it, then deletes', async () => {
    const { page, onDelete } = record({ ...PUBLISHED, deletable: true }, { onDelete: vi.fn() });

    await userEvent.click(within(page).getByRole('button', { name: 'Delete…' }));
    const confirm = within(page).getByRole('group', { name: 'delete this ask' });
    expect(within(confirm).getByText(/every quest it became goes with it/)).toBeInTheDocument();
    expect(onDelete).not.toHaveBeenCalled();

    await userEvent.click(within(confirm).getByRole('button', { name: 'Delete ask' }));
    expect(onDelete).toHaveBeenCalledTimes(1);
  });

  it('never mind puts the verbs back and deletes nothing', async () => {
    const { page, onDelete } = record({ ...PROPOSED, deletable: true }, { onDelete: vi.fn() });

    await userEvent.click(within(page).getByRole('button', { name: 'Delete…' }));
    await userEvent.click(within(page).getByRole('button', { name: 'Never mind' }));

    expect(within(page).getByRole('button', { name: 'Close ask' })).toBeInTheDocument();
    expect(onDelete).not.toHaveBeenCalled();
  });

  /**
   * UXFIX2 (the second-opinion review, `AskPage.tsx:260`): the delete's ask is the one inline confirmation. What goes takes
   * the focus and describes *Delete ask*; the press keeps it open and waiting, where it closed at once and refused in a
   * toast; a refusal is said inside it, word for word; it closes once the delete lands; Escape gives the focus back to
   * *Delete…*, drawn again.
   */
  it('takes the focus to what goes, stays open until the delete answers, says a refusal inside, and gives the focus back', async () => {
    let answered: Answered | undefined;
    const { page } = record({ ...PUBLISHED, deletable: true }, { onDelete: (told: Answered) => { answered = told; } });
    const user = userEvent.setup();

    await user.click(within(page).getByRole('button', { name: 'Delete…' }));
    const says = within(page).getByText(/every quest it became goes with it/);
    await waitFor(() => expect(says).toHaveFocus());
    expect(within(page).getByRole('button', { name: 'Delete ask' })).toHaveAccessibleDescription(says.textContent!);
    await user.keyboard('{Escape}');
    expect(within(page).queryByRole('group', { name: 'delete this ask' })).toBeNull();
    expect(within(page).getByRole('button', { name: 'Delete…' })).toHaveFocus();

    await user.click(within(page).getByRole('button', { name: 'Delete…' }));
    await user.click(within(page).getByRole('button', { name: 'Delete ask' }));
    const confirm = within(page).getByRole('group', { name: 'delete this ask' });
    expect(within(confirm).getByRole('button', { name: 'Never mind' })).toBeDisabled();
    act(() => answered!.refused('Ask #b2c3d4 has a quest a session took, so it stays. Close it instead.'));
    expect(within(confirm).getByRole('alert')).toHaveTextContent('Ask #b2c3d4 has a quest a session took, so it stays. Close it instead.');

    await user.click(within(confirm).getByRole('button', { name: 'Delete ask' }));
    act(() => answered!.done());
    expect(within(page).queryByRole('group', { name: 'delete this ask' })).toBeNull();
  });

  /** A closed ask becomes nothing more — but one made by mistake can still go, when the service says so. */
  it('offers delete on a closed ask the service says may go, and nothing else', () => {
    const { page } = record({ ...CLOSED, deletable: true }, { onDelete: vi.fn() });

    expect(within(page).getByRole('button', { name: 'Delete…' })).toBeInTheDocument();
    expect(within(page).queryByRole('button', { name: 'Close ask' })).toBeNull();
  });
});

/**
 * USE1c: an ask the service reports DONE — it became quests and every one of them closed. It says so in
 * a pill of its own, reads as finished on its row, and names what it became.
 */
describe('a done ask', () => {
  it('wears a done pill and reads as finished on its row, naming what it became', () => {
    rtlRender(row(<AskRow ask={DONE} onOpen={() => {}} />));

    expect(screen.getByText('done')).toBeInTheDocument();
    expect(screen.getByText(/became #9a8b7c, #1f2e3d/)).toBeInTheDocument();
    // Read as finished, the way a closed record is: the row's dimmed face.
    expect(screen.getByRole('button').closest('.opacity-75')).not.toBeNull();
  });

  it('says it is done on its record', () => {
    const { page } = record(DONE);
    expect(within(page).getByText('done')).toBeInTheDocument();
  });
});

/**
 * KNOWUSE1a (D135 §2): the go-aheads its sessions asked the person for, each once, on the ask's page. Each says the act
 * by its kind, where it lands and what it touches, what became of it, and the person's words on an answer verbatim; one
 * waiting is answered here, yes or no with words if any, and one answered can be answered again.
 */
describe('the go-aheads on an ask', () => {
  const section = (page: HTMLElement) => within(within(page).getByRole('region', { name: 'Go-aheads' }));

  it('lists each by its number, its act and what became of it, with the person\'s words verbatim', () => {
    const { page } = record(WITH_GO_AHEADS, { onAnswerGoAhead: vi.fn() });
    const goAheads = section(page);

    const items = goAheads.getAllByRole('listitem');
    expect(items).toHaveLength(4);
    expect(within(items[0]).getByText('#1')).toBeInTheDocument();
    expect(within(items[0]).getByText('write on production')).toBeInTheDocument();
    expect(within(items[0]).getByText('“dashboard configuration”')).toBeInTheDocument();
    expect(within(items[0]).getByText('approved')).toBeInTheDocument();
    expect(within(items[0]).getByText('run the put')).toBeInTheDocument();
    expect(within(items[1]).getByText('refused')).toBeInTheDocument();
    expect(within(items[1]).getByText('test it on dev first')).toBeInTheDocument();
    expect(within(items[2]).getByText('waiting on you')).toBeInTheDocument();
    expect(within(items[2]).getByText('The report needs an entry on both sites.')).toBeInTheDocument();
    // Asked by two sessions, and asked again because its words could not be told from #1's.
    expect(within(items[2]).getByText(/asked again by 1 more session/)).toBeInTheDocument();
    expect(within(items[2]).getByText(/could not be told from #1/)).toBeInTheDocument();
    // A kind this page has no word for is shown as the service wrote it.
    expect(within(items[3]).getByText('teleport on the moon')).toBeInTheDocument();
  });

  it('answers one waiting yes, with the person\'s words', async () => {
    const onAnswerGoAhead = vi.fn();
    const { page } = record(WITH_GO_AHEADS, { onAnswerGoAhead });
    const waiting = within(section(page).getAllByRole('listitem')[2]);

    await userEvent.type(waiting.getByRole('textbox', { name: 'your words, if any' }), 'only on the report site');
    await userEvent.click(waiting.getByRole('button', { name: 'Approve' }));

    expect(onAnswerGoAhead).toHaveBeenCalledWith(3, true, 'only on the report site');
  });

  it('answers one waiting no, with no words', async () => {
    const onAnswerGoAhead = vi.fn();
    const { page } = record(WITH_GO_AHEADS, { onAnswerGoAhead });

    await userEvent.click(within(section(page).getAllByRole('listitem')[2]).getByRole('button', { name: 'Refuse' }));

    expect(onAnswerGoAhead).toHaveBeenCalledWith(3, false, undefined);
  });

  it('answers one already answered again, only once asked to', async () => {
    const onAnswerGoAhead = vi.fn();
    const { page } = record(WITH_GO_AHEADS, { onAnswerGoAhead });
    const approved = within(section(page).getAllByRole('listitem')[0]);

    expect(approved.queryByRole('button', { name: 'Refuse' })).toBeNull();
    await userEvent.click(approved.getByRole('button', { name: 'Change answer' }));
    await userEvent.click(approved.getByRole('button', { name: 'Refuse' }));

    expect(onAnswerGoAhead).toHaveBeenCalledWith(1, false, undefined);
  });

  it('offers no answer where there is no door to give it', () => {
    const { page } = record(WITH_GO_AHEADS);

    expect(section(page).queryByRole('button', { name: 'Approve' })).toBeNull();
    expect(section(page).queryByRole('button', { name: 'Change answer' })).toBeNull();
  });

  it('has no section where no session asked for one', () => {
    const { page } = record(PUBLISHED, { onAnswerGoAhead: vi.fn() });
    expect(within(page).queryByRole('region', { name: 'Go-aheads' })).toBeNull();
  });
});
