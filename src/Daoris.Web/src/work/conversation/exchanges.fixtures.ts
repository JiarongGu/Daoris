import type { HelperAskRow } from '../waits/HelperAsks';
import type { SessionEvent } from './conversation';

/**
 * A lead that asked Codex, as helper, through `helper_ask`, and the turn that took its answer (COMMS1h): the events and the asks the
 * cards are read from, for the tests and the stories alike.
 */
export const QUESTION = 'Is the store’s write lock held across the import?';
export const ANSWER = 'Yes: the write takes the lock at its start and the import runs inside it, so a second writer waits.\n\n'
  + 'The lock is released in the finally block of `ImportAsync`.';

const at = (minute: number) => `2026-11-04T16:${String(minute).padStart(2, '0')}:00Z`;

export function askedEvents(answered: boolean): SessionEvent[] {
  return [
    { seq: 1, at: at(20), kind: 'user', origin: 'person', text: 'Build the policy store.' },
    { seq: 2, at: at(29), kind: 'tool', id: 'call-1', title: 'helper_ask', status: 'completed', input: JSON.stringify({ question: QUESTION, agent: 'codex' }) },
    { seq: 3, at: at(29), kind: 'message', text: 'Asked Codex to check the lock; ending my turn so it can read the work.' },
    { seq: 4, at: at(30), kind: 'turn', stopReason: 'end_turn' },
    ...(answered
      ? [
        { seq: 5, at: at(42), kind: 'user', origin: 'target', helper: 'helper-7', text: ANSWER },
        { seq: 6, at: at(43), kind: 'message', text: 'The lock holds; going on with the import.' },
      ] satisfies SessionEvent[]
      : []),
  ];
}

export const askRow = (state: string | null, helper: string | null = 'helper-7'): HelperAskRow => ({
  id: 'h7', question: QUESTION, agent: 'codex', askedBy: 'agent', at: at(29), state, code: null, helper, startedOn: 'codex',
});

export const NAMES: Record<string, string> = { codex: 'Codex' };
export const nameOf = (agent: string) => NAMES[agent] ?? agent;
