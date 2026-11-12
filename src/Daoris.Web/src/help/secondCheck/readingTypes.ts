/** How a reading is shown, from its report alone: a read one with any limit of its evidence is partial, never clean. */
export const READING_STATES = ['queued', 'running', 'read', 'partial', 'left', 'unread', 'disabled'] as const;
export type ReadingState = (typeof READING_STATES)[number];

/** One finding of a reading, a claim and never a fact, with the person's *Seen* and its delivery to the conversation. */
export type ReadingFinding = {
  index: number;
  severity: 'flag' | 'note';
  certainty: 'sure' | 'likely' | 'unsure';
  says: string;
  call: string;
  instruction: string;
  seen: string | null;
  told: { word: string | null; at: string } | null;
};

/** Who read the range: its agent, the model asked and the model its harness reported, and the tokens it reported. */
export type ReadingReader = { agent: string; model: string | null; ran: string | null; tokens: number | null };

/** One range of a conversation's turns as the second check keeps it, drawn as one line under the last turn it covers. */
export type Reading = {
  range: string;
  state: ReadingState;
  opened: string;
  closed: string | null;
  last: string | null;
  turns: string[];
  limits: { code: string; call: string | null }[];
  why: string | null;
  waiting: string | null;
  reader: ReadingReader | null;
  independence: 'another-maker' | 'not-independent' | 'not-counted' | 'unknown' | null;
  findings: ReadingFinding[];
  more: number;
  leave: string | null;
  reasons: { says: string; source: string }[];
};

/** The check as the panel's chip says it: whether it is on, the conversation's ranges pending, and today's spend on the machine. */
export type ReadingsCheck = { on: boolean; pending: number; today: { readings: number; tokens: number; unknown: number } };
