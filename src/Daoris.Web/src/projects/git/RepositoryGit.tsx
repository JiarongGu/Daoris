import { useCallback, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { Inline, Prose, SkeletonRows } from '../../design/ui';
import { BranchList, type BranchListValue } from './BranchList';
import { CommitGraph, type GraphCommitRow } from './CommitGraph';
import { ListMore } from '../../work/frame/ListPane';
import { GraphFoot, useGraphPaging } from './GraphFoot';

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
 * twin of `daoris-driver git branches --repository`. While the list is on its way it says so; a repository with no checkout
 * here, or one Git did not answer for, says why in its place; a checkout with no commit has no branch yet.
 *
 * @remarks
 * **A molecule**: the read arrives as a prop, and its holder reads it only while the tab shows. What it read is the list by
 * kind (UX6h2a, `BranchList`): the line at its head, then sessions', landed and yours, each session's branch named by the
 * title its holder joined. A branch's name opens its page, and the line's opens History, where its holder hands the presses
 * (UX6h3b).
 */
export function BranchesTab({ repository, read, titles, onOpen, onOpenHistory }: {
  repository: string;
  read: GitRead<BranchListValue>;
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
  return <BranchList repository={repository} list={read.value} titles={titles} onOpen={onOpen} onOpenHistory={onOpenHistory} />;
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
