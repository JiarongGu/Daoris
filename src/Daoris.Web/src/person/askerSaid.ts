import type { TFunction } from 'i18next';

/**
 * What sent a terminal's press: the process the system names for the pipe's client, and, where the press proved the key Daoris handed a
 * session's start, that session and whether it is Ask Daoris.
 */
export type PipeAsker = { process?: string | null; pid?: number | null; session?: string | null; help?: boolean | null };

/**
 * Who asks, in a card's words: Ask Daoris, or the session, where its press proved that session's key; else the process, said as the
 * record's for a press waiting again after a restart, which a process of the account could have written.
 */
export function askerSaid(t: TFunction, asker: PipeAsker | null | undefined, restored = false): string {
  if (asker?.help) return t('person.asker.help');
  if (asker?.session) return t('person.asker.session', { session: asker.session });
  if (asker?.process && asker.pid != null) {
    return t(restored ? 'person.restored' : 'person.asker.process', { process: asker.process, pid: asker.pid });
  }
  if (asker?.pid != null) return t(restored ? 'person.restoredPid' : 'person.asker.pid', { pid: asker.pid });
  return t(restored ? 'person.restoredUnknown' : 'person.asker.unknown');
}
