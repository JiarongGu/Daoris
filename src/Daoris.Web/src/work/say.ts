import type { Session } from '../api';
import { type AccountsAnswer, agentOf } from '../settings/accounts';
import { toolOf, type ToolDoor } from '../tools';
import { answeredPark, SESSION_ACTIVE } from '../ui';

// The box on a session's page (MSG1f, D137 §5.1): which box a session is offered, by what its record says and what a word
// said now would do; the sentences for what takes no words and for why words did not go on in their session; and the
// native door's bound. Pure, so every session a person could attend is an argument.

/**
 * What a word said now to a session would do, as `SESSION_QUEUE` answers it (MSG1d, D137 §5.3): when it reaches the
 * session (`next-step`, `turn-end` or `resume`, or null where it is taken at once or the door is not known yet), and why
 * nothing takes it, by a code, where nothing does.
 */
export type Reach = { reaches: string | null; why: string | null };

/**
 * The box a session is offered.
 * - `chat`: a live conversation's own composer, its turns and its endings (CONV4); and an ended one's, saying it ended,
 *   until the module answers for it.
 * - `steer`: a working driven session whose door listens (D136): words at its next step or its turn's end.
 * - `say`: words the same session goes on with (D137 §2.2): a park's `answer`; `goOn`, a session that ended or a park
 *   already answered, whose next word joins the first (§2.4); or one `ending`, whose words wait for its record to end and
 *   then reopen it.
 * - `line`: nothing takes words, and the line says why, by the code.
 * - `none`: nothing to offer and nothing to say: no session, a live intake whose head says it, or nothing known yet.
 */
export type Box =
  | { kind: 'none' }
  | { kind: 'chat' }
  | { kind: 'steer' }
  | { kind: 'say'; mode: 'answer' | 'goOn' | 'ending' }
  | { kind: 'line'; why: string };

/** What decides a session's box: its record, whether it is this machine's, and what the module answered. */
export type BoxFacts = {
  session: Pick<Session, 'kind' | 'state' | 'answer'> | null;
  /** The record is this machine's: a teammate's runs on their machine and account (D47 §6). */
  here: boolean;
  /** An intake, answered through its ask (INT4h). */
  intake: boolean;
  /** A driven session's inbox is open here (SESS3, D136). */
  listening: boolean;
  /** `SESSION_QUEUE`'s answer, or undefined until it answers. */
  reach?: Reach;
};

/**
 * The box a session is offered (D137 §5.1): on every session that takes words, and the line saying why on the rest.
 *
 * @remarks
 * - **What the page knows first.** A teammate's record never takes words here, and a live intake's head already says why
 *   it takes none, so neither waits for the module.
 * - **The module's code draws the line**, whatever the record says: it judges what the page cannot (a quest that went on
 *   in a later session, a record gone).
 * - **A park is the answer before the module answers**, as it was: its record says it waits on the person. Every other
 *   box that goes on with words waits for the module's `resume`, since a session that ended may yet be one nothing takes
 *   words for, and nothing known is nothing claimed.
 */
export function boxOf({ session, here, intake, listening, reach }: BoxFacts): Box {
  if (!session) return { kind: 'none' };
  if (!here) return { kind: 'line', why: 'teammate' };
  const live = SESSION_ACTIVE.has(session.state);
  if (intake) return live ? { kind: 'none' } : { kind: 'line', why: 'intake' };
  if (reach?.why) return { kind: 'line', why: reach.why };
  const resumes = reach?.reaches === 'resume';

  if (session.kind === 'chat') {
    return live || !resumes ? { kind: 'chat' } : { kind: 'say', mode: 'goOn' };
  }
  if (listening) return { kind: 'steer' };
  if (session.state === 'awaiting-person') return { kind: 'say', mode: answeredPark(session) ? 'goOn' : 'answer' };
  if (!resumes) return { kind: 'none' };
  return { kind: 'say', mode: live ? 'ending' : 'goOn' };
}

/** Whether a box takes words: what *Send back…* opens (D137 §5.1), and nothing else. */
export function takesWords(box: Box): boolean {
  return box.kind === 'chat' || box.kind === 'steer' || box.kind === 'say';
}

type Translate = (key: string, options?: Record<string, unknown>) => string;

/** The sentence for each code nothing takes words for (MSG1d), by its key. */
const NEVER: Record<string, string> = {
  'teammate': 'work.say.teammate',
  'stood-down': 'work.say.stoodDown',
  'intake': 'work.say.intake',
  'help': 'work.say.help',
  'superseded': 'work.say.superseded',
  'not-found': 'work.say.notFound',
};

/**
 * Why a session takes no words, in the page's own words (D137 §5.1): a code the module answered, or one the page knows.
 * A code a newer host answers that this page has no sentence for is named rather than left unsaid.
 */
export function neverSentence(t: Translate, code: string, { quest }: { quest?: string | null }): string {
  const key = NEVER[code];
  return key ? t(key, { quest: quest ?? '' }) : t('work.say.takesNone', { code });
}

/**
 * When the account a session's words wait for cools until (MSG1g2, D137 §2.2): MSG1g asks a resume for the account its record
 * ran on, so where that account cools the words wait for its reset. Read from the facts `ACCOUNTS` answers: by the agent that
 * owns the door's accounts (AGT7), the tool's own sign-in where the record names no account, and only while it has not
 * passed. Null where it is ready, where the page does not know its agent, and from a shell older than the facts.
 */
export function coolingFor(
  session: Pick<Session, 'adapter' | 'profile'>, doors: readonly ToolDoor[], answer: AccountsAnswer | null | undefined, now: Date,
): string | null {
  const door = doors.find((each) => each.harness === session.adapter);
  const agent = agentOf(answer, door ? toolOf(door) : session.adapter);
  if (!agent) return null;
  const accounts = Array.isArray(agent.accounts) ? agent.accounts : [];
  const cooling = session.profile
    ? accounts.find((account) => account.name === session.profile)?.cooling
    : agent.own?.cooling;
  return cooling && Date.parse(cooling.until) > now.getTime() ? cooling.until : null;
}

/**
 * What *Go on in a new session* came to, as `SESSION_GO_ON_NEW` answers it (MSG1g, D137 §2.2): kept for the driver's next
 * look (`sent`), or refused by a code, with the driver's sentence beside it in the terminal's words.
 */
export type NewSessionAnswer = { sent: boolean; why: string | null; message: string };

/** The sentence for each code the route refuses by that is its own, by its key; §2.2's nevers are the box's (`NEVER`). */
const NEW_SESSION_REFUSED: Record<string, string> = {
  'running': 'work.say.newSession.running',
  'no-words': 'work.say.newSession.noWords',
  'conversation': 'work.say.newSession.conversation',
  'closed': 'work.say.newSession.closed',
  'not-cooling': 'work.say.newSession.notCooling',
};

/** What nothing carries on by itself (D137 §2.2), whose door is *Start a conversation with these words* instead. */
const START_CHAT_INSTEAD = new Set(['conversation', 'closed']);

/**
 * What *Go on in a new session* came to, in the page's own words (MSG1g2): kept, or why not by the route's code, §2.2's
 * nevers said as the box says them; and whether *Start a conversation with these words* is the door instead. A choice the
 * driver could not keep, or a code this page has no sentence for, is the driver's own sentence, passed through whole, since
 * it names what to do.
 */
export function newSessionSaid(
  t: Translate, answer: NewSessionAnswer, { quest }: { quest?: string | null },
): { sentence: string; startChat: boolean } {
  if (answer.sent) return { sentence: t('work.say.newSession.sent'), startChat: false };
  const why = answer.why ?? '';
  const own = NEW_SESSION_REFUSED[why];
  if (own) return { sentence: t(own, { quest: quest ?? '' }), startChat: START_CHAT_INSTEAD.has(why) };
  if (NEVER[why]) return { sentence: neverSentence(t, why, { quest }), startChat: false };
  return { sentence: answer.message, startChat: false };
}

/** The agents a reason may name (D137 §5.1): the adapter a session ran on, the one starts ride now, and its agent's name. */
export type ReasonValues = {
  /** The adapter the session ran on. */
  from?: string | null;
  /** The adapter this machine's starts ride now. */
  to?: string | null;
  /** The adapter that cannot continue a conversation. */
  adapter?: string | null;
  /** What a person calls the agent the session ran on. */
  agent?: string | null;
};

/**
 * Each reason's key, and the values its sentence needs (D137 §5.1): a code the driver writes on its note (`ContinueWhy`).
 * `locales/note.test.ts` holds it to the reasons the driver's `NoteCodes.Continue` declares, key for key (LANG1b).
 */
export const REASONS: Readonly<Record<string, { key: string; needs?: readonly (keyof ReasonValues)[] }>> = {
  'account': { key: 'work.say.why.account' },
  'adapter': { key: 'work.say.why.adapter', needs: ['from', 'to'] },
  'unkept': { key: 'work.say.why.unkept' },
  'tree': { key: 'work.say.why.tree' },
  'unable': { key: 'work.say.why.unable', needs: ['adapter'] },
  'offered': { key: 'work.say.why.offered' },
  'gone': { key: 'work.say.why.gone' },
  'refused': { key: 'work.say.why.refused' },
  'elsewhere': { key: 'work.say.why.elsewhere', needs: ['agent'] },
  'ended': { key: 'work.say.why.ended' },
  'teammate': { key: 'work.say.why.teammate' },
  'intake': { key: 'work.say.why.intake' },
  'stood-down': { key: 'work.say.why.stoodDown' },
};

/**
 * Why the person's words did not go on in their session, in the page's own words (D137 §5.1): a reason is chrome, so the
 * page says it from the code, never from the note's English. Null for a code it does not know, or one whose sentence
 * names an agent the page was not told, and then the driver's own line stands (platform language §4).
 */
export function reasonOf(t: Translate, code: string, values: ReasonValues): string | null {
  const reason = REASONS[code];
  if (!reason) return null;
  if (reason.needs?.some((name) => !values[name])) return null;
  return t(reason.key, values);
}

/**
 * The most characters the box sends at once to a driven session on the native door (D137 §2.4). A resumed native run takes
 * the person's words as one argument, and Windows caps a whole command line at 32,767 characters, which the words share
 * with the run's flags, its conversation id and what it is handed again; this leaves those the rest. Until MSG1i hands the
 * words on stdin, the box refuses longer ones before sending rather than have the spawn fail.
 */
export const NATIVE_WORDS_LIMIT = 24_000;

/** Whether these words are longer than the session's door takes at once: a driven session's on the native door alone. */
export function tooLong(text: string, { door, kind }: { door: 'acp' | 'pipe' | null; kind?: Session['kind'] }): boolean {
  return door === 'pipe' && kind !== 'chat' && text.length > NATIVE_WORDS_LIMIT;
}
