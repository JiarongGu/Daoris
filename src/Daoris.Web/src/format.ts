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
