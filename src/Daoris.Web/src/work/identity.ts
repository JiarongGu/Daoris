import i18n from '../i18n';
import type { Quest, Session } from '../api';

// The working surface's derived identity (docs/2026-09-21-working-surface-design.md §3). Pure, so
// the rail and the head cannot disagree about what a session is called — which is the whole reason
// it is a function and not two pieces of JSX.

/**
 * What a session is FOR, in one line: the quest's title where there is one, and otherwise the
 * honest fallback.
 *
 * @remarks
 * **Derived, never invented.** Naming a session by hand is deliberately not offered until two real
 * sessions cannot be told apart — a derived title is free and true, and a rename is a store column
 * plus a surface that has to earn it. The order matters: a chat that took a quest is still about
 * that quest, so the quest wins over the kind.
 *
 * With a quest id but no quest in hand — the list has not loaded, or the quest is closed and
 * filtered out — this answers with the reference rather than a name it would have to make up.
 *
 * The design also names "the conversation's first line" as a chat's identity. The record does not
 * carry one today (`note` is what the driver observed, not what the chat is about), so a chat wears
 * its kind. When the composer gives the record an opening line, this function is the only place
 * that changes — which is the point of it existing.
 */
export function sessionTitle(session: Session, quest?: Quest | null): string {
  if (quest?.title) return quest.title;
  if (session.quest) return `#${session.quest}`;
  return i18n.t(session.kind === 'chat' ? 'work.identity.conversation' : 'work.identity.session');
}
