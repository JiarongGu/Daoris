import type { Place } from '../focus';
import type { HelpPressOutcome } from '../../person/standing';

/**
 * What one Ask Daoris turn did beyond its conversation's focus, as Daoris's doors saw it (the driver's `HelpBeyondEntry`): `asked` is a
 * press it put to the person on a card, apart from a `press` that ran.
 */
export type BeyondEntry = {
  at: string;
  act: 'search' | 'read' | 'propose' | 'press' | 'asked';
  /** The place; this machine for every workspace. */
  place: Place;
  /** A search's words, a read's path, a proposal's kind; a press's `MODULE.ROUTE`, or the connector's tool. */
  what: string | null;
  /** A press's outcome, or an asked one's card's state; none for what the connector noted, which it noted once answered. */
  outcome: HelpPressOutcome | null;
  /** How many a search's answer found, where its tool said so. */
  count?: number | null;
};
