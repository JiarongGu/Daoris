import { type Carry, NO_CARRY } from '../../compose/carry';
import type { Place } from '../focus';
import { type ChatMessage } from '../../work/conversation/conversation';

/** How often the list is asked again while words to an ended conversation wait for it to go on. */
export const GOING_ON_POLL = 1500;

/**
 * How far before its send a record's words may be stamped and still be the words sent: the record's time is the
 * driver's, the send's the page's, on one machine.
 */
export const SAID_SKEW = 5000;

/** The words that open a conversation, with their files, and the conversation that takes them once known. */
export type FirstWords = {
  words: ChatMessage;
  files: File[];
  session?: string;
  /**
   * Where the page stopped waiting on the start: the conversations listed as the words were said, when it stopped,
   * and its sentence, said if nothing took them.
   */
  late?: { before: string[]; at: number; why: string };
  /** The workspace the start is in, read at its press: only a conversation of that workspace takes them. */
  circle?: string;
};

/** Words on their way to one conversation, shown the moment they are sent until it holds them. */
export type Sent = {
  /** Which send, so its answer settles its own words. */
  n: number;
  words: ChatMessage;
  files: File[];
  session: string;
  /** When they were sent, on the page's clock. */
  at: number;
  /** The page stopped waiting on the send (the bridge's TIMEOUT): the machine may still take them. */
  late: boolean;
};

/** What the person is writing to one conversation: its words and its files. */
export type Draft = { text: string; carry: Carry };
export const NO_DRAFT: Draft = { text: '', carry: NO_CARRY };
/** The draft of a new conversation, kept apart from every conversation's own. */
export const NEW_DRAFT = 'new';

/**
 * Words handed to Ask Daoris from another door, once per `id`: a question, sent as a typed one is (the palette's, the
 * setup guide's), or a draft, put in the box for the person to finish (*Ask Daoris about it*), with the focus its conversation
 * takes where its door chose one (*Make it a task with Daoris…*'s repository).
 */
export type AskOpening = { text: string; id: number; draft?: boolean; focus?: Place };
