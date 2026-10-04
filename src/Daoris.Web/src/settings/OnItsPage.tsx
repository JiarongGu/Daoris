import { useTranslation } from 'react-i18next';
import type { StarterDoor } from '../help/starters';
import { Button } from '../ui';

/**
 * The door from a list to where a repository's own value is set (UX6f): its page at Setup, or with no repository named,
 * Repositories at Setup — through the application's one opener, which every Settings door goes by.
 */
export const setupDoor = (onGo: (door: StarterDoor) => void) => (repository: string | null) =>
  onGo({ view: 'projects', ...(repository ? { item: repository } : {}), tab: 'setup' });

/**
 * **Where a list's rows per repository went** (UX6f, D150 §3.1): a repository's own value is set on its page, under
 * Setup, since a setting has one home (§1 rule 1). One line beneath a workspace's default: the repositories there that
 * set their own, each a door to its Setup, or that none does, with a door to Repositories at Setup. It stands until UX6g
 * retires the domain, and the workspace's default above it moves to the workspace's page.
 *
 * Props only, no hook from the query layer or the shell (components §2). With no door handed, the names are words.
 */
export function OnItsPage({ own, onOpen }: {
  /** The repositories in this workspace that set a value of their own here. */
  own: string[];
  /** Open a repository's page at Setup, or with null Repositories at Setup. */
  onOpen?: (repository: string | null) => void;
}) {
  const { t } = useTranslation();
  return (
    <p className="m-0 mt-1 flex flex-wrap items-center gap-x-1 gap-y-0.5 text-small text-ink-soft">
      <span className="mr-1">{t(own.length > 0 ? 'settings.onItsPage.some' : 'settings.onItsPage.none')}</span>
      {own.length > 0 && !onOpen && <span>{own.join(t('settings.onItsPage.listJoin'))}</span>}
      {onOpen && own.map((repository) => (
        <Button
          key={repository}
          variant="ghost"
          className="px-1.5 text-small text-accent"
          aria-label={t('settings.onItsPage.door', { repository })}
          onClick={() => onOpen(repository)}
        >
          {repository}
        </Button>
      ))}
      {onOpen && own.length === 0 && (
        <Button variant="ghost" className="px-1.5 text-small text-accent" onClick={() => onOpen(null)}>
          {t('settings.onItsPage.open')}
        </Button>
      )}
    </p>
  );
}
