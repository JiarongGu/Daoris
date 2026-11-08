import i18n from '../locales/i18n';
import { type Ask, HELP_REPOSITORY, type Quest, type Session, SESSION_ACTIVE } from '../data/api';

// The working surface's derived identity. Pure, so
// the rail and the head cannot disagree about what a session is called — which is the whole reason
// it is a function and not two pieces of JSX.

/**
 * What a list calls a quest: its short title, as the service
 * answers it (the publisher's, or the name the service read from the quest's own words), else its title, from a host
 * older than the field. One helper, so no two places name one quest differently; the page never shortens a title itself,
 * since content is shown as it is.
 */
export function questName(quest: Pick<Quest, 'title' | 'short'>): string {
  return quest.short?.trim() ? quest.short.trim() : quest.title;
}

/**
 * What a list or a head calls an ask: its asker's short title where it has one, as a quest's name
 * is (`questName`), else the first line of its sentence, which an ask from before the field, or one given none, is called
 * by. The page never cuts the sentence itself: content is shown as it is.
 */
export function askName(ask: Pick<Ask, 'sentence' | 'short'>): string {
  return ask.short?.trim() ? ask.short.trim() : ask.sentence.split('\n', 1)[0].trim();
}

/**
 * Whether a quest's name leaves part of its title unsaid: then a page that heads with the name shows the
 * whole title once, under its head, and otherwise it says the title once, in its head.
 */
export function shortened(quest: Pick<Quest, 'title' | 'short'> | null | undefined): boolean {
  return Boolean(quest && questName(quest) !== quest.title);
}

/**
 * What a session is FOR, in one line: the quest's name (`questName`) where there is one, and otherwise the
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
 * A chat's identity is the conversation's first line: `opening`, the first thing the person said,
 * read from this machine's own record. It never rides the session record, which travels, so where this machine holds no record — a
 * teammate's session, a browser — a chat still wears its kind.
 *
 * An intake is opened as a chat, so every build reads it as a session nothing plans from,
 * and it is not a conversation: it serves an ask, and says so by the ask's reference.
 */
export function sessionTitle(session: Session, quest?: Quest | null, opening?: string | null): string {
  if (quest?.title) return questName(quest);
  if (session.quest) return `#${session.quest}`;
  if (session.ask) return i18n.t('work.intake.title', { ask: session.ask });
  // A reviewer's record is a chat only by the way it is opened: it reads another session's work once.
  if (isOpinion(session)) return i18n.t('opinion.session.title');
  // A partner's record serves a lead as a helper's does, so a host before the work draws it a helper; one that sends the work draws it
  // a partner.
  if (isPartner(session)) return i18n.t('work.identity.partner');
  // A supervisor's turn serves the session it decides for, so a host before the work draws it a helper too.
  if (isSupervise(session)) return i18n.t('work.identity.supervise');
  // So is a helper's: it answers another session's one question, and its rail row says whose.
  if (isHelper(session)) return i18n.t('work.identity.helper');
  if (isHelp(session) && !opening) return i18n.t('help.title');
  if (session.kind === 'chat' && opening) return opening;
  return i18n.t(session.kind === 'chat' ? 'work.identity.conversation' : 'work.identity.session');
}

/** Whether a session is an intake: the one kind of record that names an ask. */
export const isIntake = (session: Session): boolean => Boolean(session.ask);

/** Whether a session is a second opinion's reviewer: the one kind of record that names an opinion. */
export const isOpinion = (session: Session): boolean => Boolean(session.opinion);

/**
 * Whether a session is a helper: a record that names a session it serves. Like a reviewer, it
 * is a chat only by the way it is opened, one turn and no words: `sessionTitle` names it and the frame's `oneTurn` offers no box.
 * A reviewer serves the work it reads too, so the opinion mark is read first: a reviewer is never a helper.
 */
export const isHelper = (session: Session): boolean => !session.opinion && Boolean(session.serves);

/**
 * Whether the host that answered a record says its work is a partner's: a naming only, read from the
 * pushed `work` where a host sends it and false where it sends none, so a partner is drawn by today's predicates, as a
 * helper. A partner's record also serves, so `isHelper` stays true for it and the readers of that, the composer's one turn (`WorkFrame`'s
 * `oneTurn`) and the acts that take words (`acts.ts`), keep refusing it exactly as they do: the naming repair offers a partner no box and
 * no act it is not offered now.
 */
export const isPartner = (session: Session): boolean => session.work === 'partner';

/** Whether a session is a supervisor's one read-only turn, as the host's work says. */
export const isSupervise = (session: Session): boolean => session.work === 'supervise';

/**
 * The sessions a record names as its own: the one it continues (`from`) and the one
 * a helper helps (`serves`), each where the list holds it. Null where the record names none, and where the list does not hold
 * the one it names (deleted, or not this machine's): then the head says its id and the rail claims nothing.
 */
export function linkedSessions(
  session: Session, byId: ReadonlyMap<string, Session>,
): { continues: Session | null; helps: Session | null } {
  return {
    continues: session.from ? byId.get(session.from) ?? null : null,
    helps: isHelper(session) && session.serves ? byId.get(session.serves) ?? null : null,
  };
}

/** Whether a session is Ask Daoris's conversation, by the repository it is recorded in. */
export const isHelp = (session: Session): boolean => session.repository === HELP_REPOSITORY;

/**
 * Whether a session may lead a piece another agent writes (the driver's `StartAdmission.LeadsPiece`): a working
 * session in a tree of its own, driven or a conversation, never a helper, a partner, a reviewer, an intake nor Ask Daoris's. The one rule
 * the head's ⋯ and the Pieces section offer the ask by.
 */
export const leadsPieces = (session: Session): boolean =>
  Boolean(session.tree) && !session.serves && !isOpinion(session) && !isIntake(session) && !isHelp(session);

/**
 * Whether a session may be asked for a partner now: it leads pieces and has not ended, since an ended session has no running
 * work to receive the piece (the driver's `PartnerJudge` refuses it the same way). Its review and its helper's ask do not
 * wait on this, so they stay on `leadsPieces`.
 */
export const takesPartner = (session: Session): boolean => leadsPieces(session) && SESSION_ACTIVE.has(session.state);

/**
 * Which machine holds this session — or null when it is the deployment's own.
 *
 * @remarks
 * **Already in the record.** Daoris is multi-machine by
 * construction, and the session feed keys a mirrored record by `origin/id`, where the origin
 * is the key's own identity: whose key, on which machine. Nothing else about the record says where
 * it ran, and nothing needs to — a local id is eight hex characters and never carries a separator.
 *
 * **Silence means here**, the same rule `MetaLine` follows: a row that stamped every session with
 * this machine's name would spend the rail's scarcest space saying the unsurprising thing. What is
 * worth a person's attention is the session that is somewhere else.
 */
export function sessionOrigin(session: Pick<Session, 'id'>): string | null {
  const separator = session.id.indexOf('/');
  return separator > 0 ? session.id.slice(0, separator) : null;
}

/**
 * A session tree's short name — its last segment, or null where there is no tree.
 *
 * @remarks
 * **Daoris owns where trees live** (`trees/<workspace>/<repository>/<branch>` under the home),
 * which is what makes the last segment meaningful rather than a guess: it is the branch the tree
 * is cut for, and it is the part a person recognises. The rail has no room for the path and the
 * path is machine-local material besides — a name is the right amount to show there.
 *
 * Null covers both absences and they mean different things, neither of them an error: the session
 * runs in the repository's registered root, or the reader is not the machine that ran it and is
 * rightly told no path at all.
 */
export function treeName(tree: string | null | undefined): string | null {
  const segments = (tree ?? '').split(/[/\\]/).filter((segment) => segment.trim().length > 0);
  return segments.length ? segments[segments.length - 1] : null;
}

/** Two paths as the same place. Separators differ by platform; Windows does not care about case. */
const samePath = (a: string, b: string) =>
  a.replace(/[\\/]+$/, '').replace(/\\/g, '/').toLowerCase()
  === b.replace(/[\\/]+$/, '').replace(/\\/g, '/').toLowerCase();

const escapeRegExp = (text: string) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/**
 * A line naming paths — a tool's title, where it acted — with every path inside the session's tree
 * shown relative to it, or the line unchanged with no tree in hand.
 *
 * @remarks
 * A harness names what it touched by absolute path, and a card truncated `Edit D:/…/trees/…` before
 * the part a reader wanted. **Display only**: the record keeps what the wire said,
 * and a message's own words are content, never rewritten. Separators and case are ignored as
 * {@link ownTree} ignores them, and the tree must end at a separator — a sibling that only shares its
 * prefix is somewhere else.
 */
export function inTree(line: string, tree: string | null | undefined): string {
  const root = (tree ?? '').replace(/[\\/]+$/, '');
  if (!root || !line) return line;
  const pattern = root.split(/[\\/]+/).map(escapeRegExp).join('[\\\\/]+');
  return line.replace(new RegExp(`${pattern}[\\\\/]+`, 'gi'), '');
}

/**
 * The name of a tree this session opened **for itself**, or null when it is working in the
 * repository's registered checkout.
 *
 * @remarks
 * **`tree` being set is not the question, and assuming it is would be a bug.**
 * The ledger resolves an unstated tree to the registered root before it records one (a caller
 * that names the root and one that says nothing must land on the same lock key), so an ordinary
 * conversation carries a `tree` — the root's own path. A surface reading "has a tree" as "has its
 * own tree" would tell every session it is somewhere special, and the rail would say `in engine` under the
 * heading `engine`.
 *
 * So the question is *which* tree, answered against the registration. With no root in hand — a
 * browser is told neither path — the honest answer is null: nothing is claimed.
 */
export function ownTree(
  session: Session, root: string | null | undefined,
): string | null {
  if (!session.tree) return null;
  if (!root || samePath(session.tree, root)) return null;
  return treeName(session.tree);
}
