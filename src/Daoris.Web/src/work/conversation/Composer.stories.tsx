import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import * as Tooltip from '@radix-ui/react-tooltip';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { sessionWidths } from '../../design/storyWidths';
import { Composer } from './Composer';

// The states the inventory names, and the reason each is a state: sending is not idle, an ending is
// not a disabled box, a refusal is a sentence rather than a shrug, and a running turn is not idle
// either — what waits behind it and its own stop are shown (CONV4b). Drawn at the foot of a session's page at the
// frame's widths (the UI/UX design R6, STORYGAP1): half by default, then wide, narrow, 中文 and dark.

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta: Meta<typeof Composer> = {
  title: 'Work/Composer',
  component: Composer,
  args: { live: true, onSend: () => {}, onFinish: () => {}, onStop: () => {} },
  decorators: [(Story) => <Tooltip.Provider><Story /></Tooltip.Provider>, sessionWidths.half],
};
export default meta;

type Story = StoryObj<typeof Composer>;

/** Listening. Two endings side by side, and they are never one button. */
export const Idle: Story = {};

/** A message in flight: send is held, the endings are not. */
export const Sending: Story = { args: { sending: true } };

/**
 * The session ended while somebody was typing. What they wrote stays, read-only so it can be copied
 * out — a box that swallowed a paragraph they were halfway through gives them no way to get it back.
 */
export const Ended: Story = { args: { live: false, draft: 'half a thought about the chunk cap', onDraft: () => {} } };

/** An ended session nobody was writing in: no box and no send, since nothing listens (UX5 U9). */
export const EndedWithNothingWritten: Story = { args: { live: false, context: { door: 'structured' } } };

/** The ledger's own sentence, verbatim, where the person is looking rather than in a toast. */
export const Refused: Story = {
  args: { refusal: 'Nothing is listening: this session ended while you were typing.' },
};

/**
 * A turn is running (CONV4b): send says *queue*, what waits behind the turn is shown in the order
 * sent, and the turn's own stop stands beside the two endings.
 */
export const TurnRunning: Story = {
  args: {
    taking: true, stoppable: true, onStopTurn: () => {},
    queued: [
      { text: 'and then run the streaming tests', files: [] },
      { text: 'if they pass, commit it — do not push', files: ['release-notes.md'] },
    ],
  },
};

/**
 * COWORK1a (D188; the message-urgency design §8.1): words typed while a turn runs go with *Send* at the person's default, and
 * the level menu beside it sends them at another; here the agent steers, so *Now, cutting its step* is offered.
 */
export const LevelsWhileATurnRuns: Story = {
  args: {
    taking: true, stoppable: true, onStopTurn: () => {}, levels: { nextStep: true, steers: true },
    onLevel: async () => ({ done: true }), onRemove: async () => ({ done: true }), onEdit: async () => ({ done: true }),
    draft: 'use the blue one instead', onDraft: () => {},
    queued: [{ id: 'a1', text: 'and tidy the readme once you are done', files: [], level: 'ordinary' }],
  },
};

/**
 * COWORK1a (the design §3.2): a door that takes words only when its turn ends holds them, the person's words at now named so,
 * and the keeping stop stands beside them, asking once.
 */
export const KeepingStopBesideHeldWords: Story = {
  args: {
    taking: true, stoppable: true, onStopTurn: () => {}, levels: { nextStep: false, steers: false }, onKeepingStop: () => {},
    onLevel: async () => ({ done: true }),
    queued: [{ id: 'a1', text: 'stop: the streaming tests are in /spec, run those first', files: [], level: 'now' }],
  },
};

/** QUEUEEDIT1b: each waiting message that has an id can be taken back out or reworded; one without shows no controls. */
export const WaitingWithControls: Story = {
  args: {
    taking: true, stoppable: true, onStopTurn: () => {},
    onRemove: async () => ({ done: true }), onEdit: async () => ({ done: true }),
    queued: [
      { id: 'a1', text: 'and then run the streaming tests', files: [] },
      { id: 'b2', text: 'if they pass, commit it — do not push', files: ['release-notes.md'] },
      { text: 'a word with no id', files: [] },
    ],
  },
};

/** One taken already: the sentence says so, as a note and not an error. */
export const WaitingAlreadySent: Story = {
  args: { ...WaitingWithControls.args, onRemove: async () => ({ done: false }) },
};

/** SENDLOST1b: words on their way show at once, as sending, apart from what waits behind the turn, and with no controls. */
export const WordsSending: Story = {
  args: { ...TurnRunning.args, going: { words: [{ text: 'and the line under the chart?', files: [] }], late: false } },
};

/** SENDLOST1b: the page stopped waiting on their send; they stay, saying the machine may still take them. */
export const WordsUnanswered: Story = {
  args: { ...TurnRunning.args, going: { words: [{ text: 'and the line under the chart?', files: [] }], late: true } },
};

/** The stop was asked for; the button holds until the driver answers. */
export const StoppingTheTurn: Story ={ args: { taking: true, stoppable: true, stopping: true, onStopTurn: () => {} } };

/**
 * Files attached (CONV4c): chips above the box a person can take back off, going with the next send.
 * Chosen here through the paperclip in the story; dropped or pasted on the window.
 */
export const WithFilesAttached: Story = {
  play: async ({ canvasElement }) => {
    const input = canvasElement.querySelector<HTMLInputElement>('input[type=file]');
    if (!input) return;
    const files = new DataTransfer();
    files.items.add(new File(['exit 3\npanic at chunk 12'], 'crash.log', { type: 'text/plain' }));
    files.items.add(new File([new Uint8Array(48_000)], 'screenshot.png', { type: 'image/png' }));
    input.files = files.files;
    input.dispatchEvent(new Event('change', { bubbles: true }));
  },
};

/**
 * `@` a file (CONV4d): after the `@`, the session tree's files, best first. Arrows move, Enter or Tab
 * writes the one chosen, Escape leaves what was typed.
 */
export const MentioningAFile: Story = {
  decorators: [(Story) => <div className="pt-72"><Story /></div>],
  args: {
    draft: 'read @de',
    onDraft: () => {},
    mentions: {
      files: ['README.md', 'docs/design.md', 'docs/deep file.md', 'src/engine/render.ts'],
      unlisted: 0,
      refusal: null,
    },
  },
  play: async ({ canvasElement }) => {
    const box = canvasElement.querySelector<HTMLTextAreaElement>('textarea');
    if (!box) return;
    box.focus();
    box.setSelectionRange(box.value.length, box.value.length);
  },
};

/** How full the context is (CONV5), at the far end of the controls, where the reference keeps it. */
export const WithContext: Story = {
  args: { context: { usage: { used: 34_120, size: 1_000_000, most: 41_000 }, door: 'structured' } },
};

/** A door that carries only text cannot see a turn end, so it offers no stop — never one that is refused. */
export const TurnRunningOnATextDoor: Story = { args: { taking: true, stoppable: false, onStopTurn: () => {} } };

/** A running turn with its levels and what waits, across a maximized window's main area. */
export const LevelsWide: Story = { args: LevelsWhileATurnRuns.args, decorators: [sessionWidths.wide] };

/** The same at the main area's 400 px floor. */
export const LevelsNarrow: Story = { args: LevelsWhileATurnRuns.args, decorators: [sessionWidths.narrow] };

/** In 中文, narrow. */
export const LevelsChineseNarrow: Story = { args: LevelsWhileATurnRuns.args, decorators: [sessionWidths.narrow, chinese] };

/** In dark. */
export const LevelsDark: Story = { args: LevelsWhileATurnRuns.args, decorators: [dark] };
