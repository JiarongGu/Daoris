// A quest's strikes, counted from this machine's session records as the driver counts them (RETRY1b; D58, as D104, D125 and
// ROSTER1b amend it), so `daoris driver retry <quest>` marks the quest where the driver's planner stands. A mark at the
// strike limit was right only on a first park: a quest retried once and parked again has more failures than the limit, and
// a mark at the limit left it parked (the install, 2026-10-04).
//
// 🔴 A TWIN with the driver's `ServiceClient.ReadStrikes` (`twins.md`): `strikes.test.ts` holds this module's table and
// parses the driver's theory `StrikeTests.A_record_strikes_as_the_cli_counts_it` to hold it, cell for cell.
//
// Pure: the records arrive from the service client (`service.ts`'s `sessionRecords`), which the dispatcher's row hands in,
// so nothing here or in `driverconfig.ts` reaches a network.

import { sameName } from './casefold.ts';

/**
 * The note codes that say a failure was its account's (ROSTER1b): the agent or its provider refused the account's sign-in
 * or its credential, which is no strike. A deliberate copy of the driver's `NoteCodes.AccountsOwn`, in its order, held to it
 * code for code by `strikes.test.ts`.
 */
export const ACCOUNTS_OWN: readonly string[] = ['account.refused', 'account.refused-own', 'account.signed-out', 'account.signed-out-own'];

/** What reading this machine's session records came to: the list its host answered, or why there is none, in words. */
export type SessionRecords = { records: unknown[] } | { unread: string };

/** Reads this machine's session records. The `driver` row hands in the service client's; a test hands in its own. */
export type RecordsReader = () => Promise<SessionRecords>;

/**
 * How many sessions have failed on a quest here, from the records `/api/sessions?includeClosed=true` answers, as the
 * planner counts them: each `failed` record, bar one an account's limit made (`limit`, D125) and one whose note carries an
 * account's own line (ROSTER1b), and each stop that was not the person's (`interrupted`, D104). A teammate's record, keyed
 * `origin/id`, says nothing of this machine's attempts. Ids and states compare in any case (a quest's id as the driver's
 * `OrdinalIgnoreCase` does, through `casefold.ts`, CASEFOLD1), codes exactly, and a flag is only JSON `true`. Null where
 * the records are not a list.
 */
export function failuresOf(records: unknown, quest: string): number | null {
  if (!Array.isArray(records)) return null;
  return records.filter((record) => isObject(record)
    && !(typeof record.id === 'string' && record.id.includes('/'))
    && typeof record.quest === 'string' && record.quest.length > 0 && sameName(record.quest, quest)
    && isStrike(record)).length;
}

function isStrike(record: Record<string, unknown>): boolean {
  const state = typeof record.state === 'string' ? record.state.toLowerCase() : null;
  if (state === 'failed') return record.limit !== true && !accountRefused(record);
  return state === 'stopped' && record.interrupted === true;
}

/** Whether a record's note carries an account's own line, by the code the driver wrote, never by its words (ROSTER1b). */
function accountRefused(record: Record<string, unknown>): boolean {
  return Array.isArray(record.noteParts)
    && record.noteParts.some((part) => isObject(part) && typeof part.code === 'string' && ACCOUNTS_OWN.includes(part.code));
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
