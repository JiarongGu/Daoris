import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';
import { homeEntry } from '../../src/Daoris.Cli/src/platform/home.ts';

/**
 * Say who is closing the desktop before its window is closed from outside: the windows system reports that close as the
 * person's, so the application reads this file (`StopAsk`) as it stops and says the terminal's or the session's ask in `app.stopped`.
 * Left for the pids named, for a short while; with no home there is nothing to say it in, and the stop reads as a window close.
 *
 * @param home - the home of the shell being closed, from its run record.
 * @param pids - the processes about to be closed.
 */
export function askStop(home, pids, env = process.env) {
  if (!home || pids.length === 0) return false;
  const record = { pids, by: 'desktop-tool', at: new Date().toISOString() };
  if (/^[0-9a-f]{4,64}$/.test(env.DAORIS_SESSION_ID ?? '')) record.session = env.DAORIS_SESSION_ID;
  const file = homeEntry(home, 'stop-ask');
  mkdirSync(dirname(file), { recursive: true });
  writeFileSync(file, `${JSON.stringify(record)}\n`);
  return true;
}
