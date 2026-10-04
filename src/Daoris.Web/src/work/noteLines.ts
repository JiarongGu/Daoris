import type { NotePart } from '../api';
import { list, moment } from '../format';
import { reasonOf } from './say';

// A session's note as the page words it (LANG1b, D142; the language design §2, §5, §6): each coded part from its code and
// values in the reader's language, each words part as written, and what the page cannot word as its record keeps it,
// marked. Pure, so every part a writer could send is an argument; `Note.tsx` draws what this answers.

type Translate = (key: string, options?: Record<string, unknown>) => string;

/** A family of reasons a `why` names: MSG1f's (`work.say.why.*`, by `reasonOf`) or a cool-off's (`harness.cooling.why.*`). */
type Why = 'continue' | 'cooling';

/** A code the page words: the values its sentence says, its key where it is not `note.<code>`, and the reasons a `why` names. */
export type NoteCode = { values: readonly string[]; key?: string; why?: Why };

/**
 * Every code a writer declares, as the page words it: the page's half of the twin (D142 point 4; the language design §5;
 * `.claude/knowledge/twins.md`). `locales/note.test.ts` parses the driver's `NoteCodes` and the service's `LedgerNoteCodes`
 * and holds this map to them code for code, with the same values, key and family, so a code a writer adds fails until it is
 * mapped here and worded in both catalogues.
 */
export const NOTE_CODES: Readonly<Record<string, NoteCode>> = {
  // A session's end.
  'ended.awaits': { values: ['awaits'] },
  'ended.exit': { values: ['exit'] },
  'ended.turn-failed-taken': { values: [] },
  'ended.turn-failed-open': { values: [] },
  'ended.parked-asked': { values: [] },
  'ended.parked-transcript': { values: [] },
  'ended.parked-short': { values: [] },
  'ended.answered-unfinished': { values: ['awaits', 'exit'] },
  'ended.carried-unfinished': { values: ['exit'] },
  'ended.done': { values: [] },
  'ended.declined': { values: [] },
  'ended.stood-down': { values: [] },
  'ended.taken-exit': { values: ['exit'] },
  'ended.untouched': { values: [] },
  'ended.untouched-exit': { values: ['exit'] },
  'ended.went-on': { values: [] },
  'ended.went-on-exit': { values: ['exit'] },
  'ended.timeout': { values: ['minutes'] },
  'ended.stopped': { values: [] },
  'ended.lost-claim': { values: [] },
  'ended.driver-closed': { values: [] },
  // An account's line.
  'account.cooling': { values: ['until', 'why'], why: 'cooling' },
  'account.refused': { values: ['owner'] },
  'account.refused-own': { values: ['owner'] },
  'account.signed-out': { values: ['owner'] },
  'account.signed-out-own': { values: ['owner'] },
  // Why the account a resume asked for could not carry the words (MSG1g), after a line whose reason is `account`.
  'account.resume-signed-out': { values: [] },
  'account.resume-refused': { values: [] },
  'account.resume-gone': { values: [] },
  'account.resume-outside': { values: [] },
  'account.resume-kept': { values: [] },
  'account.resume-new-session': { values: [] },
  // While it starts and works.
  'started.tree': { values: ['branch', 'basedOn'] },
  'started.tree-unrecorded': { values: [] },
  'started.resumes-answered': { values: ['quest', 'answered'] },
  'started.in-asking-tree': { values: ['session'] },
  'started.words-open': { values: ['quest', 'session'] },
  'started.words-taken': { values: ['quest', 'session'] },
  'started.answer': { values: ['quest', 'session'] },
  'started.released': { values: ['quest', 'session'] },
  'started.cut-off': { values: ['quest', 'session'] },
  'started.other-account': { values: [] },
  'started.same-tree': { values: [] },
  'started.fell-back': { values: ['why'], why: 'continue' },
  'working.resumes-answer': { values: [] },
  'working.goes-on': { values: [] },
  // Where the person's words went.
  'went.cannot': { values: ['why'], key: 'work.say.cannot', why: 'continue' },
  'went.new-session': { values: ['why'], why: 'continue' },
  'went.carried-on': { values: ['why'], why: 'continue' },
  // An intake.
  'intake.ask-deleted': { values: [] },
  'intake.published': { values: ['quests', 'ask'] },
  'intake.ask-closed': { values: ['ask'] },
  'intake.ask-answered': { values: ['ask', 'quests'] },
  'intake.turn-failed': { values: ['ask'] },
  'intake.asks': { values: ['ask'] },
  'intake.exit': { values: ['ask', 'exit'] },
  'intake.person-answered': { values: ['ask', 'quests'] },
  'intake.person-closed': { values: ['ask'] },
  'intake.person-deleted': { values: ['ask'] },
  // A stop.
  'stopped.paused-ask': { values: ['ask'] },
  'stopped.paused-quest': { values: ['quest'] },
  'stopped.abandoned-ask': { values: ['ask'] },
  'stopped.abandoned-quest': { values: ['quest'] },
  'stopped.checkpoint-finished': { values: [] },
  'stopped.checkpoint-stopped': { values: [] },
  'stopped.checkpoint-moved': { values: [] },
  'stopped.orphan': { values: [] },
  'chat.closed': { values: [] },
  'chat.ended-by-person': { values: [] },
  'chat.ended': { values: [] },
  'chat.cancelled': { values: [] },
  'chat.not-kept': { values: [] },
  // A landing at the quest's done, under a rule that accepts automatically (LAND2b).
  'landing.accepted': { values: ['branch'] },
  'landing.plugin-unready': { values: ['branch', 'plugin'] },
  'landing.plugin-failed': { values: ['branch', 'plugin'] },
  'landing.nothing': { values: [] },
  'landing.held': { values: [] },
  'landing.uncommitted': { values: ['paths'] },
  'landing.exists': { values: ['branch'] },
  'landing.refused': { values: [] },
  'landing.no-tree': { values: [] },
  'landing.not-done': { values: [] },
  // The service's own lines.
  'ledger.answered': { values: [] },
  'ledger.parked': { values: [] },
  'ledger.went-on': { values: ['at'] },
};

/**
 * How each value a code carries is said (the language design §3–§4): an id as the record writes it, a list of quest ids
 * joined the reader's way, a moment in the reader's language and zone, a number in its figures, a fact as it is, and a
 * reason by its family. `locales/note.test.ts` holds that every value a writer declares has a way here.
 */
export const NOTE_VALUES: Readonly<Record<string, 'id' | 'ids' | 'moment' | 'number' | 'text' | 'why'>> = {
  awaits: 'id', answered: 'id', ask: 'id', quest: 'id', session: 'id',
  quests: 'ids',
  until: 'moment', at: 'moment',
  exit: 'number', minutes: 'number', paths: 'number',
  owner: 'text', branch: 'text', basedOn: 'text', plugin: 'text',
  why: 'why',
};

/** A cool-off's four reasons (TOOL4d), each its `harness.cooling.why.*` entry. */
const COOLING = new Set(['stated', 'assumed', 'default', 'notBelieved']);

/**
 * One line of a note as the page shows it:
 * - `said`: Daoris's line, worded by the page in the reader's language;
 * - `recorded`: the record's own English, which the page could not word (a code it does not know, a value its sentence
 *   needs that is absent, a record from before parts), shown as kept and marked *shown as recorded*;
 * - `words`: someone's words, shown as written and never marked: the agent's, the person's, a program's.
 */
export type NoteLine =
  | { kind: 'said'; text: string }
  | { kind: 'recorded'; text: string }
  | { kind: 'words'; text: string; by: string };

/** A record's note: its English, and its parts where its writer wrote them. */
export type NoteRecord = { note?: string | null; parts?: readonly NotePart[] | null };

const text = (value: unknown): string | null =>
  typeof value === 'string' && value.length > 0 ? value : typeof value === 'number' && Number.isFinite(value) ? String(value) : null;

/** A value as its sentence says it, or null where it is absent or not the shape its kind is. */
function said(t: Translate, name: string, values: Record<string, unknown>, code: NoteCode, language: string): string | null {
  const value = values[name];
  switch (NOTE_VALUES[name] ?? 'text') {
    case 'ids': {
      if (!Array.isArray(value) || value.length === 0) return null;
      const ids = value.map(text);
      return ids.every((id): id is string => id !== null) ? list(ids.map((id) => `#${id}`), language) : null;
    }
    case 'moment': {
      const at = typeof value === 'string' ? Date.parse(value) : Number.NaN;
      return Number.isNaN(at) ? null : moment(value as string, language);
    }
    case 'number':
      return typeof value === 'number' && Number.isFinite(value) ? String(value)
        : typeof value === 'string' && /^-?\d+$/.test(value) ? value : null;
    case 'why': {
      const why = text(value);
      if (!why) return null;
      if (code.why === 'cooling') return COOLING.has(why) ? t(`harness.cooling.why.${why}`) : null;
      return reasonOf(t, why, {
        from: text(values.from), to: text(values.to), adapter: text(values.adapter), agent: text(values.agent),
      });
    }
    default:
      return text(value);
  }
}

/** A coded part in the reader's language, or null where the page cannot word it (§5). */
function worded(t: Translate, part: { code: string; values?: Record<string, unknown> | null }, language: string): string | null {
  const code = NOTE_CODES[part.code];
  if (!code) return null;
  const values = part.values ?? {};
  const options: Record<string, string> = {};
  for (const name of code.values) {
    const value = said(t, name, values, code, language);
    if (value === null) return null;
    options[name] = value;
  }
  return t(code.key ?? `note.${part.code}`, options);
}

/**
 * A note's lines as the page shows them (D142 points 1, 4, 5): each part in its writer's order, or, for a record with no
 * parts, its note as kept. Nothing is read out of the English: a record from before is shown, not re-read (§6).
 *
 * @param language The reader's language, which a list and a moment are written in.
 */
export function noteLines(t: Translate, { note, parts }: NoteRecord, language: string): NoteLine[] {
  if (!parts || parts.length === 0) return note ? [{ kind: 'recorded', text: note }] : [];
  const lines: NoteLine[] = [];
  for (const part of parts) {
    if (typeof part.code === 'string') {
      const line = worded(t, part, language);
      if (line !== null) lines.push({ kind: 'said', text: line });
      else if (part.text) lines.push({ kind: 'recorded', text: part.text });
    } else if (typeof part.words === 'string' && part.words.length > 0) {
      // A note from before parts, carried whole, is Daoris's English as an older driver wrote it: shown as a record from
      // before is, marked, since the page could not word it either.
      lines.push(part.by === 'before' || !part.by ? { kind: 'recorded', text: part.words } : { kind: 'words', text: part.words, by: part.by });
    }
  }
  return lines;
}

/** Two lines joined the catalogue's way: English puts a space after a full stop and Chinese does not (platform language §4). */
export function joinLines(t: Translate, lines: readonly string[]): string {
  return lines.reduce((first, second) => (first ? t('work.note.join', { first, second }) : second), '');
}

/**
 * A note's lines as the blocks `Note` draws (the language design §9): Daoris's lines running together as one paragraph,
 * joined the reader's way, until someone's words or a recorded line sets itself apart beneath them.
 */
export function noteBlocks(t: Translate, lines: readonly NoteLine[]): NoteLine[] {
  const blocks: NoteLine[] = [];
  for (const line of lines) {
    const last = blocks.at(-1);
    if (line.kind === 'said' && last?.kind === 'said') last.text = joinLines(t, [last.text, line.text]);
    else blocks.push({ ...line });
  }
  return blocks;
}

/** A note as one run of words, for a row too narrow for its blocks: every line, joined the reader's way. */
export function noteText(t: Translate, record: NoteRecord, language: string): string {
  return joinLines(t, noteLines(t, record, language).map((line) => line.text));
}

/** Whether a record has a note to show at all. */
export function hasNote({ note, parts }: NoteRecord): boolean {
  return Boolean(note) || (parts?.length ?? 0) > 0;
}

/** An ideograph, a kana or a full-width form, which sets about twice as wide as a letter of the same size. */
const WIDE = /[⺀-鿿가-힯豈-﫿︰-﹏＀-｠￠-￦]/u;

/**
 * How many lines texts take at about `measure` letters a line (UX6b, design §2.5): each of their own lines one at least,
 * and one more for each measure it runs past, an ideograph counted as two letters. An estimate, since the page lays
 * nothing out before it draws: near enough to fold a verify report in the timeline and leave a sentence whole.
 */
export function linesTaken(texts: readonly string[], measure: number): number {
  let lines = 0;
  for (const text of texts) {
    for (const line of text.split('\n')) {
      let width = 0;
      for (const glyph of line) width += WIDE.test(glyph) ? 2 : 1;
      lines += Math.max(1, Math.ceil(width / measure));
    }
  }
  return lines;
}
