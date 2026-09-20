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
