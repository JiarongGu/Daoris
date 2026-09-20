import i18n from './i18n';

// Formatting for the management surface. Small on purpose: a helper here is one the views share,
// not a utility belt.

/** Compact counts for stat tiles: 854 · 12.9K · 4.2M. Proportional figures — never tabular-nums. */
export function compact(value: number): string {
  if (value >= 1_000_000) return `${(value / 1_000_000).toFixed(1).replace(/\.0$/, '')}M`;
  if (value >= 10_000) return `${(value / 1_000).toFixed(1).replace(/\.0$/, '')}K`;
  return value.toLocaleString(i18n.language);
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
 */
export function sentence(error: unknown): string {
  const code = (error as { code?: unknown } | null)?.code;
  if (typeof code !== 'string' || !code) return (error as Error)?.message ?? '';

  const parameters = (error as { parameters?: Record<string, string> }).parameters ?? {};
  const key = `errors.${code}`;
  const translated = i18n.t(key, parameters);
  return translated === key ? i18n.t('errors.UNKNOWN') : translated;
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
 * Shared by the quest drawer and the chat drawer for the same reason the console is: a conversation
 * is a session, and rendering it twice is how the two quietly stop agreeing.
 */
export function sessionTool(
  session: { adapter: string; harnessVersion?: string | null; profile?: string | null },
): string {
  return [
    session.adapter,
    session.harnessVersion || null,
    session.profile ? i18n.t('quests.session.asProfile', { profile: session.profile }) : null,
  ].filter(Boolean).join(' · ');
}
