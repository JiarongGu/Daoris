import { type ComponentType, type ReactNode, useCallback, useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline, Pill, Prose, SkeletonRows } from '../../design/ui';
import { clockOf } from '../../locales/format';
import { BranchList, type BranchListValue } from './BranchList';
import type { RefActs } from './NewRef';
import { DeletedRefs, type GitDeletedRef } from './DeletedRefs';
import { CommitGraph, type GraphCommitRow } from './CommitGraph';
import { ListMore } from '../../work/frame/ListPane';
import { GraphFoot, useGraphPaging } from './GraphFoot';
import type { HunkPress } from './ChangeHunks';
import type { ConflictPress } from './ConflictHunks';

const noop = () => {};

/**
 * What a Git tab shows (UX6h1): its answer on its way, no checkout of the repository on this machine, why Git did not answer
 * in words the page says (`errors.{code}`, or the driver's sentence), or what it read.
 */
export type GitRead<T> =
  | { state: 'reading' }
  | { state: 'noCheckout' }
  | { state: 'refused'; sentence: string }
  | { state: 'read'; value: T };

/**
 * What History reads: its line's commits from the tips walked, the pages so far accumulated, with whether the graph answers
 * another (`more`), the next page on its way (`loadingMore`) or refused in words (`moreError`); or that its line holds no
 * commit to walk from here.
 */
export type HistoryRead =
  | GitRead<{ line: string; tips: string[]; commits: GraphCommitRow[]; more: boolean; loadingMore?: boolean; moreError?: string | null }>
  | { state: 'noLine'; line: string | null };

/**
 * What a Git tab has nothing to show for: its fact as a headline and why beneath it. A block of the page, so it wraps at the
 * pane's edge as every block of a record page does (D141), where the main area's own empty state keeps a narrow body. A
 * workspace's Branches says its own the same way (UX6h2b2).
 */
export function Nothing({ headline, body }: { headline: string; body: string }) {
  return (
    <div className="grid gap-1">
      <p className="m-0 text-body font-semibold text-ink">{headline}</p>
      <Prose className="text-small text-ink-soft"><Inline text={body} /></Prose>
    </div>
  );
}

/** A Git tab's answer on its way, said and drawn as rows, or why it did not come: never a blank tab. */
export function Waiting({ sentence, reading }: { sentence: string; reading: boolean }) {
  return (
    <div aria-busy={reading || undefined} className="grid gap-2">
      <Prose className="text-small"><Inline text={sentence} /></Prose>
      {reading && <SkeletonRows rows={4} />}
    </div>
  );
}

/**
 * **A repository's Branches tab** (UX6h1, D150 §4.2, §4.4): its branches as `GIT_BRANCHES` reads them for it, the screen's
 * twin of `daoris git branches --repository`. While the list is on its way it says so; a repository with no checkout
 * here, or one Git did not answer for, says why in its place; a checkout with no commit has no branch yet.
 *
 * @remarks
 * **A molecule**: the read arrives as a prop, and its holder reads it only while the tab shows. What it read is the list by
 * kind (UX6h2a, `BranchList`): the line at its head, then sessions', landed and yours, each session's branch named by the
 * title its holder joined. A branch's name opens its page, and the line's opens History, where its holder hands the presses
 * (UX6h3b).
 */
export function BranchesTab({ repository, read, titles, onOpen, onOpenHistory, acts, deleted, unrecorded }: {
  repository: string;
  read: GitRead<BranchListValue>;
  /** The deletes the journal holds (ONEWAY1r), listed under the branches with each one's Undo; absent, the tab lists none. */
  deleted?: readonly GitDeletedRef[];
  /** The commits kept for recovery that no journal record names, said under the deletes. */
  unrecorded?: number;
  /** *New branch or tag* (GITAPP2c), made at the line's commit; absent, the tab offers none. */
  acts?: RefActs;
  /** Each session branch's session's title, by the branch's name. */
  titles?: Readonly<Record<string, string>>;
  /** Open a branch's page, by its name. */
  onOpen?: (branch: string) => void;
  /** Open the repository's History, from its line's row. */
  onOpenHistory?: () => void;
}) {
  const { t } = useTranslation();
  if (read.state === 'reading') return <Waiting reading sentence={t('projects.git.reading.branches', { repository })} />;
  if (read.state === 'refused') return <Waiting reading={false} sentence={read.sentence} />;
  if (read.state === 'noCheckout') {
    return <Nothing headline={t('projects.git.noCheckout.headline')} body={t('projects.git.noCheckout.branches', { repository })} />;
  }

  if (read.value.branches.length === 0) {
    return <Nothing headline={t('projects.git.branches.none.headline')} body={t('projects.git.branches.none.body', { repository })} />;
  }
  return (
    <div className="grid gap-4">
      <BranchList repository={repository} list={read.value} titles={titles} onOpen={onOpen} onOpenHistory={onOpenHistory} acts={acts} />
      {acts && deleted && <DeletedRefs rows={deleted} unrecorded={unrecorded} acts={acts} />}
    </div>
  );
}

/**
 * **A repository's History tab** (UX6h1, D150 §4.2, the built-in Git design §2.3): its line's graph, the commits `GIT_GRAPH`
 * walks from the line's commit, newest first, each beside the lanes laid from its parents. It says while they are on their
 * way, that this machine has no checkout, why Git did not answer, and that its line has no commit here yet.
 *
 * @remarks
 * **A molecule**: the read arrives as a prop, and its holder reads it only while the tab shows. It draws the first page;
 * the pages accumulated, with the load-more foot while the graph answers `more` (UX6h3a); a commit opened beside the graph and
 * *All branches* are UX6h3's.
 */
export function HistoryTab({ repository, read, onMore, all }: {
  repository: string;
  read: HistoryRead;
  /** The next page, asked: the holder reads it and hands the commits back accumulated. */
  onMore?: () => void;
  /**
   * *All branches* (UX6h3c): the ⋯'s one checked item, page state its holder keeps. `limit` says the graph drew only the
   * first `shown` of `total` branches, the read's own limit (D147). Absent, History has no ⋯.
   */
  all?: { on: boolean; onToggle: () => void; limit?: { shown: number; total: number } };
}) {
  const { t } = useTranslation();
  const body = useRef<HTMLDivElement>(null);
  const rowAt = useCallback((index: number) => body.current?.querySelectorAll<HTMLElement>('[data-commit]')[index] ?? null, []);
  const loaded = read.state === 'read' ? read.value : null;
  const paging = useGraphPaging({ shown: loaded?.commits.length ?? 0, loadingMore: Boolean(loaded?.loadingMore), onMore: onMore ?? noop, rowAt });
  // The ⋯ stays while the graph is on its way or refused with it ticked, so it can always be unticked.
  const options = all && (
    <div className="flex">
      <ListMore
        label={t('projects.git.history.all.more')}
        items={[{ id: 'all', label: t('projects.git.history.all.show'), checked: all.on }]}
        onChoose={all.onToggle}
      />
    </div>
  );
  if (read.state === 'reading') {
    return (
      <div className="grid gap-2">
        {all?.on && options}
        <Waiting reading sentence={t('projects.git.reading.history', { repository })} />
      </div>
    );
  }
  if (read.state === 'refused') {
    return (
      <div className="grid gap-2">
        {all?.on && options}
        <Waiting reading={false} sentence={read.sentence} />
      </div>
    );
  }
  if (read.state === 'noCheckout') {
    return <Nothing headline={t('projects.git.noCheckout.headline')} body={t('projects.git.noCheckout.history', { repository })} />;
  }
  if (read.state === 'noLine') {
    return (
      <Nothing
        headline={t('projects.git.history.none.headline')}
        body={read.line
          ? t('projects.git.history.none.line', { repository, branch: read.line })
          : t('projects.git.history.none.unnamed', { repository })}
      />
    );
  }

  const { line, tips, commits, more, loadingMore, moreError } = read.value;
  return (
    <div ref={body} className="grid gap-2">
      <div className="flex items-start justify-between gap-2">
        <Prose className="text-small text-ink-soft">
          <Inline text={all?.on ? t('projects.git.history.all.head', { repository, branch: line }) : t('projects.git.history.head', { repository, branch: line })} />
          {all?.on && all.limit && <> <Inline text={t('projects.git.history.all.limit', all.limit)} /></>}
        </Prose>
        {options}
      </div>
      <CommitGraph
        label={all?.on ? t('projects.git.history.all.list', { repository }) : t('projects.git.history.list', { repository, branch: line })}
        commits={commits}
        tips={tips}
      />
      {more && onMore && <GraphFoot shown={commits.length} loadingMore={Boolean(loadingMore)} error={moreError} paging={paging} />}
    </div>
  );
}

/** One file as a diff names it: the bridge's `ChangeEntry`, handed as it came (the molecule imports nothing of the bridge). */
export type ChangedFile = { path: string; status: string; oldMode: string; newMode: string; oldBlob: string; newBlob: string };

/** What the Changes tab shows: the three observations of the checkout and when they were made. */
export type ChangesValue = {
  staged: readonly ChangedFile[];
  unstaged: readonly ChangedFile[];
  untracked: readonly string[];
  unmerged: readonly { path: string; code: string; modes: readonly string[]; blobs: readonly string[] }[];
  readAt: string;
};

const NONE = /^0+$/;
const short = (blob: string) => blob.slice(0, 7);

/** The side of an entry that exists: the worktree's blob is never hashed, so an unstaged entry names the staged one. */
const mode = (file: ChangedFile) => (NONE.test(file.newMode) ? file.oldMode : file.newMode);
const blobOf = (file: ChangedFile) => (NONE.test(file.newBlob) ? file.oldBlob : file.newBlob);

/** The side of a file's hunks: the index → worktree for a file to stage, HEAD → index for one to unstage. */
export type ChangeSide = 'unstaged' | 'staged';

/** The last act's answer, said under the tab's head with *Undo* where it is done and kept a way back. */
export type ChangesLast = {
  verb: 'stage' | 'unstage' | 'undo' | 'undoResolve' | 'commit' | 'amend' | 'resolve' | 'mark' | 'switch' | 'stash' | 'stashApply' | 'stashDrop';
  path: string | null;
  act: string | null;
  done: boolean;
  said: string | null;
  stale?: boolean;
  /** A commit's own words beside the record's sentence: what a hook changed in it, or why git refused it. */
  notes?: string | null;
};

/**
 * The presses of the Changes tab (GITAPP2h): stage and unstage a file whole or by hunk and line, and *Undo*. Absent, the tab only reads
 * (the switch is off, or the shell is not there).
 */
export type ChangesActs = {
  /** An act is running: every press waits. */
  busy: boolean;
  last: ChangesLast | null;
  /** A file pressed whole (no pick), or the hunks it picked with the faces they were read against. `shown` is the index entry the list showed. */
  press: (press: { verb: 'stage' | 'unstage'; path: string; shown: string; picks?: HunkPress['picks']; faces?: HunkPress['faces'] }) => void;
  undo: (act: string) => void;
  /** A file's hunks on a side, read by its holder. */
  Hunks: ComponentType<{ path: string; side: ChangeSide; busy: boolean; onPress: (press: HunkPress) => void }>;
  /** The commit box (GITAPP2i), read and pressed by its holder; absent, the tab draws none. */
  Commit?: ComponentType<{ busy: boolean }>;
  /** A conflicted file's conflicts (GITAPP2j), read by its holder; with `resolve` and `mark`, the row draws *Resolve* and *Mark resolved*. */
  Conflict?: ComponentType<{ path: string; busy: boolean; onPress: (press: ConflictPress) => void }>;
  /** The choices made for a file's conflicts, with the faces they were read against. */
  resolve?: (press: { path: string } & ConflictPress) => void;
  /** *Mark resolved*: the file added, its stages gone from the index. */
  mark?: (path: string) => void;
  /** The switch box (GITAPP2k) and the stash box, each read and pressed by its holder; absent, the tab draws none. */
  Switch?: ComponentType<{ busy: boolean }>;
  Stash?: ComponentType<{ busy: boolean }>;
};

/**
 * **A repository's Changes tab** (GITAPP2b, D204, the git-writes design §1 and §4): where the person lets Daoris change the
 * checkout's files, what is staged against the last commit, what the files hold beyond it, what is untracked and what a merge
 * left in conflict. It is read when the tab opens, on focus and on *Refresh*, never watched, and says when. With `acts` (GITAPP2h)
 * a file can be staged or unstaged whole, or by the hunks and lines of its changes, and the last act can be undone.
 *
 * @remarks
 * **A molecule**: the read arrives as a prop and `refreshing` says it is being read again while the last stays on show, with its
 * own time. A staged addition whose file was removed since is in Staged and in Not staged: two observations, both true.
 */
export function ChangesTab({ repository, read, refreshing = false, onRefresh, acts }: {
  repository: string;
  read: GitRead<ChangesValue>;
  /** The holder is reading it again. */
  refreshing?: boolean;
  /** *Refresh*: read again now. Absent, the tab draws no press. */
  onRefresh?: () => void;
  acts?: ChangesActs;
}) {
  const { t, i18n } = useTranslation();
  if (read.state === 'reading') return <Waiting reading sentence={t('projects.git.reading.changes', { repository })} />;
  if (read.state === 'refused') return <Waiting reading={false} sentence={read.sentence} />;
  if (read.state === 'noCheckout') {
    return <Nothing headline={t('projects.git.noCheckout.headline')} body={t('projects.git.noCheckout.changes', { repository })} />;
  }

  const { staged, unstaged, untracked, unmerged, readAt } = read.value;
  const none = staged.length + unstaged.length + untracked.length + unmerged.length === 0;
  return (
    <section aria-label={t('projects.git.changes.list', { repository })} aria-busy={refreshing || undefined} className="grid min-w-0 gap-4">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <Prose className="min-w-0 text-small text-ink-soft">
          <Inline text={t('projects.git.changes.readAt', { time: clockOf(readAt, undefined, i18n.language) })} />
          {refreshing && <> <span>{t('projects.git.changes.readingAgain')}</span></>}
        </Prose>
        {onRefresh && <Button variant="ghost" disabled={refreshing} onClick={onRefresh}>{t('projects.git.changes.refresh')}</Button>}
      </div>
      {acts?.last && <ActSaid last={acts.last} busy={acts.busy} onUndo={acts.undo} />}
      {acts?.Commit && <acts.Commit busy={acts.busy} />}
      {acts?.Switch && <acts.Switch busy={acts.busy} />}
      {acts?.Stash && <acts.Stash busy={acts.busy} />}
      {none && <Nothing headline={t('projects.git.changes.none.headline')} body={t('projects.git.changes.none.body', { repository })} />}
      {staged.length > 0 && (
        <ChangeKind kind="staged" count={staged.length}>
          {staged.map((file) => <ChangeRow key={file.path} file={file} side="staged" acts={acts} />)}
        </ChangeKind>
      )}
      {unstaged.length > 0 && (
        <ChangeKind kind="unstaged" count={unstaged.length}>
          {unstaged.map((file) => <ChangeRow key={file.path} file={file} side="unstaged" acts={acts} />)}
        </ChangeKind>
      )}
      {untracked.length > 0 && (
        <ChangeKind kind="untracked" count={untracked.length}>
          {untracked.map((path) => (
            <li key={path} className="flex min-w-0 flex-wrap items-center justify-between gap-2 border-b border-line py-1.5 last:border-b-0">
              <Path path={path} />
              {acts && <Button variant="ghost" disabled={acts.busy} onClick={() => acts.press({ verb: 'stage', path, shown: '' })}>{t('projects.git.changes.stage')}</Button>}
            </li>
          ))}
        </ChangeKind>
      )}
      {unmerged.length > 0 && (
        <ChangeKind kind="unmerged" count={unmerged.length}>
          {unmerged.map((entry) => <ConflictRow key={entry.path} entry={entry} acts={acts} />)}
        </ChangeKind>
      )}
    </section>
  );
}

function ChangeKind({ kind, count, children }: { kind: 'staged' | 'unstaged' | 'untracked' | 'unmerged'; count: number; children: ReactNode }) {
  const { t } = useTranslation();
  const named = useId();
  return (
    <div className="grid gap-1">
      <h3 id={named} className="m-0 text-small font-semibold text-ink-faint">{t(`projects.git.changes.kind.${kind}`, { count })}</h3>
      <Prose className="text-small text-ink-soft"><Inline text={t(`projects.git.changes.note.${kind}`)} /></Prose>
      <ul aria-labelledby={named} className="m-0 grid list-none p-0">{children}</ul>
    </div>
  );
}

/** The index entry a listed file stands for, as the driver names it: `mode blob`, or empty where the index holds none. */
const indexOf = (file: ChangedFile, side: ChangeSide) => {
  const [m, b] = side === 'unstaged' ? [file.oldMode, file.oldBlob] : [file.newMode, file.newBlob];
  return NONE.test(b) ? '' : `${m} ${b}`;
};

function ChangeRow({ file, side, acts }: { file: ChangedFile; side: ChangeSide; acts?: ChangesActs }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const verb = side === 'unstaged' ? 'stage' : 'unstage';
  return (
    <li className="grid min-w-0 gap-1 border-b border-line py-1.5 last:border-b-0">
      <div className="flex min-w-0 flex-wrap items-start justify-between gap-2">
        <div className="grid min-w-0 gap-0.5">
          <Path path={file.path} status={file.status} />
          <Detail parts={[t('projects.git.changes.mode', { mode: mode(file) }), t('projects.git.changes.blob', { id: short(blobOf(file)) })]} />
        </div>
        {acts && (
          <div className="flex flex-wrap gap-1">
            <Button variant="ghost" disabled={acts.busy} onClick={() => acts.press({ verb, path: file.path, shown: indexOf(file, side) })}>{t(`projects.git.changes.${verb}`)}</Button>
            <Button variant="ghost" aria-expanded={open} onClick={() => setOpen(!open)}>{t(open ? 'projects.git.changes.hide' : 'projects.git.changes.show')}</Button>
          </div>
        )}
      </div>
      {acts && open && (
        <acts.Hunks
          path={file.path}
          side={side}
          busy={acts.busy}
          onPress={(press) => acts.press({ verb, path: file.path, shown: indexOf(file, side), ...press })}
        />
      )}
    </li>
  );
}

/** A file the index holds in conflict: its stages, and where the tab can resolve it, *Resolve* beside *Mark resolved*. */
function ConflictRow({ entry, acts }: { entry: ChangesValue['unmerged'][number]; acts?: ChangesActs }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const { Conflict, resolve, mark } = acts ?? {};
  return (
    <li className="grid min-w-0 gap-1 border-b border-line py-1.5 last:border-b-0">
      <div className="flex min-w-0 flex-wrap items-start justify-between gap-2">
        <div className="grid min-w-0 gap-0.5">
          <Path path={entry.path} code={entry.code} />
          <Detail parts={[t('projects.git.changes.mode', { mode: entry.modes.join(' ') }), t('projects.git.changes.stages', { ids: entry.blobs.map(short).join(', ') })]} />
        </div>
        {acts && Conflict && resolve && mark && (
          <div className="flex flex-wrap gap-1">
            <Button variant="ghost" aria-expanded={open} onClick={() => setOpen(!open)}>{t(open ? 'projects.git.changes.conflict.hide' : 'projects.git.changes.conflict.show')}</Button>
            <Button variant="ghost" disabled={acts.busy} onClick={() => mark(entry.path)}>{t('projects.git.changes.conflict.mark')}</Button>
          </div>
        )}
      </div>
      {acts && Conflict && resolve && open && (
        <Conflict path={entry.path} busy={acts.busy} onPress={(press) => resolve({ path: entry.path, ...press })} />
      )}
    </li>
  );
}

/** The last act in words, a refusal in the driver's sentence, and *Undo* beside a done one that kept a way back. */
function ActSaid({ last, busy, onUndo }: { last: ChangesLast; busy: boolean; onUndo: (act: string) => void }) {
  const { t } = useTranslation();
  const words = last.done
    ? [t(`projects.git.changes.acted.${last.verb}`, { path: last.path ?? '' }), last.notes].filter(Boolean).join(' ')
    : last.stale ? t('projects.git.changes.acted.stale') : last.notes ?? last.said ?? t('projects.git.changes.acted.unknown');
  return (
    <div role="status" className="flex flex-wrap items-center justify-between gap-2 rounded-control border border-line p-2">
      <Prose className="min-w-0 text-small"><Inline text={words} /></Prose>
      {last.done && last.act && (last.verb === 'stage' || last.verb === 'unstage' || last.verb === 'resolve' || last.verb === 'mark') && (
        <Button variant="ghost" disabled={busy} onClick={() => onUndo(last.act!)}>{t('projects.git.changes.undo')}</Button>
      )}
    </div>
  );
}

/** A path, whole, wrapping anywhere rather than widening the tab, beside its status. */
function Path({ path, status, code }: { path: string; status?: string; code?: string }) {
  const { t } = useTranslation();
  return (
    <div className="flex min-w-0 items-baseline gap-2">
      {status && <Pill tone={status === 'D' ? 'declined' : status === 'A' ? 'done' : 'neutral'}>{t(`projects.git.changes.status.${status}`, { defaultValue: status })}</Pill>}
      {code && <Pill tone="declined">{code}</Pill>}
      <code className="min-w-0 font-mono text-small text-ink [overflow-wrap:anywhere]">{path}</code>
    </div>
  );
}

function Detail({ parts }: { parts: string[] }) {
  return <p className="m-0 flex flex-wrap gap-x-2 text-meta text-ink-faint">{parts.map((part) => <span key={part}>{part}</span>)}</p>;
}
