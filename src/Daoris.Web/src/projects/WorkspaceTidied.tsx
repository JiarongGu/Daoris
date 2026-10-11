import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ago } from '../format';
import type { LogRow } from '../settings/Logs';
import { Button } from '../ui';

/** Rows drawn before the rest are one press away, as the groups above are (UX6h2b2). */
export const TIDIED_SHOWN = 5;

/** The events the look writes for a branch it cleaned up, and for one it could not (AUTOTIDY1, D88). */
export const TIDIED_EVENTS = ['branch.tidied', 'branch.kept'] as const;

/** The reasons a branch was left, as the machine log's design §4 names them; any other code is said with no reason. */
const WHY = ['unread', 'refused', 'busy', 'hidden', 'nested', 'linked', 'starting', 'sessions'] as const;

const text = (value: unknown) => (typeof value === 'string' && value ? value : null);

/**
 * **What the look cleaned up on its own** (AUTOTIDY1a, D88): the workspace's recent `branch.tidied` and `branch.kept` lines
 * of the machine log, newest first, five shown and the rest behind *Show N more*. Nothing is drawn where there is nothing.
 *
 * @remarks
 * **A molecule**: the lines arrive as a prop, read by the holder from the shell, which is the log's only reader (D94, D47 §4),
 * so a browser passes none. A line's own words are never shown: a branch is Daoris's own name (`daoris/s-…`), a reason is a
 * word of the page's by its code, and a folder, which is the machine's, is never named. A code the page does not know is
 * left unworded rather than written out.
 */
export function WorkspaceTidied({ lines }: { lines: readonly LogRow[] | null | undefined }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const rows = (lines ?? []).filter((line) => line.event === 'branch.tidied' || line.event === 'branch.kept');
  if (rows.length === 0) return null;

  const shown = open ? rows : rows.slice(0, TIDIED_SHOWN);
  const hidden = rows.length - shown.length;
  return (
    <section className="grid min-w-0 gap-2 border-t border-line pt-4">
      <h2 className="m-0 text-body font-semibold text-ink">{t('projects.tidied.title')}</h2>
      <ul className="m-0 grid list-none gap-2 p-0">
        {shown.map((line, index) => <Tidied key={`${line.time}-${index}`} line={line} />)}
      </ul>
      {hidden > 0 && (
        <div>
          <Button variant="ghost" className="text-small" onClick={() => setOpen(true)}>{t('work.list.showMore', { count: hidden })}</Button>
        </div>
      )}
    </section>
  );
}

/** One line: the branch where the log names one, then what became of it and when, with its repository. */
function Tidied({ line }: { line: LogRow }) {
  const { t } = useTranslation();
  const branch = text(line.data.branch);
  const repository = text(line.data.repository);
  const done = line.event === 'branch.tidied';
  const why = WHY.find((code) => code === line.data.why);
  const meta = [
    repository,
    t(done ? 'projects.tidied.done' : 'projects.tidied.kept', { when: ago(line.time) }),
    done && line.data.tree === true ? t('projects.tidied.aside') : null,
    !done && why ? t(`projects.tidied.why.${why}`) : null,
  ].filter(Boolean);

  return (
    <li className="grid min-w-0 gap-0.5">
      {branch && <span className="font-mono text-small text-ink [overflow-wrap:anywhere]">{branch}</span>}
      <span className="text-small text-ink-soft">{meta.join(' · ')}</span>
    </li>
  );
}
