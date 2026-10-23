import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { GoAhead, Session } from '../../data/api';
import type { ParkQuestion } from '../waits/parkQuestion';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { sessionWidths } from '../../design/storyWidths';
import { NATIVE_WORDS_LIMIT } from '../say';
import { SessionBox } from './SessionBox';

// Every state of the box at the foot of a session's page (MSG1f, D137 §5.1): the running door at its next step and at its
// turn's end, the park's answer with what it asks as presses (PARKVIEW1), a session that ended going on, words held while one winds up, the native door's bound,
// and the line for each code nothing takes words for — the states a real session rarely shows at will. Drawn at the
// frame's widths (the UI/UX design R6, STORYGAP1): half by default, then wide, narrow and dark.

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta: Meta<typeof SessionBox> = {
  title: 'Work/SessionBox',
  component: SessionBox,
  args: { onSend: () => {}, quest: 'q8f2e1' },
  decorators: [(Story) => <Tooltip.Provider><Story /></Tooltip.Provider>, sessionWidths.half],
};
export default meta;

type Story = StoryObj<typeof SessionBox>;

/**
 * Its turn running, on a door that takes words at its next step (D136): an urgent word goes at once, so nothing waits above
 * the box; the conversation says when it reads it.
 */
export const RunningNextStep: Story = {
  args: { box: { kind: 'steer' }, held: { queued: [], taking: true }, levels: { nextStep: true, steers: false } },
};

/** COWORK1a (D188): words typed while its turn runs go with *Send* at the default, or at a level from the menu beside it. */
export const RunningAtALevel: Story = {
  args: {
    box: { kind: 'steer' }, held: { queued: [], taking: true }, levels: { nextStep: true, steers: true },
    draft: 'use the blue one instead', onDraft: () => {},
  },
};

/**
 * Its turn running, on a door that hears words when its turn ends (D90): what it holds waits, and the keeping stop beside it
 * stops the turn so it goes next (COWORK1a), asking once; no word stops the turn.
 */
export const RunningTurnEnd: Story = {
  args: {
    box: { kind: 'steer' }, levels: { nextStep: false, steers: false }, onKeepingStop: () => {},
    held: { queued: [{ id: 'a1', text: 'and cap the budget at 64 KiB', files: [], level: 'urgent' }], taking: true },
  },
};

/** The same in 中文, a word sent now named so. */
export const RunningTurnEndChinese: Story = {
  args: {
    ...RunningTurnEnd.args,
    held: { queued: [{ id: 'a1', text: '先跑流式测试，它们在 /spec 里', files: [], level: 'now' }], taking: true },
  },
  decorators: [chinese],
};

/** QUEUEEDIT1b: a held word with an id can be removed or reworded. */
export const RunningWithControls: Story = {
  args: {
    box: { kind: 'steer' },
    held: { queued: [{ id: 'a1', text: 'and cap the budget at 64 KiB', files: [] }], taking: true },
    onRemove: async () => ({ done: true }), onEdit: async () => ({ done: true }),
  },
};

/** Parked to ask the person: the box is the answer (D126 §3.1). */
export const Parked: Story = { args: { box: { kind: 'say', mode: 'answer' } } };

// PARKVIEW1 (the park's ask design §3-§4): what a park asks, in its box above the words, every answer a press the words go with.
const ASKED: ParkQuestion = {
  id: 'q5t10n00',
  question: 'Which quarterly target should the dashboard tile show? The ticket says "the new quarterly target" without a number.',
  choices: [
    { label: '1,200', does: 'Write 1,200, the planning sheet\'s value, which the ticket links; development already holds it.' },
    { label: '1,350', does: 'Write 1,350, the value in the last stand-up note.' },
    { label: 'Leave it for now', does: 'Ship the tile reading the configuration, and leave the value as it is.' },
  ],
  recommend: 1,
  why: 'The ticket links the planning sheet, and development already holds that value.',
  by: 'before Friday\'s release window',
  at: '2026-10-22T09:05:00Z',
};

const PRODUCTION: GoAhead = {
  number: 1, kind: 'write', on: 'production', act: 'dashboard configuration', state: 'asked',
  asked: [{ session: 's1a2b3c4', quest: '7a82cc', at: '2026-10-22T09:04:00Z', why: 'The tile reads its target from it, and production still holds last quarter\'s value.' }],
};

const PARK = {
  quest: '7a82cc', tree: 's-1a2b3c4', since: new Date(Date.now() - 12 * 60_000).toISOString(),
  onChoice: () => {}, onGoAhead: () => {},
};

/** Parked on a question its agent asked through the connector: each answer a press beside what it does, one recommended. */
export const ParkedAsking: Story = {
  args: { box: { kind: 'say', mode: 'answer' }, park: { ...PARK, question: ASKED, goAheads: [] } },
};

/** The same across a maximized window's main area. */
export const ParkedAskingWide: Story = { ...ParkedAsking, decorators: [sessionWidths.wide] };

/** The same at the main area's 400 px floor, where each answer's words wrap under its press. */
export const ParkedAskingNarrow: Story = { ...ParkedAsking, decorators: [sessionWidths.narrow] };

/** The same in 中文: the chrome translated, the agent's question and answers as written. */
export const ParkedAskingChinese: Story = { ...ParkedAsking, decorators: [chinese] };

/** The same in dark. */
export const ParkedAskingDark: Story = { ...ParkedAsking, decorators: [dark] };

/**
 * The owner's case, in neutral words: parked on a production go-ahead beside its question, with the person's words in the box. The
 * go-ahead comes first and its press takes the words; the question's presses wait for it, said so.
 */
export const ParkedOnAProductionGoAhead: Story = {
  args: {
    box: { kind: 'say', mode: 'answer' }, draft: 'only the menu entry, not the report', onDraft: () => {},
    park: { ...PARK, question: ASKED, goAheads: [PRODUCTION] },
  },
};

/** The same at the floor. */
export const ParkedOnAProductionGoAheadNarrow: Story = { ...ParkedOnAProductionGoAhead, decorators: [sessionWidths.narrow] };

/** The same in 中文 at the floor. */
export const ParkedOnAProductionGoAheadChineseNarrow: Story = {
  ...ParkedOnAProductionGoAhead, decorators: [sessionWidths.narrow, chinese],
};

/** The same in dark, across the wide main area. */
export const ParkedOnAProductionGoAheadDarkWide: Story = { ...ParkedOnAProductionGoAhead, decorators: [sessionWidths.wide, dark] };

/** Parked on the go-ahead alone, its question asked only in its last message: the go-ahead's presses, and the free answer. */
export const ParkedOnAGoAheadAlone: Story = {
  args: { box: { kind: 'say', mode: 'answer' }, park: { ...PARK, tree: null, question: null, goAheads: [PRODUCTION] } },
};

/** The same in 中文. */
export const ParkedOnAGoAheadAloneChinese: Story = { ...ParkedOnAGoAheadAlone, decorators: [chinese] };

/**
 * PARKWORDS1: parked since five hours ago, its question asked only in its last message. One line says where it asked and how long
 * it has waited; what it holds and how the answer travels stay folded under *While it waits*.
 */
export const ParkedLongAbove: Story = {
  args: {
    box: { kind: 'say', mode: 'answer' },
    park: { ...PARK, since: new Date(Date.now() - (5 * 60 + 6) * 60_000).toISOString(), question: null, goAheads: [] },
  },
};

/** The same in 中文. */
export const ParkedLongAboveChinese: Story = { ...ParkedLongAbove, decorators: [chinese] };

/** The same with its disclosure opened: no deadline, its quest and tree held, and where the answer goes. */
export const ParkedLongAboveOpened: Story = {
  ...ParkedLongAbove,
  play: async ({ canvasElement }) => {
    canvasElement.querySelectorAll('details').forEach((details) => { details.open = true; });
  },
};

/** The same opened, in 中文 at the floor. */
export const ParkedLongAboveOpenedChineseNarrow: Story = { ...ParkedLongAboveOpened, decorators: [sessionWidths.narrow, chinese] };

/** Ended, and the same session goes on with the words (D137 §2.2): written to after its review, the next day. */
export const EndedGoingOn: Story = {
  args: { box: { kind: 'say', mode: 'goOn' }, draft: 'also say so in the release notes', onDraft: () => {} },
};

/**
 * SAIDBACK1b (D76): words waiting on a session that ended, the same session to go on with them, each with *Edit* and *Remove*
 * until a session takes them: a closed quest's session's two words, one of them out of date.
 */
export const EndedWithWordsWaiting: Story = {
  args: {
    box: { kind: 'say', mode: 'goOn' },
    waiting: [
      { id: 'w1', text: 'use the blue palette for the charts', files: [] },
      { id: 'w2', text: 'and say so in the release notes', files: ['notes.md'] },
    ],
    onRemove: async () => ({ done: true }), onEdit: async () => ({ done: true }),
  },
};

/** Written to as its run wound up (D137 §2.1): the words wait for its record to end, then reopen it. */
export const HeldWhileWindingUp: Story = {
  args: { box: { kind: 'say', mode: 'ending' }, ending: ['one more thing: keep the old flag for a release'] },
};

/** On the native door, words past the bound are refused before they are sent (D137 §2.4), and the draft stays. */
export const TooLongForTheNativeDoor: Story = {
  args: {
    box: { kind: 'say', mode: 'goOn' },
    refusal: `This agent's door takes at most ${NATIVE_WORDS_LIMIT} characters at once; shorten it, or send it in parts.`,
    draft: 'a pasted log that runs on…', onDraft: () => {},
  },
};

/** A teammate's record: its conversation is on their machine (D47 §6). */
export const LineTeammate: Story = { args: { box: { kind: 'line', why: 'teammate' } } };

/** A session that stood down: its quest is someone else's. */
export const LineStoodDown: Story = { args: { box: { kind: 'line', why: 'stood-down' } } };

/** An intake that ended: it is answered through its ask (INT4h). */
export const LineIntake: Story = { args: { box: { kind: 'line', why: 'intake' } } };

/** Ask Daoris's own conversation: its panel starts a new one (D137 §5.4). */
export const LineHelp: Story = { args: { box: { kind: 'line', why: 'help' } } };

/** An earlier session of a quest that went on in a later one here (MSG1d). */
export const LineSuperseded: Story = { args: { box: { kind: 'line', why: 'superseded' } } };

/** A conversation handed over or moved, naming the one it went on in (HANDWORDS1). */
export const LineContinued: Story = { args: { box: { kind: 'line', why: 'continued', to: 'c4d5e6f7' } } };

/**
 * HANDWORDS1-ii: where the frame holds the conversation it went on in, the line names it by its title and *Open session*
 * attends it: moved by the curve to another account at the end of its turn.
 */
export const LineContinuedTitled: Story = {
  args: {
    box: { kind: 'line', why: 'continued', to: 'c4d5e6f7' },
    continued: { session: { id: 'c4d5e6f7' } as Session, title: 'Tidy the release notes' },
    onOpenSession: () => {},
  },
};

/** The same in 中文, the title as its record keeps it. */
export const LineContinuedTitledChinese: Story = {
  args: {
    ...LineContinuedTitled.args,
    continued: { session: { id: 'c4d5e6f7' } as Session, title: '整理发布说明' },
  },
  decorators: [chinese],
};

/** A record the service no longer has. */
export const LineNotFound: Story = { args: { box: { kind: 'line', why: 'not-found' } } };

/** A code a newer host answers that this page has no sentence for: named, never left unsaid. */
export const LineUnknownCode: Story = { args: { box: { kind: 'line', why: 'no-words' } } };

/** Words waiting on an ended session, across a maximized window's main area. */
export const EndedWithWordsWaitingWide: Story = { args: EndedWithWordsWaiting.args, decorators: [sessionWidths.wide] };

/** The same at the main area's 400 px floor. */
export const EndedWithWordsWaitingNarrow: Story = { args: EndedWithWordsWaiting.args, decorators: [sessionWidths.narrow] };

/** Its turn running with a held word, in 中文 at the floor. */
export const RunningTurnEndChineseNarrow: Story = { args: RunningTurnEndChinese.args, decorators: [sessionWidths.narrow, chinese] };

/** Its turn running with a held word, in dark. */
export const RunningTurnEndDark: Story = { args: RunningTurnEnd.args, decorators: [dark] };
