import i18n from '../i18n';
import type { Quest, Registration, Session } from '../api';
import { sessionTitle } from './identity';
import type { Attention } from './AttentionRow';

/**
 * What is waiting on a person, oldest first within each kind (design §4).
 *
 * @remarks
 * **Parked first, because a parked session is holding a working tree while it waits.** Then the
 * quests nobody here can take — which sit forever and which nothing else surfaces, since the quest
 * list shows them among every other open one.
 *
 * **A quest is unanswerable when this deployment has no registration for its receiver.** The
 * publish door already refuses a quest addressed to a non-adopter, so the way one comes to exist is
 * afterwards: the receiver retired, or the registration never arrived here. Either way no agent
 * will pull it, and "who cannot be asked" is the same question as "who can" (the Projects view
 * already argues this for repositories).
 *
 * **The design's middle category — finished work nobody has looked at — is deliberately absent.**
 * Nothing records that anybody looked, so any row here would be a guess. It arrives with the
 * *viewed* mark SURF6 brings, and the band says so rather than leaving the gap silent.
 */
export function needsAPerson(
  sessions: readonly Session[],
  quests: readonly Quest[],
  registry: readonly Registration[],
): Attention[] {
  const parked = sessions
    .filter((session) => session.state === 'awaiting-person')
    .map((session): Attention => ({
      id: session.id,
      kind: 'parked',
      title: sessionTitle(session, quests.find((quest) => quest.id === session.quest)),
      repository: session.repository,
      since: session.updated,
      detail: session.note,
    }));

  const registered = new Set(registry.map((row) => row.repository.toLowerCase()));
  const unanswerable = quests
    .filter((quest) => quest.status === 'Open' && !registered.has(quest.to.toLowerCase()))
    .map((quest): Attention => ({
      id: quest.id,
      kind: 'unanswerable',
      title: quest.title,
      repository: quest.to,
      since: quest.filed,
      detail: i18n.t('work.attention.unanswerableWhy', { repository: quest.to }),
    }));

  const oldestFirst = (a: Attention, b: Attention) => a.since.localeCompare(b.since);
  return [...parked.sort(oldestFirst), ...unanswerable.sort(oldestFirst)];
}
