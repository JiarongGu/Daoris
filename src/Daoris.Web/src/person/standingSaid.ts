import type { TFunction } from 'i18next';

/**
 * The words a card says about where its terminal stands, from the code the pipe wrote on its record: only
 * the one place the system would not tell has words, since a join from a process in an agents' job is refused before any card. Any other
 * code, the standing confirmation's reasons among them, says nothing here.
 */
export const STANDING_PLACES = ['untold'] as const;

/** The sentence for a card's `standing`, or null where the code has none. */
export function standingSaid(t: TFunction, standing: string | null | undefined): string | null {
  return standing && (STANDING_PLACES as readonly string[]).includes(standing) ? t(`person.standing.${standing}`) : null;
}
