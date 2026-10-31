import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Prose } from '../../design/ui';
import type { GitRead } from './RepositoryGit';

export type ConflictTake = 'ours' | 'theirs' | 'both';

/** One conflict of a file as the driver read it: what each side holds there, and a line or two of the text around it. */
export type ConflictHunkRead = { id: string; before: readonly string[]; ours: readonly string[]; theirs: readonly string[]; after: readonly string[] };

/** What the list shows and a press sends back (the bridge's `GitFileConflict`, handed as it came). */
export type ConflictValue = {
  head: string | null;
  base: string;
  ours: string;
  theirs: string;
  worktree: string;
  why: string | null;
  hunks: readonly ConflictHunkRead[];
};

export type ConflictPress = { picks: { id: string; take: ConflictTake }[]; faces: Pick<ConflictValue, 'head' | 'base' | 'ours' | 'theirs' | 'worktree'> };

const TAKES: readonly ConflictTake[] = ['ours', 'theirs', 'both'];

/**
 * **A conflicted file's conflicts in the Changes tab** (GITAPP2j, D204, the git-writes design §8): each conflict with what ours and theirs hold
 * there, and a choice of ours, theirs or both. The file is written once every conflict has a choice; what is sent back is the stages and the
 * file's id it was read with, so the driver refuses a file that moved since. A file the driver cannot take by hunk says why instead.
 *
 * @remarks **A molecule**: the read arrives as a prop; `busy` holds every press while an act runs.
 */
export function ConflictHunks({ read, busy, onPress }: {
  read: GitRead<ConflictValue>;
  busy: boolean;
  onPress: (press: ConflictPress) => void;
}) {
  const { t } = useTranslation();
  const [taken, setTaken] = useState<Readonly<Record<string, ConflictTake>>>({});
  if (read.state === 'reading') return <Prose className="text-small text-ink-soft">{t('projects.git.changes.conflict.reading')}</Prose>;
  if (read.state === 'refused') return <Prose className="text-small text-ink-soft">{read.sentence}</Prose>;
  if (read.state === 'noCheckout') return null;
  const { why, hunks, head, base, ours, theirs, worktree } = read.value;
  if (why) return <Prose className="text-small text-ink-soft">{why}</Prose>;
  const chosen = hunks.filter((hunk) => taken[hunk.id]).length;
  const everywhere = (take: ConflictTake) => setTaken(Object.fromEntries(hunks.map((hunk) => [hunk.id, take])));
  return (
    <div className="grid min-w-0 gap-2">
      <Prose className="text-small text-ink-soft">{t('projects.git.changes.conflict.help')}</Prose>
      <div className="flex flex-wrap gap-2">
        <Button variant="ghost" disabled={busy} onClick={() => everywhere('ours')}>{t('projects.git.changes.conflict.all.ours')}</Button>
        <Button variant="ghost" disabled={busy} onClick={() => everywhere('theirs')}>{t('projects.git.changes.conflict.all.theirs')}</Button>
      </div>
      {hunks.map((hunk, at) => (
        <ConflictBlock
          key={hunk.id}
          hunk={hunk}
          at={at + 1}
          count={hunks.length}
          take={taken[hunk.id]}
          busy={busy}
          onTake={(take) => setTaken({ ...taken, [hunk.id]: take })}
        />
      ))}
      <div className="flex flex-wrap gap-2">
        <Button
          disabled={busy || chosen < hunks.length}
          onClick={() => onPress({ picks: hunks.map((hunk) => ({ id: hunk.id, take: taken[hunk.id]! })), faces: { head, base, ours, theirs, worktree } })}
        >
          {t('projects.git.changes.conflict.resolve', { chosen, count: hunks.length })}
        </Button>
      </div>
    </div>
  );
}

function ConflictBlock({ hunk, at, count, take, busy, onTake }: {
  hunk: ConflictHunkRead;
  at: number;
  count: number;
  take: ConflictTake | undefined;
  busy: boolean;
  onTake: (take: ConflictTake) => void;
}) {
  const { t } = useTranslation();
  return (
    <div className="grid min-w-0 gap-1 rounded-control border border-line p-2" data-conflict={hunk.id}>
      <p className="m-0 text-meta font-semibold text-ink-faint">{t('projects.git.changes.conflict.heading', { at, count })}</p>
      <Lines lines={hunk.before} tone="text-ink-soft" />
      <Side label={t('projects.git.changes.conflict.ours')} lines={hunk.ours} tone="text-ink-done" />
      <Side label={t('projects.git.changes.conflict.theirs')} lines={hunk.theirs} tone="text-ink-danger" />
      <Lines lines={hunk.after} tone="text-ink-soft" />
      <div role="group" aria-label={t('projects.git.changes.conflict.choose', { at })} className="flex flex-wrap gap-2">
        {TAKES.map((each) => (
          <Button key={each} variant={take === each ? 'default' : 'ghost'} aria-pressed={take === each} disabled={busy} onClick={() => onTake(each)}>
            {t(`projects.git.changes.conflict.take.${each}`)}
          </Button>
        ))}
      </div>
    </div>
  );
}

function Side({ label, lines, tone }: { label: string; lines: readonly string[]; tone: string }) {
  const { t } = useTranslation();
  return (
    <div className="grid min-w-0 gap-0.5">
      <span className="text-meta text-ink-faint">{label}</span>
      {lines.length === 0 ? <span className="text-small text-ink-soft">{t('projects.git.changes.conflict.empty')}</span> : <Lines lines={lines} tone={tone} />}
    </div>
  );
}

function Lines({ lines, tone }: { lines: readonly string[]; tone: string }) {
  return (
    <ul className="m-0 grid min-w-0 list-none p-0">
      {lines.map((line, at) => (
        <li key={at}>
          <code className={`min-w-0 whitespace-pre-wrap font-mono text-small [overflow-wrap:anywhere] ${tone}`}>{line}</code>
        </li>
      ))}
    </ul>
  );
}
