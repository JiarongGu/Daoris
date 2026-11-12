import type { Quest, Session } from '../../data/api';
import { isLeft } from '../../quests/records';

/** A note's plain done, on a quest closed with a departure, read as the unfinished ending the driver now writes (`ended.done-left`). */
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
