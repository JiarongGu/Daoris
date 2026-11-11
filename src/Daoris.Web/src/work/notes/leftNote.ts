import type { Quest, Session } from '../../data/api';
import { isLeft } from '../../quests/records';

/**
 * A session's note parts as the page reads them where its quest closed done with part of the work left: the plain *the quest reached
 * done* a record from before the driver said otherwise is read as the unfinished ending it is (`ended.done-left`, which the driver
 * writes itself now), so the same quest never reads finished in one place and unfinished in another. Every other part, and every note
 * of a quest that is finished, is as it was written.
 */
export function leftParts(parts: Session['noteParts'], quest: Pick<Quest, 'status' | 'answers'> | null | undefined): Session['noteParts'] {
  if (!parts || !quest || !isLeft(quest)) return parts;
  const left = (quest.answers ?? []).filter((answer) => !answer.met?.trim() && !!answer.departed?.trim()).length;
  return parts.map((part) => part.code === 'ended.done'
    ? {
      code: 'ended.done-left',
      values: { left },
      text: `The quest reached done, but it departed from ${left} of its requirements: part of the work is left.`,
    }
    : part);
}
