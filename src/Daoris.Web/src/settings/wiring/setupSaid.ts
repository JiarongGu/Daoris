import type { TFunction } from 'i18next';

/** The codes a set-up press is refused with, each worded in `settings.daoris.setup.refused.*`. */
export const SETUP_REFUSALS = [
  'PATH_NOT_INSTALL', 'PATH_NOT_WINDOWS', 'PATH_UNREADABLE', 'PATH_NOT_WRITTEN',
  'SERVICE_NOT_LOOPBACK', 'SERVICE_MALFORMED', 'SERVICE_NOT_WINDOWS', 'SERVICE_UNREADABLE', 'SERVICE_NOT_WRITTEN',
  'FOLDER_UNKNOWN', 'FOLDER_NOT_OPENED',
] as const;

/**
 * A set-up press's refusal in the catalogue's words, one per code; a code the catalogue has no word for says the press is refused and
 * passes the host's own sentence along, so a code the host adds later shows a sentence, never its spelling.
 */
export function setupSaid(t: TFunction, why: string | null | undefined, message: string | null | undefined): string {
  return (SETUP_REFUSALS as readonly string[]).includes(why ?? '')
    ? t(`settings.daoris.setup.refused.${why}`)
    : t('settings.daoris.setup.refused.other', { message: message ?? '' });
}
