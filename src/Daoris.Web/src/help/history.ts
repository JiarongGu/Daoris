/**
 * One of Ask Daoris's conversations as its history lists it (ASKHIST1): the driver's `HELP_CONVERSATIONS` row, field for
 * field what `daoris-driver help list --json` prints. Kept on this machine only: its words are this machine's record, and its
 * name and pin a file beside them.
 */
export type HelpConversationRow = {
  session: string;
  /** What the list calls it: the person's name for it, else its first question. */
  title: string;
  name: string | null;
  opening: string | null;
  /** A line of what it was about: the first line of Ask Daoris's last answer in it. */
  about: string | null;
  created: string;
  /** When it was last spoken in. */
  last: string;
  pinned: string | null;
  live: boolean;
  /** Whether words written to it once it ended go on in its own conversation; else a new one starts from its words. */
  resumable: boolean;
  /** The earlier conversation it started from, and what it was handed of it. */
  from: string | null;
  handed: string | null;
  /** Where a search found its words. */
  found: string | null;
};

/** A name's longest: a title, as the driver keeps it (`HelpConversations.NameLimit`). */
export const HELP_NAME_LIMIT = 80;
