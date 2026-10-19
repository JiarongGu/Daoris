import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ago } from '../../locales/format';
import type { LogRow } from '../../settings/Logs';
import { Button } from '../../design/ui';
import { unitOfBranch, type MilestoneUnit } from '../../work/head/milestone';
import { MilestoneLine } from '../../work/head/WorkMilestone';

/** Rows drawn before the rest are one press away, as the groups above are (UX6h2b2). */
export const TIDIED_SHOWN = 5;

/** The events the look writes for a branch it cleaned up, and for one it could not (AUTOTIDY1, D88). */
export const TIDIED_EVENTS = ['branch.tidied', 'branch.kept'] as const;

/** The reasons a branch was left, as the machine log's design §4 names them; any other code is said with no reason. */
const WHY = ['unread', 'refused', 'busy', 'hidden', 'nested', 'linked', 'starting', 'sessions'] as const;

/** Which `unread` it was (AUTOTIDY1a2): a code from the driver, never the sentence with its path (D47 §4); any other is left unworded. */
const CAUSE = ['toplevel', 'refs', 'list', 'folder', 'index', 'folders', 'pass', 'judged'] as const;

const text = (value: unknown) => (typeof value === 'string' && value ? value : null);

/** A row: the newest line of its group, and the oldest's time where the group is more than one line. */
type Group = { line: LogRow; oldest: string | null };

/**
 * The driver says a reason once while it lasts, which is once at each start of the window, so one branch kept for a week is a
 * line a start. Lines of one branch, repository, reason and cause are one row (AUTOTIDY1a2); a cleaned-up branch goes once, so
 * is a row by its own nature. Order is the lines' own, newest first.
 */
function group(rows: readonly LogRow[]): Group[] {
  const groups = new Map<string, { line: LogRow; first: number; last: number; count: number }>();
  rows.forEach((line, index) => {
    const at = Date.parse(line.time);
    const key = line.event === 'branch.tidied' ? `tidied\n${index}`
      : ['kept', line.data.branch, line.data.repository, line.data.why, line.data.cause].map((part) => text(part) ?? '').join('\n');
    const held = groups.get(key);
    if (!held) { groups.set(key, { line, first: at, last: at, count: 1 }); return; }
    held.count += 1;
    if (at > held.first) { held.line = line; held.first = at; }
    if (at < held.last) held.last = at;
  });
  return [...groups.values()].map((held) => ({
    line: held.line, oldest: held.count > 1 ? new Date(held.last).toISOString() : null,
  }));
}

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
export function WorkspaceTidied({ lines, titles = {}, units = [] }: {
  lines: readonly LogRow[] | null | undefined;
  /**
   * Each unit's milestone (FABRIC-MILESTONE1b, the fabric events design §D0): a row whose branch one names says, under it, where
   * that work stands: the end, the clean-up and the recovery kept. Absent in a browser, which has no driver to read it from.
   */
  units?: readonly MilestoneUnit[];
  /** Session ids to the names the page gives them (TIDIEDLANDING1): a landing's line names its session by id, shown by name where known. */
  titles?: Readonly<Record<string, string>>;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const rows = group((lines ?? []).filter((line) => line.event === 'branch.tidied' || line.event === 'branch.kept'));
  if (rows.length === 0) return null;

  const shown = open ? rows : rows.slice(0, TIDIED_SHOWN);
  const hidden = rows.length - shown.length;
  return (
    <section className="grid min-w-0 gap-2 border-t border-line pt-4">
      <h2 className="m-0 text-body font-semibold text-ink">{t('projects.tidied.title')}</h2>
      <ul className="m-0 grid list-none gap-2 p-0">
        {shown.map((row, index) => <Tidied key={`${row.line.time}-${index}`} line={row.line} oldest={row.oldest} titles={titles} units={units} />)}
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
function Tidied({ line, oldest, titles, units }: {
  line: LogRow; oldest: string | null; titles: Readonly<Record<string, string>>; units: readonly MilestoneUnit[];
}) {
  const { t } = useTranslation();
  const branch = text(line.data.branch);
  // A landing's branch name carries the quest title's words, which the log never holds (AUTOTIDY2b, D88): its line names the
  // session whose work it carried and no branch (TIDIEDLANDING1).
  const session = line.event === 'branch.tidied' && !branch ? text(line.data.session) : null;
  const repository = text(line.data.repository);
  const unit = branch && repository ? unitOfBranch(units, repository, branch) : undefined;
  const done = line.event === 'branch.tidied';
  const why = WHY.find((code) => code === line.data.why);
  const cause = CAUSE.find((code) => code === line.data.cause);
  const meta = [
    repository,
    t(done ? 'projects.tidied.done' : 'projects.tidied.kept', { when: ago(line.time) }),
    oldest ? t('projects.tidied.since', { when: ago(oldest) }) : null,
    // A folder git lists no tree in, or a tree whose opening was cut off (AUTOTIDY2c, OPENCUT1, TIDIEDSTRAY1): said by its kind.
    done && line.data.tree === true
      ? t(line.data.kind === 'stray' ? 'projects.tidied.stray' : line.data.kind === 'opening' ? 'projects.tidied.opening' : 'projects.tidied.aside')
      : null,
    !done && why ? t(`projects.tidied.why.${why}`) : null,
    !done && why === 'unread' && cause ? t(`projects.tidied.cause.${cause}`) : null,
  ].filter(Boolean);

  return (
    <li className="grid min-w-0 gap-0.5">
      {branch && <span className="font-mono text-small text-ink [overflow-wrap:anywhere]">{branch}</span>}
      {session && <span className="text-small text-ink">{t('projects.tidied.landing', { session: titles[session] ?? session })}</span>}
      <span className="text-small text-ink-soft">{meta.join(' · ')}</span>
      {unit && <MilestoneLine unit={unit} />}
    </li>
  );
}
