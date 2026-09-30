import i18n from '../i18n';
import type { Quest, Session } from '../api';
import { sessionTitle } from './identity';

/** A session with its hands on Daoris's browser, as a person reads it (BRW8). */
export type BrowserDriver = { id: string; name: string };

/**
 * Who is driving Daoris's browser: the driver's ids (`drivingBrowser` in its state — the running
 * sessions it handed a server that drives the browser), named from the records the page holds.
 *
 * @remarks
 * **A name the rail would recognise**: the repository, then what the session is for, by the one namer
 * the rail and the head share (`sessionTitle`). A session the page has no record of yet — the list has
 * not caught up with the tick — is named by its id rather than left out: an unnamed pair of hands is
 * still a pair of hands.
 *
 * Pure, so the strip's chip and the Settings row cannot name one session two ways.
 */
export function browserDrivers(
  ids: readonly string[] | undefined, sessions: readonly Session[], quests: readonly Quest[],
): BrowserDriver[] {
  return (ids ?? []).map((id) => {
    const session = sessions.find((candidate) => candidate.id === id);
    if (!session) return { id, name: i18n.t('browser.driving.unknown', { id: id.slice(0, 8) }) };
    const quest = session.quest ? quests.find((candidate) => candidate.id === session.quest) : null;
    return { id, name: `${session.repository} · ${sessionTitle(session, quest)}` };
  });
}
