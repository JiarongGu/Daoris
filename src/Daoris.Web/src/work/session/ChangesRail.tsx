import { useState } from 'react';
import { type TFunction } from 'i18next';
import { useTranslation } from 'react-i18next';
import { Button, PathText } from '../../design/ui';
import { clockOf } from '../../locales/format';
import { cn } from '../../lib/cn';

/** One file the session's work changed, with its lines added and removed. */
export type ChangedFile = { path: string; change: 'A' | 'M' | 'D' | 'R'; added: number; removed: number };

/** What the session's work changed in its repository: its files, its commits on the branch, its pull request and its checks. */
export type ChangesFacts = {
  files: ChangedFile[];
  commits: number | null;
  pullRequest: { name: string; href?: string } | null;
  checks: { passed: number; of: number; at: string } | null;
};

/** Files shown before *+ n more*. */
const SHOWN = 4;

/** The changes as one fact, the narrow head's: "5 files +248 −37". */
export function changesFact(t: TFunction, changes: ChangesFacts): string {
  const added = changes.files.reduce((sum, file) => sum + file.added, 0);
  const removed = changes.files.reduce((sum, file) => sum + file.removed, 0);
  return t('work.session.head.changes', { count: changes.files.length, added, removed });
}

const CHANGE_INK: Readonly<Record<ChangedFile['change'], string>> = {
  A: 'text-ok-ink', M: 'text-live-ink', D: 'text-fail-ink', R: 'text-ink-soft',
};

/**
 * **The Changes rail**: state, never events. Its files with their lines added and removed (four, then *+ n more*), its commits, its
 * pull request and its checks, and *Review the changes*. It stands beside the story from 68rem of the panel's width; narrower, the
 * head carries it as one fact.
 */
export function ChangesRail({ changes, now, onReview, onFile }: {
  changes: ChangesFacts;
  now: number;
  onReview?: () => void;
  onFile?: (path: string) => void;
}) {
  const { t, i18n } = useTranslation();
  const [all, setAll] = useState(false);
  const shown = all ? changes.files : changes.files.slice(0, SHOWN);
  const more = changes.files.length - shown.length;
  return (
    <section aria-label={t('work.session.changes.title')} className="grid content-start gap-1.5">
      <h3 className="m-0 flex items-baseline gap-2 text-caption font-semibold uppercase tracking-wide text-ink-soft">
        {t('work.session.changes.title')}
        {changes.files.length > 0 && <span className="font-normal normal-case tracking-normal text-ink-faint">{changesFact(t, changes)}</span>}
      </h3>
      {changes.files.length === 0
        ? <p className="m-0 text-small text-ink-faint">{t('work.session.changes.none')}</p>
        : (
          <ul className="m-0 grid list-none gap-0.5 p-0">
            {shown.map((file) => (
              <li key={file.path}>
                <button
                  type="button"
                  onClick={() => onFile?.(file.path)}
                  className="grid w-full cursor-pointer grid-cols-[1rem_minmax(0,1fr)_auto] items-baseline gap-1.5 rounded-control border-0 bg-transparent px-1 py-0.5 text-left text-small hover:bg-sunken"
                >
                  <span className={cn('font-mono text-caption', CHANGE_INK[file.change])}>{file.change}</span>
                  <PathText path={file.path} className="text-ink" />
                  <span className="whitespace-nowrap font-mono text-caption">
                    <span className="text-ok-ink">+{file.added}</span> <span className="text-fail-ink">−{file.removed}</span>
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
      {more > 0 && (
        <button type="button" onClick={() => setAll(true)} className="w-fit cursor-pointer border-0 bg-transparent p-0 text-small text-accent-ink">
          {t('work.session.changes.more', { count: more })}
        </button>
      )}
      <p className="m-0 text-small text-ink-soft">
        {[
          changes.commits !== null ? t('work.session.changes.commits', { count: changes.commits }) : null,
          changes.pullRequest ? t('work.session.changes.pr', { name: changes.pullRequest.name }) : t('work.session.changes.noPr'),
        ].filter(Boolean).join(' · ')}
      </p>
      {changes.checks && (
        <p className={cn('m-0 text-small', changes.checks.passed === changes.checks.of ? 'text-ok-ink' : 'text-fail-ink')}>
          {changes.checks.passed === changes.checks.of ? '✓ ' : '▲ '}
          {t('work.session.changes.checks', { ...changes.checks, time: clockOf(changes.checks.at, new Date(now), i18n.language) })}
        </p>
      )}
      {onReview && changes.files.length > 0 && (
        <Button variant="ghost" className="w-fit px-0" onClick={onReview}>{t('work.session.changes.review')} ▸</Button>
      )}
    </section>
  );
}
