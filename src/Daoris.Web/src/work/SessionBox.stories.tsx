import type { Meta, StoryObj } from '@storybook/react-vite';
import * as Tooltip from '@radix-ui/react-tooltip';
import { NATIVE_WORDS_LIMIT } from './say';
import { SessionBox } from './SessionBox';

// Every state of the box at the foot of a session's page (MSG1f, D137 §5.1): the running door at its next step and at its
// turn's end, the park's answer, a session that ended going on, words held while one winds up, the native door's bound,
// and the line for each code nothing takes words for — the states a real session rarely shows at will.

const meta: Meta<typeof SessionBox> = {
  title: 'Work/SessionBox',
  component: SessionBox,
  args: { onSend: () => {}, onSendNow: () => {}, quest: 'q8f2e1' },
  decorators: [(Story) => (
    <Tooltip.Provider><div className="max-w-2xl border border-line"><Story /></div></Tooltip.Provider>
  )],
};
export default meta;

type Story = StoryObj<typeof SessionBox>;

/**
 * Its turn running, on a door that takes words at its next step (D136): a word goes at once, so nothing waits above the
 * box and *Send now* has nothing to send; the conversation says when it reads it.
 */
export const RunningNextStep: Story = { args: { box: { kind: 'steer' }, held: { queued: [], taking: true } } };

/** Its turn running, on a door that hears words when its turn ends (D90): what it holds waits, and *Send now* sends it. */
export const RunningTurnEnd: Story = {
  args: {
    box: { kind: 'steer' },
    held: { queued: [{ text: 'and cap the budget at 64 KiB', files: [] }], taking: true },
  },
};

/** Parked to ask the person: the box is the answer (D126 §3.1). */
export const Parked: Story = { args: { box: { kind: 'say', mode: 'answer' } } };

/** Ended, and the same session goes on with the words (D137 §2.2): written to after its review, the next day. */
export const EndedGoingOn: Story = {
  args: { box: { kind: 'say', mode: 'goOn' }, draft: 'also say so in the release notes', onDraft: () => {} },
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

/** A record the service no longer has. */
export const LineNotFound: Story = { args: { box: { kind: 'line', why: 'not-found' } } };

/** A code a newer host answers that this page has no sentence for: named, never left unsaid. */
export const LineUnknownCode: Story = { args: { box: { kind: 'line', why: 'no-words' } } };
