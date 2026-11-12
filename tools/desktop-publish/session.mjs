import { insideFixtures } from './gates.mjs';

/** The variable a session's terminal carries, Ask Daoris's among them: the driver's `DriverChoicesCommand.SessionVariable`. */
export const SESSION_VARIABLE = 'DAORIS_SESSION_ID';

/**
 * Why a publish or a stage into `to` is refused from this terminal, or null. A session's terminal never stages or publishes
 * into an install: a stage drains it, nothing new starting there until it installs, so whether and when an install takes a
 * build is the person's. A rehearsal's install under `_fixtures` is no one's, and is published as before.
 */
export function sessionRefusal(to, repoRoot, env = process.env) {
  if (!env[SESSION_VARIABLE] || insideFixtures(to, repoRoot)) return null;
  return `desktop-publish: this terminal is a session's (\`${SESSION_VARIABLE}\`), and a session never stages or publishes into an install:\n`
    + '  a stage drains the install, nothing new starting there until it installs, so whether and when it takes a build is the\n'
    + '  person\'s, from their own terminal. Nothing was done.';
}
