import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { Button, Inline } from '../../design/ui';
import { ago, stamp } from '../../locales/format';
import type { RefActs } from './NewRef';

/**
 * One delete the journal holds (ONEWAY1r): the act `GIT_REF_UNDO` takes, what went and at which commit, who pressed it, and `closed`, why
 * its Undo is shut (`undone` · `name-taken` · `commit-gone`), null where it is open.
 */
export type GitDeletedRef = {
  act: string;
  kind: 'branch' | 'tag';
  name: string;
  commit: string;
  at: string;
  press: string;
  recovery: string | null;
  closed: string | null;
};

/** Who pressed a delete, as the journal records it: Daoris's own door, the window or the terminal. */
const who = (t: TFunction, press: string): string =>
  press === 'daoris' ? t('projects.git.deleted.by.daoris') : press === 'terminal' ? t('projects.git.deleted.by.terminal') : t('projects.git.deleted.by.window');

/** Why an Undo is shut, in the catalogue's words. */
const shut = (t: TFunction, row: GitDeletedRef): string =>
  row.closed === 'undone' ? t('projects.git.deleted.closed.undone')
    : row.closed === 'name-taken' ? t('projects.git.deleted.closed.nameTaken', { name: row.name })
      : t('projects.git.deleted.closed.commitGone');

/**
 * **A repository's deletes, as the journal holds them** (ONEWAY1r, D204, D228): each branch or tag deleted, by the person or by a door of
 * Daoris's, with the *Undo* its act carries. A row whose Undo is shut says why, in words; a refusal an Undo met is the driver's sentence,
 * shown as written.
 *
 * `unrecorded` counts the commits kept for recovery that no journal record names, kept before every keep was journaled: said under the
 * list, since the old recovery list stays for them alone.
 *
 * @remarks
 * **A molecule**: the rows and the acts arrive as props; a repository with no delete draws nothing.
 */
export function DeletedRefs({ rows, unrecorded = 0, acts }: { rows: readonly GitDeletedRef[]; unrecorded?: number; acts: Pick<RefActs, 'undo'> }) {
  const { t } = useTranslation();
  const named = useId();
  if (rows.length === 0) return null;
  return (
    <section aria-labelledby={named} className="grid gap-1">
      <h3 id={named} className="m-0 text-small font-semibold text-ink-faint">{t('projects.git.deleted.heading')}</h3>
      <ul className="m-0 grid list-none p-0">
        {rows.map((row) => <DeletedRow key={row.act} row={row} acts={acts} />)}
      </ul>
      {unrecorded > 0 && <p className="m-0 text-small text-ink-soft">{t('projects.git.deleted.unrecorded', { count: unrecorded })}</p>}
    </section>
  );
}

type Step = { is: 'open' } | { is: 'undoing' } | { is: 'undone' } | { is: 'said'; sentence: string } | { is: 'unknown' };

function DeletedRow({ row, acts }: { row: GitDeletedRef; acts: Pick<RefActs, 'undo'> }) {
  const { t } = useTranslation();
  const [step, setStep] = useState<Step>({ is: 'open' });
  const undo = async () => {
    setStep({ is: 'undoing' });
    try {
      const outcome = await acts.undo(row.act);
      if (!outcome.refused && outcome.outcome === 'done') setStep({ is: 'undone' });
      else if (outcome.refused || outcome.outcome === 'not-done') setStep({ is: 'said', sentence: outcome.said });
      else setStep({ is: 'unknown' });
    } catch (error) {
      setStep({ is: 'said', sentence: error instanceof Error ? error.message : t('projects.git.ref.failed') });
    }
  };

  const open = row.closed === null && (step.is === 'open' || step.is === 'undoing');
  const words = row.closed !== null ? shut(t, row)
    : step.is === 'undone' ? t('projects.git.deleted.closed.undone')
      : step.is === 'said' ? step.sentence
        : step.is === 'unknown' ? t('projects.git.ref.unknown') : null;
  return (
    <li className="grid min-w-0 gap-0.5 border-b border-line py-1.5 last:border-b-0">
      <div className="flex min-w-0 flex-wrap items-baseline gap-x-2">
        <span className="min-w-0 [overflow-wrap:anywhere]"><Inline text={row.name} /></span>
        <span className="text-meta text-ink-faint">{t(`projects.git.deleted.kind.${row.kind}`)}</span>
        <time dateTime={row.at} title={stamp(row.at)} className="shrink-0 text-meta text-ink-faint tabular-nums">{ago(row.at)}</time>
      </div>
      <p className="m-0 text-small text-ink-soft">
        {t('projects.git.deleted.meta', { id: row.commit.slice(0, 12), who: who(t, row.press) })}
        {row.recovery !== null && ` ${t('projects.git.deleted.kept')}`}
      </p>
      {words && <p role="status" className="m-0 text-small text-ink [overflow-wrap:anywhere]">{words}</p>}
      {open && (
        <div>
          <Button
            variant="ghost"
            className="text-small"
            disabled={step.is === 'undoing'}
            aria-label={t('projects.git.deleted.undoLabel', { name: row.name })}
            onClick={() => { void undo(); }}
          >
            {t('projects.git.ref.undo')}
          </Button>
        </div>
      )}
    </li>
  );
}
