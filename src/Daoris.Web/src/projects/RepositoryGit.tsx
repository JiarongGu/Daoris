import { useTranslation } from 'react-i18next';
import type { GitLogCommit, GitRepositoryBranches } from '../bridge/git';
import { ago } from '../format';
import { Inline, Prose, SkeletonRows } from '../ui';
import { CommitGraph } from './CommitGraph';

/**
 * What a Git tab shows (UX6h1): its answer on its way, no checkout of the repository on this machine, why Git did not answer
 * in words the page says (`errors.{code}`, or the driver's sentence), or what it read.
 */
export type GitRead<T> =
  | { state: 'reading' }
  | { state: 'noCheckout' }
  | { state: 'refused'; sentence: string }
  | { state: 'read'; value: T };

/** What History reads: its line's commits from the tips walked, or that its line holds no commit to walk from here. */
export type HistoryRead =
  | GitRead<{ line: string; tips: string[]; commits: GitLogCommit[] }>
  | { state: 'noLine'; line: string | null };

/**
 * What a Git tab has nothing to show for: its fact as a headline and why beneath it. A block of the page, so it wraps at the
 * pane's edge as every block of a record page does (D141), where the main area's own empty state keeps a narrow body.
 */
function Nothing({ headline, body }: { headline: string; body: string }) {
  return (
    <div className="grid gap-1">
      <p className="m-0 text-body font-semibold text-ink">{headline}</p>
      <Prose className="text-small text-ink-soft"><Inline text={body} /></Prose>
    </div>
  );
}

/** A Git tab's answer on its way, said and drawn as rows, or why it did not come: never a blank tab. */
function Waiting({ sentence, reading }: { sentence: string; reading: boolean }) {
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
 * **A molecule**: the read arrives as a prop, and its holder reads it only while the tab shows. The list by kind (the
 * line, sessions', landed, yours, origin's: the built-in Git design §2.2) is UX6h2's; here each branch is a row of its
 * name and its last commit, in the order the list answered.
 */
export function BranchesTab({ repository, read }: { repository: string; read: GitRead<GitRepositoryBranches> }) {
  const { t } = useTranslation();
  if (read.state === 'reading') return <Waiting reading sentence={t('projects.git.reading.branches', { repository })} />;
  if (read.state === 'refused') return <Waiting reading={false} sentence={read.sentence} />;
  if (read.state === 'noCheckout') {
    return <Nothing headline={t('projects.git.noCheckout.headline')} body={t('projects.git.noCheckout.branches', { repository })} />;
  }

  const { branches } = read.value;
  if (branches.length === 0) {
    return <Nothing headline={t('projects.git.branches.none.headline')} body={t('projects.git.branches.none.body', { repository })} />;
  }
  return (
    <ul aria-label={t('projects.git.branches.list', { repository })} className="m-0 grid list-none p-0">
      {branches.map((branch) => (
        <li key={branch.name} className="flex min-w-0 items-baseline gap-2 border-b border-line py-1.5 last:border-b-0">
          <code className="shrink-0 font-mono text-small text-ink">{branch.name}</code>
          <span className="min-w-0 flex-1 truncate text-small text-ink-soft">{branch.subject}</span>
          {branch.at && <span className="shrink-0 text-meta text-ink-faint tabular-nums">{ago(branch.at)}</span>}
        </li>
      ))}
    </ul>
  );
}

/**
 * **A repository's History tab** (UX6h1, D150 §4.2, the built-in Git design §2.3): its line's graph, the commits `GIT_GRAPH`
 * walks from the line's commit, newest first, each beside the lanes laid from its parents. It says while they are on their
 * way, that this machine has no checkout, why Git did not answer, and that its line has no commit here yet.
 *
 * @remarks
 * **A molecule**: the read arrives as a prop, and its holder reads it only while the tab shows. It draws the first page;
 * paging, a commit opened beside the graph and *All branches* are UX6h3's.
 */
export function HistoryTab({ repository, read }: { repository: string; read: HistoryRead }) {
  const { t } = useTranslation();
  if (read.state === 'reading') return <Waiting reading sentence={t('projects.git.reading.history', { repository })} />;
  if (read.state === 'refused') return <Waiting reading={false} sentence={read.sentence} />;
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

  const { line, tips, commits } = read.value;
  return (
    <div className="grid gap-2">
      <Prose className="text-small text-ink-soft"><Inline text={t('projects.git.history.head', { repository, branch: line })} /></Prose>
      <CommitGraph label={t('projects.git.history.list', { repository, branch: line })} commits={commits} tips={tips} />
    </div>
  );
}
