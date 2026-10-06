import i18n from './i18n';
import type { AccountNamer } from './tools';

// Formatting for the management surface. Small on purpose: a helper here is one the views share,
// not a utility belt.

/**
 * The locale every formatter here writes in (UX5 U28): the page's language, with the machine's habits
 * when the machine speaks it. A British machine reading English writes 24/09/2026, which the page's
 * bare `en` would have written 9/24/2026; the same machine reading 中文 writes the date as 中文 does.
 */
function locale(page: string = i18n.language): string {
  const machine = typeof navigator === 'undefined' ? undefined : navigator.language;
  return machine && machine.split('-')[0] === page.split('-')[0] ? machine : page;
}

/** Compact counts for stat tiles: 854 · 12.9K · 4.2M. Proportional figures — never tabular-nums. */
export function compact(value: number): string {
  if (value >= 1_000_000) return `${(value / 1_000_000).toFixed(1).replace(/\.0$/, '')}M`;
  if (value >= 10_000) return `${(value / 1_000).toFixed(1).replace(/\.0$/, '')}K`;
  return value.toLocaleString(locale());
}

/**
 * A count, grouped as the reader's language groups it. Never `toLocaleString()` with no locale,
 * which takes the machine's rather than the page's (UX5 U28; `tokens.test.ts` holds it).
 */
export function figure(value: number): string {
  return value.toLocaleString(locale());
}

/**
 * A moment as a date and a clock, written the way the reader's language writes them: `2026/9/23
 * 13:58:49` in 中文. The drawer read `23/09/2026, 1:58:49 pm` there, the machine's locale (UX5 U28).
 */
export function stamp(iso: string): string {
  return new Date(iso).toLocaleString(locale());
}

/**
 * A moment as a person reads a reset (TOOL4g, D125 §2.4): its month and day, its clock, and the zone named, in this
 * machine's zone and written the way the reader's language writes them — `Oct 3, 16:02 GMT+5:45`, `10月3日 16:02 GMT+5:45`.
 * `language` is the page's unless a caller words in another, as {@link list}'s is.
 */
export function moment(iso: string, language?: string): string {
  return new Date(iso).toLocaleString(locale(language), {
    month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit', hourCycle: 'h23', timeZoneName: 'short',
  });
}

/**
 * When something was read, as a person scans a list of them (UX6e, D150 §5.3): its clock today, its day and clock before,
 * in this machine's zone and the reader's language: `10:42`, `Oct 3, 10:42`, `10月3日 10:42`.
 */
export function clockOf(iso: string, now: Date = new Date(), language?: string): string {
  const at = new Date(iso);
  const today = at.toDateString() === now.toDateString();
  return at.toLocaleString(locale(language), today
    ? { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }
    : { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' });
}

/**
 * Items joined the way the reader's language joins a list (LANG1b, the language design §4): `#a1, #b2, and #c3`,
 * `#a1、#b2和#c3`. `language` is the page's unless a caller words in another (a story's or a test's fixed language).
 */
export function list(items: readonly string[], language?: string): string {
  return new Intl.ListFormat(locale(language), { type: 'conjunction' }).format(items);
}

/** A file's size, in the unit a person reads it in: 812 B · 2.4 KB · 3.1 MB. */
export function size(bytes: number): string {
  if (bytes < 1024) return `${bytes.toLocaleString(locale())} B`;
  const kb = bytes / 1024;
  if (kb < 1024) return `${kb.toLocaleString(locale(), { maximumFractionDigits: 1 })} KB`;
  return `${(kb / 1024).toLocaleString(locale(), { maximumFractionDigits: 1 })} MB`;
}

/**
 * Relative time, because on a management surface "when" matters as "how long has this sat".
 * Reads the active catalog; callers re-render on language change through their own useTranslation.
 */
export function ago(iso: string): string {
  const minutes = Math.floor((Date.now() - new Date(iso).getTime()) / 60_000);
  if (minutes < 1) return i18n.t('time.justNow');
  if (minutes < 60) return i18n.t('time.minutes', { count: minutes });
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return i18n.t('time.hours', { count: hours });
  return i18n.t('time.days', { count: Math.floor(hours / 24) });
}

/** Whole days something has been sitting — the number the Overview leads with. */
export function sittingDays(iso: string): number {
  return Math.floor((Date.now() - new Date(iso).getTime()) / 86_400_000);
}

/**
 * How long something has been going — a SPAN, where `ago` is a point.
 *
 * @remarks
 * The fact the rail could not carry before (D55): "moved 4m ago" reads identically for a session
 * three minutes old and one three hours deep, and those are different situations. Measured to `to`
 * where one is given — a finished session has a lifetime, not an age — and to now while it runs.
 *
 * Two units at most, because this sits in an 18rem rail beside a title that needs the room. A span
 * under a minute is said in words rather than as a bare `0m`, and a start in this machine's future
 * reads as brand new: records travel between machines and clocks do not, so a mirrored record can
 * legitimately arrive stamped ahead of here, and `-4m` in a rail is a bug report nobody can act on.
 */
export function elapsed(from: string, to?: string | null): string {
  const end = to ? new Date(to).getTime() : Date.now();
  const minutes = Math.floor(Math.max(0, end - new Date(from).getTime()) / 60_000);
  if (minutes < 1) return i18n.t('duration.under');
  if (minutes < 60) return i18n.t('duration.minutes', { count: minutes });
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return i18n.t('duration.hours', { hours, minutes: minutes % 60 });
  return i18n.t('duration.days', { days: Math.floor(hours / 24), hours: hours % 24 });
}

/**
 * A short span in milliseconds — how long a turn took (CONV5), where `elapsed` measures a session.
 * Tenths under ten seconds, whole seconds under a minute, then minutes and seconds; an hour and more
 * reads as `elapsed` says it. `language` is the page's unless a caller words in another, as {@link list}'s is.
 */
export function span(ms: number, language?: string): string {
  const lng = language ? { lng: language } : {};
  const seconds = Math.max(0, ms) / 1000;
  if (seconds < 10) {
    return i18n.t('duration.seconds', {
      seconds: (Math.round(seconds * 10) / 10).toLocaleString(locale(language), { maximumFractionDigits: 1 }),
      ...lng,
    });
  }
  if (seconds < 60) return i18n.t('duration.seconds', { seconds: Math.round(seconds), ...lng });
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return i18n.t('duration.minuteSeconds', { minutes, seconds: Math.floor(seconds % 60), ...lng });
  return i18n.t('duration.hours', { hours: Math.floor(minutes / 60), minutes: minutes % 60, ...lng });
}

/**
 * A refusal whose sentence names something its record may not have, and the form to read where the shell sent it
 * empty (CHATTAKE1c, D126's CHATTAKE1 note): a chat that took a quest is refused as having served one, but no quest
 * records which session took it, so the refusal names none, and the sentence that names one read `#`.
 */
const FORM_WHEN_EMPTY: Readonly<Record<string, { parameter: string; context: string }>> = {
  SESSION_SERVED_QUEST: { parameter: 'quest', context: 'took' },
};

/**
 * The sentence a failure puts in front of the person, whichever half of the platform it came from.
 *
 * @remarks
 * Two transports, two shapes, one rule: **the person reads a sentence, never a code.**
 *
 * The HTTP service answers refusals as prose and the platform renders them VERBATIM — that is the
 * older half of this rule and it does not move. The shell's bridge is the other half: it rejects with
 * a structured `code` and `parameters`, because the framework's contract is that the client produces
 * the text (`errors.{code}`) — which is also what lets a refusal speak 中文.
 *
 * An unmapped code falls back to a sentence rather than to the code itself. It happens when the page
 * is older than the host, and a bare `SOMETHING_FAILED` in front of a person is barely better than the
 * generic failure this whole seam was fixed to replace.
 *
 * A form of the sentence is the shell's `context`, or, for a refusal in `FORM_WHEN_EMPTY`, the form that
 * names nothing where the shell sent that parameter empty.
 */
export function sentence(error: unknown, language?: string): string {
  const code = (error as { code?: unknown } | null)?.code;
  if (typeof code !== 'string' || !code) return (error as Error)?.message ?? '';

  // In a caller's language where it words in another than the page's (a story's or a test's), as `list` does.
  const lng = language ? { lng: language } : {};
  const parameters = (error as { parameters?: Record<string, string> }).parameters ?? {};
  const empty = FORM_WHEN_EMPTY[code];
  const values = empty && !parameters[empty.parameter] && !parameters.context
    ? { ...parameters, context: empty.context }
    : parameters;
  const key = `errors.${code}`;
  const translated = i18n.t(key, { ...values, ...lng });
  return translated === key ? i18n.t('errors.UNKNOWN', lng) : translated;
}

/**
 * What a session ran ON and AS (D49 §4): the harness, the version observed at spawn, and the named
 * credential profile.
 *
 * @remarks
 * Every part after the harness name is absent-tolerant, and each absence means something real rather
 * than something missing. No version: the record predates the toolchain, or the harness could not be
 * asked. No profile: it ran in the harness's own configuration home — or the reader is a browser over
 * a keyed remote, where the profile name deliberately never travels, machine-local like the
 * transcript. Neither is an error, so neither gets a placeholder that looks like one.
 *
 * Shared by the quest's page and the chat drawer for the same reason the console is: a conversation
 * is a session, and rendering it twice is how the two quietly stop agreeing.
 */
export function sessionTool(
  session: { adapter: string; harnessVersion?: string | null; profile?: string | null },
  /**
   * The session's agent has accounts, so a record naming none ran on the tool's own sign-in, said so (D125 §3.7, TOOL4m's
   * rest with UX6e). Left out, nothing is said of an account the record does not name.
   */
  ownSignIn = false,
  /**
   * What a person calls an account (ACCTNAME1, D152 §4.2), from the roster: the record keeps the id, and the line says the
   * name the person gave it, looked up as it is drawn. Left out, the id is said.
   */
  nameOf?: AccountNamer,
): string {
  return [
    session.adapter,
    session.harnessVersion || null,
    session.profile
      ? i18n.t('quests.session.asProfile', { profile: nameOf ? nameOf(session.adapter, session.profile) : session.profile })
      : ownSignIn ? i18n.t('quests.session.ownSignIn') : null,
  ].filter(Boolean).join(' · ');
}
