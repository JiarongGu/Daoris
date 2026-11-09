import { useTranslation } from 'react-i18next';
import { ago } from '../../locales/format';
import { Card, CardHeader, EmptyState, Pill, Prose } from '../../design/ui';

/** Why a question went to git's own program instead of the library, with how often. */
export type GitLibraryReasonShown = { reason: string; count: number };

/** What one repository read: the answers the library gave and the reads git answered, each with its reason. */
export type GitLibraryReadShown = {
  repository: string;
  library: number;
  git: number;
  reasons: GitLibraryReasonShown[];
  last: string;
};

/** The reasons the driver says; one it does not know is shown as sent. */
const KNOWN = [
  'no-repository', 'reftable', 'sha256', 'object-format', 'ref-format', 'replacements', 'configuration', 'unanswered', 'failed', 'native-missing', 'native-fault',
];

/**
 * *Git reads*: what each repository read through the git library and through git's own program, with the reason each
 * read that fell back to git did. Machine-local counts since the journal began; a repository is its folder's name.
 */
export function GitReads({ reads }: { reads: GitLibraryReadShown[] }) {
  const { t } = useTranslation();
  return (
    <Card id="settings-git-reads" className="scroll-mt-3">
      <CardHeader title={t('settings.git.reads')} />
      <Prose className="mt-1 text-small">{t('settings.git.readsWhy')}</Prose>
      {reads.length === 0
        ? <EmptyState icon="info" headline={t('settings.git.reads.empty.headline')} body={t('settings.git.reads.empty.body')} />
        : (
          <ul className="m-0 mt-2 grid list-none gap-2 p-0">
            {reads.map((each) => (
              <li key={each.repository} className="grid min-w-0 gap-1">
                <div className="flex min-w-0 flex-wrap items-center gap-2">
                  <span className="wrap-anywhere text-body text-ink">{each.repository}</span>
                  <Pill>{t('settings.git.reads.library', { count: each.library })}</Pill>
                  <Pill>{t('settings.git.reads.git', { count: each.git })}</Pill>
                  <span className="text-small text-ink-soft">{t('settings.git.reads.last', { time: ago(each.last) })}</span>
                </div>
                {each.reasons.length > 0 && (
                  <ul className="m-0 grid list-none gap-0.5 p-0 text-small text-ink-soft">
                    {each.reasons.map((why) => (
                      <li key={why.reason}>
                        {t('settings.git.reads.because', { reason: KNOWN.includes(why.reason) ? t(`settings.git.reads.reason.${why.reason}`) : why.reason, count: why.count })}
                      </li>
                    ))}
                  </ul>
                )}
              </li>
            ))}
          </ul>
        )}
    </Card>
  );
}
