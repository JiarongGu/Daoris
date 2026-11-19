import type { TFunction } from 'i18next';
import { useTranslation } from 'react-i18next';
import { elapsed } from '../../locales/format';
import type { Digest, DigestLine } from './digest';

/** One line of the digest in the reader's words; the agent's last words are quoted beside it, as written. */
function said(t: TFunction, line: DigestLine): string {
  switch (line.kind) {
    case 'turns':
      return t('work.catchup.turns', { count: line.count });
    case 'files':
      return t('work.catchup.files', { count: line.count });
    case 'commands':
      return [t('work.catchup.commands', { count: line.count }),
        ...(line.failed.length > 0 ? [t('work.catchup.failedNames', { names: line.failed.join(', ') })] : [])].join(' · ');
    case 'children':
    case 'tasks':
      return [t(`work.catchup.${line.kind}`, { started: line.started, ended: line.ended }),
        ...(line.kind === 'children' && line.nested ? [t('work.catchup.nested', { count: line.nested })] : []),
        ...(line.failed > 0 ? [t('work.catchup.endedFailed', { count: line.failed })] : [])].join(' · ');
    case 'steps':
      return [...(line.passed > 0 ? [t('work.catchup.steps', { count: line.passed })] : []),
        ...(line.failed.length > 0 ? [t('work.catchup.stepsFailed', { names: line.failed.join(', ') })] : [])].join(' · ');
    case 'waits':
      return [...(line.open > 0 ? [t('work.catchup.waiting', { count: line.open })] : []),
        ...(line.asked > 0 || line.answered > 0 ? [t('work.catchup.helpers', { asked: line.asked, answered: line.answered })] : [])]
        .join(' · ');
    case 'words':
      return t('work.catchup.words');
  }
}

/**
 * *Since you left*: the row at the person's read mark, how long ago they left and what happened since, one line each, computed
 * from the record and the run with no model. Each line with a row to go to is a press that jumps there; the agent's last words
 * are quoted as written, labelled as its own.
 */
export function SinceYouLeft({ digest, read, now, onJump }: {
  digest: Digest;
  /** When the mark last moved: when the person left. */
  read: string;
  now: number;
  /** Go to the block a line names, by its key. */
  onJump: (key: string) => void;
}) {
  const { t } = useTranslation();
  return (
    <section aria-label={t('work.catchup.label')} data-catchup className="grid min-w-0 scroll-mt-12 gap-1 border-t border-line pt-2">
      <p className="m-0 text-meta uppercase tracking-[0.06em] text-ink-faint">
        {t('work.catchup.since', { span: elapsed(read, new Date(now).toISOString()) })}
      </p>
      <ul className="m-0 grid list-none gap-0.5 p-0">
        {digest.lines.map((line) => {
          const words = said(t, line);
          const to = 'to' in line ? line.to : undefined;
          const body = line.kind === 'words'
            ? <>{words} <q className="text-ink">{line.text}</q></>
            : words;
          return (
            <li key={line.kind} className="text-small text-ink-soft wrap-anywhere">
              {to
                ? (
                  <button type="button" onClick={() => onJump(to)}
                    className="border-0 bg-transparent p-0 text-left text-small text-ink-soft hover:text-ink hover:underline">
                    {body}
                  </button>
                )
                : body}
            </li>
          );
        })}
      </ul>
    </section>
  );
}
