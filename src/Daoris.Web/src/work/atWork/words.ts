import type { AtWorkChildState } from './types';

/**
 * The one key each child state draws (the cowork view design §5.4, *each state's words*): an end the wire said reads the panel's own
 * word, and a state Daoris's reading gave reads the view's; a key missing from a catalogue fails the mapping's test.
 */
export const CHILD_STATE_KEY: Readonly<Record<AtWorkChildState, string>> = {
  working: 'sessionState.working',
  unsaid: 'work.atWork.child.unsaid',
  completed: 'work.panel.stream.state.completed',
  failed: 'work.panel.stream.state.failed',
  cancelled: 'work.panel.stream.state.cancelled',
  disconnected: 'work.panel.stream.state.disconnected',
  stopped: 'work.panel.stream.state.stopped',
  finished: 'work.atWork.child.finished',
  ended: 'work.atWork.child.ended',
  lost: 'work.atWork.child.lost',
  agentEnded: 'work.atWork.child.agentEnded',
  unrecorded: 'work.atWork.child.unrecorded',
  uncertain: 'work.atWork.child.uncertain',
};

/** A part's state: its record's own, worded as a session's is, never the strip's shown word. */
export const partStateKey = (state: string): string => `sessionState.${state}`;

/** The record states a part can hold, the nine and `idle`. */
export const PART_STATES = [
  'queued', 'starting', 'working', 'awaiting-person', 'completed', 'declined', 'stood-down', 'failed', 'stopped', 'idle',
] as const;

/** States whose row says the child's last line rather than an end's moment. */
export const LAST_SEEN: ReadonlySet<AtWorkChildState> = new Set(['uncertain', 'unrecorded', 'agentEnded']);

/** How a state's word is toned: live where it works, warn where it was lost, neutral where nothing is proven. */
export function childTone(state: AtWorkChildState): 'live' | 'warn' | 'failed' | 'ended' | 'neutral' {
  switch (state) {
    case 'working':
      return 'live';
    case 'lost':
      return 'warn';
    case 'failed':
      return 'failed';
    case 'unsaid':
    case 'uncertain':
    case 'unrecorded':
    case 'agentEnded':
      return 'neutral';
    default:
      return 'ended';
  }
}
