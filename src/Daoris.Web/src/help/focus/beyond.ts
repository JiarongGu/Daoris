import type { Place } from '../focus';

/** What one Ask Daoris turn did beyond its conversation's focus, as Daoris's doors saw it (the driver's `HelpBeyondEntry`). */
export type BeyondEntry = {
  at: string;
  act: 'search' | 'read' | 'propose' | 'press';
  /** The place; this machine for every workspace. */
  place: Place;
  /** A search's words, a read's path, a proposal's kind; a press's `MODULE.ROUTE`, or the connector's tool. */
  what: string | null;
  /** A full-access press's outcome; none for what the connector noted, which it noted once answered. */
  outcome: 'running' | 'done' | 'failed' | null;
  /** How many a search's answer found, where its tool said so. */
  count?: number | null;
};
