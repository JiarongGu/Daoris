import type { AtWorkExchange } from '../atWork/types';
import type { ChannelItem } from '../channel';

// A line whose agent asked a helper, whose work a reviewer read, and whose channel took a message from another part of its ask.

export const at = (minute: number) => `2026-11-19T10:${String(minute).padStart(2, '0')}:00Z`;

/** A helper the person asked, answered and taken in: its session is the helper's. */
export const HELPER: AtWorkExchange = {
  id: 'helper:h1', part: 's1', expects: 'answer', to: 'codex', delivery: 'taken', outcome: 'taken-in', holds: false,
  at: at(2), answered: at(6), session: 'h-sess', agent: 'codex',
};

/** A second opinion read on the line's work and handed to it; the read keeps no moment for it. */
export const OPINION: AtWorkExchange = {
  id: 'opinion:o1', part: 's1', expects: 'opinion', to: 's1', delivery: 'taken', outcome: 'taken-in', holds: false,
  session: 'r-sess', agent: 'claude',
};

/** A piece asked of a partner, not yet started. */
export const PIECE: AtWorkExchange = {
  id: 'piece:p1', part: 's1', expects: 'piece', to: 'gemini', delivery: 'kept', outcome: 'asked', holds: true, at: at(4),
};

/** One message on the line's channel, from another repository's part. */
export const CHANNEL_WORDS: ChannelItem[] = [
  { kind: 'message', id: 'm1', at: at(4), repository: 'db', agent: 'claude', entry: 'ask:a1/db', text: 'The schema moved: read `users.v2`.' },
  { kind: 'line', at: at(5), code: 'quest-taken', quest: 'q2' },
];
