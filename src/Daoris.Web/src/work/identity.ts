import i18n from '../i18n';
import { HELP_REPOSITORY, type Quest, type Session } from '../api';

// The working surface's derived identity (docs/2026-09-21-working-surface-design.md §3). Pure, so
// the rail and the head cannot disagree about what a session is called — which is the whole reason
// it is a function and not two pieces of JSX.

/**
 * What a list calls a quest (SESSUX1j, D126 §9, the session management design §6.2): its short title, as the service
 * answers it (the publisher's, or the name the service read from the quest's own words), else its title, from a host
 * older than the field. One helper, so no two places name one quest differently; the page never shortens a title itself,
 * since content is shown as it is (platform language §4).
 */
export function questName(quest: Pick<Quest, 'title' | 'short'>): string {
  return quest.short?.trim() ? quest.short.trim() : quest.title;
}

/**
 * Whether a quest's name leaves part of its title unsaid (UX7c, D152 §7): then a page that heads with the name shows the
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
 * The design also names "the conversation's first line" as a chat's identity, and since RAIL1 it is
 * here: `opening`, the first thing the person said, read from this machine's own record. It never
 * rides the session record, which travels (D47 §4), so where this machine holds no record — a
 * teammate's session, a browser — a chat still wears its kind.
 *
 * An intake is opened as a chat, so every build reads it as a session nothing plans from (INT4b),
 * and it is not a conversation: it serves an ask, and says so by the ask's reference (INT4g).
 */
export function sessionTitle(session: Session, quest?: Quest | null, opening?: string | null): string {
  if (quest?.title) return questName(quest);
  if (session.quest) return `#${session.quest}`;
  if (session.ask) return i18n.t('work.intake.title', { ask: session.ask });
  // A reviewer's record is a chat only by the way it was opened (XAGENT1c): it reads another session's work once.
  if (isOpinion(session)) return i18n.t('opinion.session.title');
  if (isHelp(session) && !opening) return i18n.t('help.title');
  if (session.kind === 'chat' && opening) return opening;
  return i18n.t(session.kind === 'chat' ? 'work.identity.conversation' : 'work.identity.session');
}

/** Whether a session is an intake (INT4b): the one kind of record that names an ask. */
export const isIntake = (session: Session): boolean => Boolean(session.ask);

/** Whether a session is a second opinion's reviewer (XAGENT1c, D155 point 11): the one kind of record that names an opinion. */
export const isOpinion = (session: Session): boolean => Boolean(session.opinion);

/** Whether a session is Ask Daoris's conversation (HELP1a, D89), by the repository it is recorded in. */
export const isHelp = (session: Session): boolean => session.repository === HELP_REPOSITORY;

/**
 * Which machine holds this session — or null when it is the deployment's own.
 *
 * @remarks
 * **Already in the record, and D55 is what asks for it on a row.** Daoris is multi-machine by
 * construction (D47), and the session feed keys a mirrored record by `origin/id`, where the origin
 * is the key's own identity: whose key, on which machine. Nothing else about the record says where
 * it ran, and nothing needs to — a local id is eight hex characters and never carries a separator.
 *
 * **Silence means here**, the same rule `MetaLine` follows: a row that stamped every session with
 * this machine's name would spend the rail's scarcest space saying the unsurprising thing. What is
 * worth a person's attention is the session that is somewhere else.
 */
export function sessionOrigin(session: Session): string | null {
  const separator = session.id.indexOf('/');
  return separator > 0 ? session.id.slice(0, separator) : null;
}

/**
 * A session tree's short name — its last segment, or null where there is no tree.
 *
 * @remarks
 * **Daoris owns where trees live** (D51 §2: `trees/<workspace>/<repository>/<branch>` under the home),
 * which is what makes the last segment meaningful rather than a guess: it is the branch the tree
 * was cut for, and it is the part a person recognises. The rail has no room for the path and the
 * path is machine-local material besides (D51 §9) — a name is the right amount to show there.
 *
 * Null covers both absences and they mean different things, neither of them an error: the session
 * runs in the repository's registered root, or the reader is not the machine that ran it and was
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
 * shown relative to it, or the line as it was with no tree in hand.
 *
 * @remarks
 * A harness names what it touched by absolute path, and a card truncated `Edit D:/…/trees/…` before
 * the part a reader wanted (CONV3's look). **Display only**: the record keeps what the wire said
 * (D52), and a message's own words are content, never rewritten. Separators and case are ignored as
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
 * **`tree` being set is not the question, and assuming it was is a bug the real window caught.**
 * The ledger resolves an unstated tree to the registered root before it records one (D51: a caller
 * that names the root and one that says nothing must land on the same lock key), so an ordinary
 * conversation carries a `tree` — the root's own path. A surface reading "has a tree" as "has its
 * own tree" told every session it was somewhere special, and the rail said `in engine` under the
 * heading `engine`.
 *
 * So the question is *which* tree, answered against the registration. With no root in hand — a
 * browser is told neither path (D48 §7, D51 §9) — the honest answer is null: nothing is claimed.
 */
export function ownTree(
  session: Session, root: string | null | undefined,
): string | null {
  if (!session.tree) return null;
  if (!root || samePath(session.tree, root)) return null;
  return treeName(session.tree);
}
