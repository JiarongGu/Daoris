import { createContext } from 'react';
import type { AccountNames } from '../accountNames';
import type { NewSessionAnswer, ReasonValues } from '../say';

/**
 * Words a cooling account holds (MSG1g2, D137 §2.2): a resume asks for the account its record ran on, so while that account
 * cools the words wait for its reset, and *Go on in a new session* carries them on now, without this conversation.
 */
export type Cooling = {
  /** When the account cools until (ISO 8601), which the held line names. */
  until: string;
  /** The quest the session served, which a refusal names. */
  quest?: string | null;
  /** *Go on in a new session*; absent, the held line alone. */
  onGoOnNew?: () => void;
  /** The press is on its way. */
  pending?: boolean;
  /** What the press came to, once the route answered. */
  answer?: NewSessionAnswer | null;
};

/**
 * What the person's words to a session that parked or ended need below the conversation (MSG1f, D137 §3.1): whether it
 * is a chat, the doors a note about them offers, what a reason may name, and a cool-off that holds them (MSG1g2). Handed
 * down by the view itself, so a block deep in a turn reads it without every level between carrying it.
 */
export type Said = {
  chat: boolean;
  onSession?: (id: string) => void;
  onStartFrom?: (words: string[]) => void;
  /** *Go on with Daoris's brief* (SESSCTX1c part B): the session's words go on in a new part of it, handed Daoris's brief. */
  onGoOnBrief?: () => void;
  /** *Say it again* (COWORK1c): the person's own words that may have reached the agent, said anew. */
  onSayAgain?: (words: string[]) => void;
  reasons: ReasonValues;
  cooling?: Cooling;
  /** What the person calls each account (NOTENAME1), for a driver's note that names one. */
  names?: AccountNames;
};

export const SaidDoors = createContext<Said>({ chat: false, reasons: {} });
