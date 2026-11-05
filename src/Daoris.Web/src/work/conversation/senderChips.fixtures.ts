import type { Channel } from '../channel';
import type { HelperAskRow } from '../waits/HelperAsks';
import type { SessionEvent } from './conversation';

/**
 * A lead's turns as each sender's word began one (COMMS1h, the comms design §7): the person's with its receipts, Daoris's own, another
 * session's claim from the channel, a helper's answer to the person's own ask (MIXAGENT3a-iii), the supervisor's press and a second
 * opinion's findings; for the tests and the stories alike.
 */
export const HELPER_QUESTION = 'Does the import keep the store’s write lock until it commits?';
export const HELPER_ANSWER = 'It does: `ImportAsync` takes the lock first and releases it in its finally block, after the commit.';
export const CLAIM = 'Session `s9` on claude-code wrote:\n\n> The policy table moved to `policies_v2`; read that one.\n\n'
  + 'This is that agent’s claim, not the person’s words.';

const at = (minute: number) => `2026-11-04T16:${String(minute).padStart(2, '0')}:00Z`;

/** A turn: the word that began it, at its own `seq` and moment, the agent's reply and the turn's end. */
const turn = (seq: number, minute: number, word: SessionEvent, said: string): SessionEvent[] => [
  word,
  { seq: seq + 1, at: at(minute + 1), kind: 'message', text: said },
  { seq: seq + 2, at: at(minute + 1), kind: 'turn', stopReason: 'end_turn' },
];

export function senderEvents(): SessionEvent[] {
  return [
    ...turn(1, 2, { seq: 1, at: at(2), kind: 'user', origin: 'person', id: 'w1', text: 'Build the policy store.' }, 'Starting.'),
    { seq: 4, at: at(2), kind: 'receipt', id: 'w1', receipt: 'sent' },
    ...turn(5, 8, {
      seq: 5, at: at(8), kind: 'user', origin: 'target', from: 'daoris', text: 'Daoris: the work #q3 grew from was reopened.',
    }, 'Reading the reopened work.'),
    ...turn(8, 14, { seq: 8, at: at(14), kind: 'user', origin: 'target', channel: 'm4', text: CLAIM }, 'Reading `policies_v2`.'),
    ...turn(11, 20, { seq: 11, at: at(20), kind: 'user', origin: 'target', helper: 'helper-3', text: HELPER_ANSWER }, 'The lock holds.'),
    ...turn(14, 26, {
      seq: 14, at: at(26), kind: 'user', origin: 'target', from: 'supervisor', choice: 'Use the staging copy',
      text: 'Use the staging copy',
    }, 'Using the staging copy.'),
    ...turn(17, 32, {
      seq: 17, at: at(32), kind: 'user', origin: 'target', opinion: 'o2', text: 'Two findings: the lock is held across a network call; the retry has no bound.',
    }, 'Taking the findings.'),
  ];
}

/** The person's ask of a helper, from the session's page: no call in the conversation asked it, so its answer is that agent's block. */
export const personsAsk = (helper: string | null = 'helper-3'): HelperAskRow => ({
  id: 'h3', question: HELPER_QUESTION, agent: 'codex', askedBy: 'person', at: at(15), state: 'answered', code: null, helper, startedOn: 'codex',
});

/** The channel its claim came from, as the page's panel reads it: the message by its id, from another repository's session. */
export const CHANNEL: Channel = {
  ask: 'a1', entry: 'e1', unread: 0,
  items: [
    {
      kind: 'message', at: at(13), id: 'm4', session: 's9', repository: 'billing', agent: 'claude-code',
      text: 'The policy table moved to `policies_v2`; read that one.',
    },
  ],
};

export const NAMES: Record<string, string> = { codex: 'Codex', 'claude-code': 'Claude Code' };
export const nameOf = (agent: string) => NAMES[agent] ?? agent;
