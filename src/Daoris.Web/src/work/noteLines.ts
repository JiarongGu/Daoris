import type { NotePart } from '../data/api';
import { figure, list, moment } from '../locales/format';
import { windowName } from '../settings/accounts/accounts';
import type { AccountNames } from './accountNames';
import { NOTE_CODES, type NoteCode } from './notes/noteCodes';
import { reasonOf } from './say';

// A session's note as the page words it: each coded part from its code and
// values in the reader's language, each words part as written, and what the page cannot word as its record keeps it,
// marked. Pure, so every part a writer could send is an argument; `Note.tsx` draws what this answers.

type Translate = (key: string, options?: Record<string, unknown>) => string;

export { NOTE_CODES } from './notes/noteCodes';
export type { NoteCode } from './notes/noteCodes';

/**
 * Whose words a resumed conversation goes on with (the driver's `NoteCodes.Resumed`), each its entry; none carries
 * a value of its own. `locales/note.test.ts` holds this map to the driver's declaration key for key.
 */
export const RESUMED_REASONS: Readonly<Record<string, { key: string; values: readonly string[] }>> = {
  answer: { key: 'note.resumed.why.answer', values: [] },
  words: { key: 'note.resumed.why.words', values: [] },
  findings: { key: 'note.resumed.why.findings', values: [] },
  'answer-and-findings': { key: 'note.resumed.why.answer-and-findings', values: [] },
  'words-and-findings': { key: 'note.resumed.why.words-and-findings', values: [] },
  helper: { key: 'note.resumed.why.helper', values: [] },
  'words-and-helper': { key: 'note.resumed.why.words-and-helper', values: [] },
  // Daoris's own words alone: a reopen told, a step's go-on.
  daoris: { key: 'note.resumed.why.daoris', values: [] },
  // The supervisor's answer, on the owner's behalf, with none of the owner's words.
  supervisor: { key: 'note.resumed.why.supervisor', values: [] },
};

/**
 * Why the person's words never reached the session (the driver's `NoteCodes.Unreached`), each its entry; none
 * carries a value of its own. `locales/note.test.ts` holds this map to the driver's declaration key for key.
 */
export const UNREACHED_REASONS: Readonly<Record<string, { key: string; values: readonly string[] }>> = {
  'ended-first': { key: 'note.words.why.ended-first', values: [] },
  unkept: { key: 'note.words.why.unkept', values: [] },
  // The record did not name them marked attempted, so they are not written.
  unmarked: { key: 'note.words.why.unmarked', values: [] },
};

/**
 * Why a conversation went to another agent (the driver's `NoteCodes.Handed`), each its entry; none carries
 * a value of its own. `locales/note.test.ts` holds this map to the driver's declaration key for key.
 */
export const HANDED_REASONS: Readonly<Record<string, { key: string; values: readonly string[] }>> = {
  press: { key: 'note.handed.why.press', values: [] },
  near: { key: 'note.handed.why.near', values: [] },
  limit: { key: 'note.handed.why.limit', values: [] },
  agent: { key: 'note.handed.why.agent', values: [] },
  // Daoris moved it by the usage curve.
  curve: { key: 'note.handed.why.curve', values: [] },
  // Every other continuation names its link: its conversation lost, a carry-on, a driven record's fallback.
  resume: { key: 'note.handed.why.resume', values: [] },
  'carry-on': { key: 'note.handed.why.carry-on', values: [] },
  retry: { key: 'note.handed.why.retry', values: [] },
  // The replacement of a record its agent refused at the open, before any prompt.
  'not-offered': { key: 'note.handed.why.not-offered', values: [] },
  // A role's policy moved the line to another choice.
  transient: { key: 'note.handed.why.transient', values: [] },
  failed: { key: 'note.handed.why.failed', values: [] },
  quiet: { key: 'note.handed.why.quiet', values: [] },
  budget: { key: 'note.handed.why.budget', values: [] },
};

/**
 * Why the files stage kept, left, placed or stored a dependency as it did (the driver's `NoteCodes.Deps`), each its entry with the
 * values it says. `locales/note.test.ts` holds this map to the driver's declaration key for key.
 */
export const DEPS_REASONS: Readonly<Record<string, { key: string; values: readonly string[] }>> = {
  changed: { key: 'note.deps.why.changed', values: [] },
  companion: { key: 'note.deps.why.companion', values: ['file'] },
  unplaced: { key: 'note.deps.why.unplaced', values: [] },
  'key-moved': { key: 'note.deps.why.key-moved', values: [] },
  'before-own': { key: 'note.deps.why.before-own', values: ['before'] },
  'input-changed': { key: 'note.deps.why.input-changed', values: [] },
  gone: { key: 'note.deps.why.gone', values: [] },
  grouped: { key: 'note.deps.why.grouped', values: ['with'] },
  denied: { key: 'note.deps.why.denied', values: ['install'] },
  held: { key: 'note.deps.why.held', values: ['install'] },
  failed: { key: 'note.deps.why.failed', values: ['install', 'tail'] },
  tracked: { key: 'note.deps.why.tracked', values: ['file'] },
  bound: { key: 'note.deps.why.bound', values: [] },
  'names-tree': { key: 'note.deps.why.names-tree', values: [] },
  secret: { key: 'note.deps.why.secret', values: [] },
  beside: { key: 'note.deps.why.beside', values: ['file'] },
  'link-out': { key: 'note.deps.why.link-out', values: ['link'] },
  'held-ignored': { key: 'note.deps.why.held-ignored', values: ['file'] },
  group: { key: 'note.deps.why.group', values: ['with'] },
  unsealed: { key: 'note.deps.why.unsealed', values: [] },
  'share-pattern': { key: 'note.deps.why.share-pattern', values: [] },
  'share-absent': { key: 'note.deps.why.share-absent', values: [] },
  'share-unignored': { key: 'note.deps.why.share-unignored', values: [] },
  'share-repository': { key: 'note.deps.why.share-repository', values: [] },
  'share-place-link': { key: 'note.deps.why.share-place-link', values: [] },
  'share-standing': { key: 'note.deps.why.share-standing', values: [] },
  'share-file-link': { key: 'note.deps.why.share-file-link', values: [] },
  'share-failed': { key: 'note.deps.why.share-failed', values: ['tail'] },
};

/**
 * Why a piece is refused, held or uncertain (the driver's `NoteCodes.Partner`), each its entry; none carries a value of
 * its own. `locales/note.test.ts` holds this map to the driver's declaration key for key.
 */
export const PARTNER_REASONS: Readonly<Record<string, { key: string; values: readonly string[] }>> = {
  'asker-unfit': { key: 'note.partner.why.asker-unfit', values: [] },
  'asker-ended': { key: 'note.partner.why.asker-ended', values: [] },
  'asker-gone': { key: 'note.partner.why.asker-gone', values: [] },
  blank: { key: 'note.partner.why.blank', values: [] },
  'path-outside': { key: 'note.partner.why.path-outside', values: [] },
  'no-commit': { key: 'note.partner.why.no-commit', values: [] },
  'base-missing': { key: 'note.partner.why.base-missing', values: [] },
  'too-many': { key: 'note.partner.why.too-many', values: [] },
  'not-listed': { key: 'note.partner.why.not-listed', values: [] },
  'no-connector': { key: 'note.partner.why.no-connector', values: [] },
  'not-measured': { key: 'note.partner.why.not-measured', values: [] },
  'copy-failed': { key: 'note.partner.why.copy-failed', values: [] },
  'no-job': { key: 'note.partner.why.no-job', values: [] },
  'state-unreadable': { key: 'note.partner.why.state-unreadable', values: [] },
  'job-unknown': { key: 'note.partner.why.job-unknown', values: [] },
  'ref-moved': { key: 'note.partner.why.ref-moved', values: [] },
  'import-refused': { key: 'note.partner.why.import-refused', values: [] },
  'no-work': { key: 'note.partner.why.no-work', values: [] },
  'hand-cut': { key: 'note.partner.why.hand-cut', values: [] },
};

/**
 * Why no helper answered a question (the driver's `NoteCodes.Helper`), each its entry; none carries a value of its
 * own. `locales/note.test.ts` holds this map to the driver's declaration key for key.
 */
export const HELPER_REASONS: Readonly<Record<string, { key: string; values: readonly string[] }>> = {
  off: { key: 'note.helper.why.off', values: [] },
  'asker-gone': { key: 'note.helper.why.asker-gone', values: [] },
  'asker-unfit': { key: 'note.helper.why.asker-unfit', values: [] },
  'too-many': { key: 'note.helper.why.too-many', values: [] },
  unreadable: { key: 'note.helper.why.unreadable', values: [] },
  'not-started': { key: 'note.helper.why.not-started', values: [] },
  'no-answer': { key: 'note.helper.why.no-answer', values: [] },
  conversation: { key: 'note.helper.why.conversation', values: [] },
  'cannot-take': { key: 'note.helper.why.cannot-take', values: [] },
  // The person's no to an agent's ask.
  declined: { key: 'note.helper.why.declined', values: [] },
  // The asker's work in progress that could not be captured whole for its helper.
  'capture-merge': { key: 'note.helper.why.capture-merge', values: [] },
  'capture-link': { key: 'note.helper.why.capture-link', values: [] },
  'capture-submodule': { key: 'note.helper.why.capture-submodule', values: [] },
  'capture-unreadable': { key: 'note.helper.why.capture-unreadable', values: [] },
  'capture-bound': { key: 'note.helper.why.capture-bound', values: [] },
  'capture-moved': { key: 'note.helper.why.capture-moved', values: [] },
  // A hand whose outcome a restart lost, never made again by itself.
  'hand-uncertain': { key: 'note.helper.why.hand-uncertain', values: [] },
  // A limit cut it off and the session no longer waits on it (its line is `helper.limited`, which says when).
  limited: { key: 'note.helper.why.limited', values: [] },
  // Under the default, only the agents Daoris set up and signed in on this machine are asked.
  'not-set-up': { key: 'note.helper.why.not-set-up', values: [] },
  'none-set-up': { key: 'note.helper.why.none-set-up', values: [] },
  // Its provider kept failing through its tries or its minutes.
  transient: { key: 'note.helper.why.transient', values: [] },
};

/**
 * Why an agent's ask to hand its session over came to nothing (the driver's `NoteCodes.HandOver`), each its entry;
 * none carries a value of its own. `locales/note.test.ts` holds this map to the driver's declaration key for key.
 */
export const HAND_OVER_ASK_REASONS: Readonly<Record<string, { key: string; values: readonly string[] }>> = {
  off: { key: 'note.handover.why.off', values: [] },
  declined: { key: 'note.handover.why.declined', values: [] },
  'just-handed': { key: 'note.handover.why.just-handed', values: [] },
  'no-agent': { key: 'note.handover.why.no-agent', values: [] },
  'asker-gone': { key: 'note.handover.why.asker-gone', values: [] },
  'asker-unfit': { key: 'note.handover.why.asker-unfit', values: [] },
  cannot: { key: 'note.handover.why.cannot', values: [] },
  'quest-closed': { key: 'note.handover.why.quest-closed', values: [] },
  'quest-elsewhere': { key: 'note.handover.why.quest-elsewhere', values: [] },
  'quest-reopened': { key: 'note.handover.why.quest-reopened', values: [] },
  'not-last-run': { key: 'note.handover.why.not-last-run', values: [] },
  'unknown-agent': { key: 'note.handover.why.unknown-agent', values: [] },
  'same-agent': { key: 'note.handover.why.same-agent', values: [] },
  'no-door': { key: 'note.handover.why.no-door', values: [] },
  running: { key: 'note.handover.why.running', values: [] },
  'tree-gone': { key: 'note.handover.why.tree-gone', values: [] },
  waiting: { key: 'note.handover.why.waiting', values: [] },
};

/**
 * The states background work is known to end in (the driver's `BackgroundState`), each its entry. Any other state
 * is shown as the harness sent it. `locales/note.test.ts` holds this map to the driver's declaration key for key.
 */
export const BACKGROUND_STATES: Readonly<Record<string, string>> = {
  completed: 'note.background.state.completed',
  failed: 'note.background.state.failed',
  stopped: 'note.background.state.stopped',
  ended: 'note.background.state.ended',
};

/**
 * The steps that chose a conversation's account (the driver's `NoteCodes.Opening`), each its
 * entry and its own values, which ride beside the line's `why`. `locales/note.test.ts` holds this map to the driver's
 * declaration key for key and value for value.
 */
export const OPENING_REASONS: Readonly<Record<string, { key: string; values: readonly string[] }>> = {
  cooling: { key: 'note.opening.why.cooling', values: ['agent', 'over', 'until', 'cooling'] },
  refused: { key: 'note.opening.why.refused', values: ['agent', 'over'] },
  'signed-out': { key: 'note.opening.why.signed-out', values: ['agent', 'over'] },
  locked: { key: 'note.opening.why.locked', values: ['agent', 'over'] },
  'kept-self': { key: 'note.opening.why.kept-self', values: [] },
  'kept-last': { key: 'note.opening.why.kept-last', values: ['over'] },
  kept: { key: 'note.opening.why.kept', values: ['over'] },
  'near-reached': { key: 'note.opening.why.near-reached', values: ['over', 'window'] },
  // Only in a line written before: the pace warning is not near.
  'near-word': { key: 'note.opening.why.near-word', values: ['over', 'window'] },
  'near-credits': { key: 'note.opening.why.near-credits', values: ['over'] },
  // Only in a line written before: near is the curve, and both near are said by the other's reason.
  'near-used': { key: 'note.opening.why.near-used', values: ['over', 'used', 'window', 'near'] },
  'over-curve': { key: 'note.opening.why.over-curve', values: ['over', 'window', 'used', 'curve'] },
  near: { key: 'note.opening.why.near', values: ['over'] },
  room: { key: 'note.opening.why.room', values: ['over', 'used', 'against'] },
  fewest: { key: 'note.opening.why.fewest', values: [] },
  // Only in a line written before: step 4 weighs what would lapse, said by `lapses`.
  lapsing: { key: 'note.opening.why.lapsing', values: ['at'] },
  lapses: { key: 'note.opening.why.lapses', values: ['window', 'unused', 'at', 'seen'] },
  'most-room': { key: 'note.opening.why.most-room', values: ['room', 'window'] },
  // The week's pace, written under `switch: off` only.
  behind: { key: 'note.opening.why.behind', values: ['used', 'gone'] },
  ahead: { key: 'note.opening.why.ahead', values: ['over', 'used', 'gone'] },
  'not-started': { key: 'note.opening.why.not-started', values: [] },
  'least-recent': { key: 'note.opening.why.least-recent', values: [] },
  // Only in a line written before: every start reads the one set, whose first is `first-order`; `default-here` and
  // `first-here` are said without the machine's list's name.
  default: { key: 'note.opening.why.default', values: ['workspace'] },
  'default-here': { key: 'note.opening.why.default-here', values: [] },
  first: { key: 'note.opening.why.first', values: ['workspace'] },
  'first-here': { key: 'note.opening.why.first-here', values: [] },
  'first-order': { key: 'note.opening.why.first-order', values: [] },
};

/**
 * How each value a code carries is said: an id as the record writes it, a list of quest ids
 * joined the reader's way, a moment in the reader's language and zone, a number in its figures, a fact as it is, a reason
 * by its family, and a limit's window as the agents' screens word it (`harness.window.*`), one this build does not
 * know said as named. A conversation's first line adds a count grouped the reader's way, a whole percent, and a
 * cool-off's reason as a value of its own. `locales/note.test.ts` holds that every value a writer declares has a way here.
 */
export const NOTE_VALUES: Readonly<
  Record<
    string,
    | 'id' | 'ids' | 'names' | 'moment' | 'number' | 'count' | 'percent' | 'text' | 'why' | 'cooling' | 'window' | 'account' | 'accounts'
    | 'bgstate' | 'setAt' | 'accessAt' | 'actor' | 'authors' | 'routing'
  >
> = {
  // The routing standing a chat is told, each of the judge's four worded by `work.note.routing.*`.
  standing: 'routing',
  // Who a word is by, each a code worded by `work.note.author.*` and joined the reader's way.
  authors: 'authors',
  // A commit rule's mode, its own word set as code by the sentence, and where it is set.
  mode: 'text', source: 'setAt',
  // The level whose access setup landed work by its rule.
  access: 'accessAt',
  awaits: 'id', answered: 'id', ask: 'id', quest: 'id', session: 'id', opinion: 'id',
  // The operation a combined Accept brought a member in by, by its id.
  operation: 'id',
  quests: 'ids',
  servers: 'names', works: 'names',
  option: 'text', chosen: 'text', work: 'text', state: 'bgstate', repository: 'text',
  // The call a quiet session's agent is inside, its title as the record names it.
  call: 'text',
  // Why a role's policy chose the choice that ran, the judge's sentence as written.
  said: 'text',
  // A chat run's workflow, by its id, and why it cannot be bound or land, in the driver's words.
  workflow: 'text', problem: 'text',
  // The limit an attempt just after its reset met again, in the agent's own words.
  hit: 'text', reason: 'text',
  // The build an update installed when its close cut a run, as `version` is said.
  build: 'text',
  until: 'moment', at: 'moment', seen: 'moment',
  exit: 'number', minutes: 'number', paths: 'number', turn: 'number',
  // How long Ask Daoris's start waited for the machine's reading, in whole seconds.
  seconds: 'number',
  // An account's room under its usage curve, in whole percent points.
  room: 'number',
  tokens: 'count', count: 'count',
  // A curve's value now is a whole percent, as a window's use is.
  used: 'percent', near: 'percent', gone: 'percent', against: 'percent', curve: 'percent',
  // The share of a week unused at its reset, as a window's use is.
  unused: 'percent',
  // A window's use projected to now, as a window's use is.
  projected: 'percent',
  now: 'text', then: 'text',
  // The working role a design- or exact-weighted quest started on, said as its name.
  role: 'text',
  // An account id is worded by the name the person gave it where the page knows it; `owner` is the agent.
  account: 'account', over: 'account', first: 'account',
  // A list of account ids, each named so and set as code; `logins` are the sign-in commands, each set as code.
  accounts: 'accounts', logins: 'names',
  profile: 'text', version: 'text', door: 'text',
  // A review set-up's environment and repository, its commit's short id, and whose findings a second opinion's answers are.
  // `to` and `from` are a code's accounts where it says so (`NoteCode.accounts`: a move by the curve).
  named: 'text', to: 'text', from: 'text', commit: 'text', who: 'text',
  owner: 'text', branch: 'text', basedOn: 'text', plugin: 'text', agent: 'text', workspace: 'text',
  // The model an actor's choice named, in the tool's own word, the actor by its name, and which of its choices ran.
  model: 'text', actor: 'actor', n: 'number', entry: 'text', passed: 'text',
  // A supervisor rule's act, the count it reads by name, its bound, its place and its act as the rule says them.
  counted: 'text', bound: 'number', place: 'text', do: 'text',
  // The dependency folders a tree's open copied from its checkout, each as code, and one it did not copy.
  copied: 'names', modules: 'text',
  // The machine-local files a tree's open copied from its checkout, each as code, and one it did not copy.
  inputs: 'names', input: 'text',
  // The paths a tree's files stage linked to one place every tree uses, each as code.
  shared: 'names',
  bases: 'names', runners: 'names', project: 'text',
  // A rule's setup command that failed, as code.
  command: 'text',
  // A dependency's folder and its install, what the files stage put back, and git's own files an install changed, each as code.
  path: 'text', install: 'text', written: 'names', changed: 'names',
  why: 'why',
  cooling: 'cooling',
  window: 'window',
};

/** A cool-off's four reasons, each its `harness.cooling.why.*` entry. */
const COOLING = new Set(['stated', 'assumed', 'default', 'notBelieved']);

/**
 * Where a rule is set, worded as the handed account words a section's source: the default is none set. A source
 * this page does not know shows the line as recorded.
 */
const SET_AT: Readonly<Record<string, string>> = {
  repository: 'work.handed.source.repository',
  workspace: 'work.handed.source.workspace',
  default: 'work.handed.none.not-set',
};

/**
 * The level that set the access setup a landing is made under, worded whose; one this page does not know shows as
 * recorded. The trace's landing words it by the same map.
 */
export const ACCESS_AT: Readonly<Record<string, string>> = {
  repository: 'settings.access.setupOf.repository',
  workspace: 'settings.access.setupOf.workspace',
  machine: 'settings.access.setupOf.machine',
};

/** The routing standings the driver's judge answers; one this page does not know shows the line as recorded. */
const ROUTING_STANDINGS = new Set(['not-driven', 'no-lanes', 'one-at-a-time', 'open']);

/** The actors' ids, each worded by its `work.role.name.*` entry, as *Details* says it. */
const ACTOR_IDS = new Set(['build', 'design', 'exact', 'intake', 'opinion', 'helper', 'partner', 'brainstorm', 'ask-daoris', 'chat']);

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

/** A record's note: its parts, which are all the page draws. */
export type NoteRecord = { parts?: readonly NotePart[] | null };

const text = (value: unknown): string | null =>
  typeof value === 'string' && value.length > 0 ? value : typeof value === 'number' && Number.isFinite(value) ? String(value) : null;

/** A value as its sentence says it, or null where it is absent or not the shape its kind is. */
/** The authors a note names, in the order the driver writes them; any other is not worded. */
const AUTHORS: readonly string[] = ['supervisor', 'helper', 'opinion', 'daoris'];

function said(
  t: Translate, name: string, values: Record<string, unknown>, code: NoteCode, language: string, names?: AccountNames,
): string | null {
  const value = values[name];
  switch (code.accounts?.includes(name) ? 'account' : NOTE_VALUES[name] ?? 'text') {
    case 'account': {
      const id = text(value);
      return id === null ? null : names?.get(id) ?? id;
    }
    case 'accounts': {
      if (!Array.isArray(value) || value.length === 0) return null;
      const ids = value.map(text);
      return ids.every((id): id is string => id !== null) ? list(ids.map((id) => `\`${names?.get(id) ?? id}\``), language) : null;
    }
    case 'authors': {
      if (!Array.isArray(value) || value.length === 0) return null;
      const authors = value.map(text);
      return authors.every((author): author is string => author !== null && AUTHORS.includes(author))
        ? list(authors.map((author) => t(`work.note.author.${author}`)), language)
        : null;
    }
    case 'ids': {
      if (!Array.isArray(value) || value.length === 0) return null;
      const ids = value.map(text);
      return ids.every((id): id is string => id !== null) ? list(ids.map((id) => `#${id}`), language) : null;
    }
    case 'names': {
      // Plugin-declared server names, each as code, joined the reader's way.
      if (!Array.isArray(value) || value.length === 0) return null;
      const names = value.map(text);
      return names.every((name): name is string => name !== null) ? list(names.map((name) => `\`${name}\``), language) : null;
    }
    case 'moment': {
      const at = typeof value === 'string' ? Date.parse(value) : Number.NaN;
      return Number.isNaN(at) ? null : moment(value as string, language);
    }
    case 'number':
      return typeof value === 'number' && Number.isFinite(value) ? String(value)
        : typeof value === 'string' && /^-?\d+$/.test(value) ? value : null;
    case 'count':
      return typeof value === 'number' && Number.isInteger(value) ? figure(value, language) : null;
    case 'percent':
      return typeof value === 'number' && Number.isInteger(value) ? `${value}%` : null;
    case 'bgstate': {
      const state = text(value);
      return state === null ? null : Object.hasOwn(BACKGROUND_STATES, state) ? t(BACKGROUND_STATES[state]) : state;
    }
    case 'cooling': {
      const why = text(value);
      return why && COOLING.has(why) ? t(`harness.cooling.why.${why}`) : null;
    }
    case 'setAt': {
      const source = text(value);
      return source && Object.hasOwn(SET_AT, source) ? t(SET_AT[source]) : null;
    }
    case 'accessAt': {
      const level = text(value);
      return level && Object.hasOwn(ACCESS_AT, level) ? t(ACCESS_AT[level]) : null;
    }
    case 'routing': {
      const standing = text(value);
      return standing && ROUTING_STANDINGS.has(standing) ? t(`work.note.routing.${standing}`) : null;
    }
    case 'actor': {
      // An actor by its id, said by the name the screen gives it; one this page does not know as written.
      const actor = text(value);
      return actor === null ? null : ACTOR_IDS.has(actor) ? t(`work.role.name.${actor}`) : actor;
    }
    case 'why': {
      const why = text(value);
      if (!why) return null;
      if (code.why === 'cooling') return COOLING.has(why) ? t(`harness.cooling.why.${why}`) : null;
      if (code.why === 'opening') return opening(t, why, values, code, language, names);
      if (code.why === 'resumed') return RESUMED_REASONS[why] ? t(RESUMED_REASONS[why].key) : null;
      if (code.why === 'unreached') return UNREACHED_REASONS[why] ? t(UNREACHED_REASONS[why].key) : null;
      if (code.why === 'handed') return HANDED_REASONS[why] ? t(HANDED_REASONS[why].key) : null;
      if (code.why === 'helper') return HELPER_REASONS[why] ? t(HELPER_REASONS[why].key) : null;
      if (code.why === 'partner') return PARTNER_REASONS[why] ? t(PARTNER_REASONS[why].key) : null;
      if (code.why === 'handover') return HAND_OVER_ASK_REASONS[why] ? t(HAND_OVER_ASK_REASONS[why].key) : null;
      if (code.why === 'deps') {
        const reason = DEPS_REASONS[why];
        return reason ? t(reason.key, Object.fromEntries(reason.values.map((name) => [name, text(values[name]) ?? '']))) : null;
      }
      return reasonOf(t, why, {
        from: text(values.from), to: text(values.to), door: text(values.door), agent: text(values.agent),
      });
    }
    case 'window': {
      const window = typeof value === 'string' && value.length > 0 ? value : null;
      return window === null ? null : windowName(window, t);
    }
    default:
      return text(value);
  }
}

/**
 * The step that chose a conversation's account, with its own values beside the line's, or null where it is a step
 * this page does not know or a value its entry says is absent, so the line shows as recorded.
 */
function opening(
  t: Translate, why: string, values: Record<string, unknown>, code: NoteCode, language: string, names?: AccountNames,
): string | null {
  const reason = OPENING_REASONS[why];
  if (!reason) return null;
  const options: Record<string, string> = {};
  for (const name of reason.values) {
    const value = said(t, name, values, code, language, names);
    if (value === null) return null;
    options[name] = value;
  }
  return t(reason.key, options);
}

/** A coded part in the reader's language, or null where the page cannot word it. */
function worded(
  t: Translate, part: { code: string; values?: Record<string, unknown> | null }, language: string, names?: AccountNames,
): string | null {
  const code = NOTE_CODES[part.code];
  if (!code) return null;
  const values = part.values ?? {};
  const options: Record<string, string> = {};
  for (const name of code.values) {
    const value = said(t, name, values, code, language, names);
    if (value === null) return null;
    options[name] = value;
  }
  return t(code.key ?? `note.${part.code}`, options);
}

/**
 * A note's lines as the page shows them: each part in its writer's order. A record with no parts has
 * no line: nothing is read out of its English.
 *
 * @param language The reader's language, which a list and a moment are written in.
 * @param names What the person calls each account, by id; an id it lacks is said as the record wrote it.
 */
export function noteLines(t: Translate, { parts }: NoteRecord, language: string, names?: AccountNames): NoteLine[] {
  if (!parts || parts.length === 0) return [];
  const lines: NoteLine[] = [];
  for (const part of parts) {
    if (typeof part.code === 'string') {
      const line = worded(t, part, language, names);
      if (line !== null) lines.push({ kind: 'said', text: line });
      else if (part.text) lines.push({ kind: 'recorded', text: part.text });
    } else if (typeof part.words === 'string' && part.words.length > 0) {
      // A sentence the driver wrote with no code, carried whole, is Daoris's English: shown marked, since the page cannot word it.
      lines.push(part.by === 'before' || !part.by ? { kind: 'recorded', text: part.words } : { kind: 'words', text: part.words, by: part.by });
    }
  }
  return lines;
}

/**
 * A working session's note parts as the page reads them: what the session said of an ending it has since gone on
 * from (`ended.*`) and of the words it went on with (`ledger.went-on*`) are history, and read beside its working they say it
 * ended while it works. The ledger keeps them; only this reading leaves them out.
 */
export function liveParts<T extends { code?: string }>(parts: readonly T[] | null | undefined): T[] | null | undefined {
  if (!parts) return parts as null | undefined;
  return parts.filter((part) => !part.code || !(part.code.startsWith('ended.') || part.code.startsWith('ledger.went-on')));
}

/** Two lines joined the catalogue's way: English puts a space after a full stop and Chinese does not. */
export function joinLines(t: Translate, lines: readonly string[]): string {
  return lines.reduce((first, second) => (first ? t('work.note.join', { first, second }) : second), '');
}

/**
 * A note's lines as the blocks `Note` draws: Daoris's lines running together as one paragraph,
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
export function noteText(t: Translate, record: NoteRecord, language: string, names?: AccountNames): string {
  return joinLines(t, noteLines(t, record, language, names).map((line) => line.text));
}

/** Whether a record has a note to show at all. */
export function hasNote({ parts }: NoteRecord): boolean {
  return (parts?.length ?? 0) > 0;
}

/** An ideograph, a kana or a full-width form, which sets about twice as wide as a letter of the same size. */
const WIDE = /[⺀-鿿가-힯豈-﫿︰-﹏＀-｠￠-￦]/u;

/**
 * How many lines texts take at about `measure` letters a line: each of their own lines one at least,
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
