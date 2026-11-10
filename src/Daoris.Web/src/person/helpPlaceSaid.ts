import type { TFunction } from 'i18next';
import { type Place, readPlace } from '../help/focus';

/**
 * The place an Ask Daoris press put to the person on a card named, whether it lay beyond its conversation's focus, and that focus:
 * the modules' `HelpCardPlace`, judged as the receipt and Notifications judge it.
 */
export type HelpCardPlace = { place: Place; beyond: boolean; focus: Place | null };

/** The card's place as the shell sends it, or null where it sends none or none this reads. */
export function helpCardPlaceOf(raw: unknown): HelpCardPlace | null {
  if (!raw || typeof raw !== 'object') return null;
  const shaped = raw as Record<string, unknown>;
  const place = readPlace(shaped.place);
  return place === null ? null : { place, beyond: shaped.beyond === true, focus: readPlace(shaped.focus) };
}

/** A place by its own name: this machine, a workspace or a repository. */
const named = (t: TFunction, place: Place) =>
  place.kind === 'machine' ? t('help.focus.machine') : place.kind === 'workspace' ? place.workspace : place.repository;

/**
 * The card's line for a press whose place lies outside its conversation's focus (`docs/2026-11-11-ask-scope-ux-design.md` §4); null
 * for one within it, or naming no place.
 */
export function helpPlaceSaid(t: TFunction, helpPlace: HelpCardPlace | null | undefined): string | null {
  if (!helpPlace?.beyond || helpPlace.focus === null) return null;
  return t('person.helpBeyond', { place: named(t, helpPlace.place), focus: named(t, helpPlace.focus) });
}
