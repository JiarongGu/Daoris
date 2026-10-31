import { useTranslation } from 'react-i18next';
import { moment } from '../../locales/format';
import type { SessionRecord } from './sessionRecord';
import { short } from './trace';

/**
 * **What was kept of its work** (KEEPSIZE1): the record of the tree a finished session ran in, shown on its page once the tree is let
 * go: where it landed, its tip and the recovery ref that holds it, its commits, and what the tree held that no commit holds.
 *
 * @remarks
 * A molecule: handed the record, it reads the catalogue and nothing else. A commit's subject and a branch's name are someone's words,
 * shown as written. A tree's folder is never shown (no path on the page).
 */
export function FinishedRecord({ record }: { record: SessionRecord }) {
  const { t } = useTranslation();
  const { post } = record;
  const commits = post.commits ?? [];
  return (
    <section className="grid gap-1.5 border-l-[3px] border-line-strong bg-page/60 px-3.5 py-2" aria-label={t('work.record.title')}>
      <h3 className="m-0 text-body font-medium text-ink">{t('work.record.title')}</h3>
      <p className="m-0 text-small text-ink-soft">{t('work.record.hint', { when: moment(record.at) })}</p>
      <p className="m-0 text-small text-ink-soft">
        {post.landing
          ? t(post.landing.pullRequest ? 'work.record.landedPull' : 'work.record.landed', {
            branch: post.landing.branch, line: post.landing.line ?? post.line ?? '', when: moment(post.landing.at), pull: post.landing.pullRequest ?? '',
          })
          : t('work.record.notLanded')}
      </p>
      <p className="m-0 text-small text-ink-soft">{t('work.record.tip', { branch: post.branch, tip: short(post.tip) })}</p>
      <p className="m-0 text-small text-ink-soft">{t(post.ref ? 'work.record.ref' : 'work.record.noRef', { ref: post.ref ?? '' })}</p>
      {post.commits === null
        ? <p className="m-0 text-small text-ink-soft">{t('work.record.commitsUnread')}</p>
        : (
          <div className="grid gap-0.5">
            <p className="m-0 text-small text-ink-faint">{t('work.record.commits', { count: commits.length })}</p>
            <ul className="m-0 grid list-none gap-0.5 p-0">
              {commits.map((commit) => (
                <li key={commit.id} className="text-small text-ink-soft">
                  <span className="font-mono text-ink-faint">{short(commit.id)}</span>{' '}{commit.subject}
                  <span className="text-ink-faint">{' · '}{t('work.record.files', { count: commit.files.length })}</span>
                </li>
              ))}
            </ul>
          </div>
        )}
      {post.left.count > 0 && (
        <p className="m-0 text-small text-ink-soft">
          {t('work.record.left', { count: post.left.count, names: post.left.names.join(', ') })}
          {post.left.cut ? ` ${t('work.record.leftCut')}` : ''}
        </p>
      )}
    </section>
  );
}
