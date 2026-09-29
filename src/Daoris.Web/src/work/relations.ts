import type { Quest, Session } from '../api';

/**
 * Who a session worked with, beyond its chain (SESS1): where its work came from and what it caused.
 *
 * @remarks
 * **Declared, never guessed** (the map's rule, MAP4): a quest names the session whose connector
 * published it (`publishedBy`), and a taken quest the question it waits on (`awaits`, D79). A session's
 * quests are never inferred from its repository and the times, which two sessions of one repository, or
 * a person publishing beside one, would make wrong. What the chain strip already draws (the ask, the
 * steps, a quest's sessions in order) is not said again here.
 */
export type Relations = {
  /** The session that published its quest, when one did — with its record, where this machine holds it. */
  askedBy: { id: string; session: Session | null } | null;
  /** The question its quest waited on, answered, when an earlier session asked it (D79's resume). */
  resumedAfter: Quest | null;
  /** The quests it published, oldest first; `question` marks the one its quest waited on. */
  asked: { quest: Quest; question: boolean }[];
};

export function relationsOf(
  session: Session, quest: Quest | null | undefined, quests: readonly Quest[], sessions: readonly Session[],
): Relations {
  const asker = quest?.publishedBy && quest.publishedBy !== session.id ? quest.publishedBy : null;
  const asked = quests
    .filter((row) => row.publishedBy === session.id)
    .sort((a, b) => a.filed.localeCompare(b.filed))
    .map((row) => ({ quest: row, question: quest?.awaits === row.id }));

  const question = quest?.awaits ? quests.find((row) => row.id === quest.awaits) ?? null : null;
  const answered = question && (question.status === 'Done' || question.status === 'Declined');

  return {
    askedBy: asker ? { id: asker, session: sessions.find((row) => row.id === asker) ?? null } : null,
    resumedAfter: answered && question.publishedBy !== session.id ? question : null,
    asked,
  };
}
