import type { SaidWord } from '../data/api';

/**
 * The person's words waiting on a record, as the service answers them to this machine, one word each (ONEWAY1m: a park's answer is
 * read from these alone). Ids and moments are fixed, so a record built twice is equal.
 */
export const said = (...texts: string[]): SaidWord[] =>
  texts.map((text, at) => ({ id: `w${at + 1}`, text, at: '2026-10-03T08:00:00Z', files: [] }));
