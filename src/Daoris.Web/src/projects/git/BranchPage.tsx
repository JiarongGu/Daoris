import { type ReactNode, useCallback, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { ExternalLink } from '../../design/links';
import { Button, Icon, Inline, Pill, Prose, SESSION_TONE, type ShownState } from '../../design/ui';
import { PageHead, ViewMain } from '../../work/frame/ViewMain';
import { type ListedBranch, When } from './BranchList';
import { kindOf, landedStanding, pullRequestSaid, standing } from './branchStanding';
import { CommitGraph, type GraphCommitRow } from './CommitGraph';
import { GraphFoot, useGraphPaging } from './GraphFoot';
import { LandingFlow, type LandingDoor } from './LandingFlow';
import { type GitRead, Nothing, Waiting } from './RepositoryGit';

const noop = () => {};

/** A branch as its page reads it: as the list holds it, its line's name, and its session's title where its holder joined one. */
export type BranchFacts = {
  branch: ListedBranch;
  /** The line it is drawn beside, by the name the list heads with; null where nothing names one. */
  line: string | null;
  title?: string | null;
};

/**
 * Its graph since it parted from its line (D147's `sinceParted`), the pages accumulated: the tips in the order drawn, the line's
 * first so it keeps the leftmost lane as History's does; or that its line has no commit here to be drawn beside.
 */
export type BranchGraph =
  | GitRead<{ tips: string[]; commits: GraphCommitRow[]; more: boolean; loadingMore?: boolean; moreError?: string | null }>
  | { state: 'noLine' };

/**
 * What the page reads: the list on its way, with no checkout here, or refused in words; the branch gone from the list since it
 * was opened; or the branch as listed, with its graph.
 */
export type BranchRead =
  | { state: 'reading' }
  | { state: 'noCheckout' }
  | { state: 'refused'; sentence: string }
  | { state: 'gone' }
  | { state: 'read'; value: BranchFacts; graph: BranchGraph };

/**
 * **A branch's page** (UX6h3b, the built-in Git design §2.3), in Repositories' main area, opened from a branch's name in a
 * repository's Branches or a workspace's. Its header names the branch and its kind, a session's with its state and title as its
 * list row says them, and says in one line where it stands: against its line, against origin and its pull request, in the
 * list's own words (`branchStanding`). Its act goes back to the repository's Branches. Beneath, the branch's commits since it
 * left its line beside the line's since then (`sinceParted`), in lanes, a page at a time with the load-more foot (UX6h3a).
 *
 * @remarks
 * **A molecule**: the read arrives as a prop, and its holder (`RepositoryBranch`) reads the list and the graph. The header and
 * its way back are there in every state, so a branch the list no longer holds says so beside the press that leaves. Commit
 * rows keep no press (GIT1f's) and the branch carries no act of git's (GIT1g's); the walk's sides are not marked.
 */
export function BranchPage({ repository, name, read, onBack, onMore, landing }: {
  repository: string;
  /** The branch's name, as the press that opened it named it. */
  name: string;
  read: BranchRead;
  /** Back to the repository's Branches. */
  onBack: () => void;
  /** The next page, asked: the holder reads it and hands the commits back accumulated. */
  onMore?: () => void;
  /**
   * How a landed branch landed (FLOWCHART1c): its fold and its reads, which the holder keeps. Absent for any other kind of branch,
   * and where nothing can read the trace (a browser).
   */
  landing?: LandingDoor;
}) {
  const { t } = useTranslation();
  const body = useRef<HTMLDivElement>(null);
  const rowAt = useCallback((index: number) => body.current?.querySelectorAll<HTMLElement>('[data-commit]')[index] ?? null, []);
  const facts = read.state === 'read' ? read.value : null;
  const graph = read.state === 'read' && read.graph.state === 'read' ? read.graph.value : null;
  const paging = useGraphPaging({ shown: graph?.commits.length ?? 0, loadingMore: Boolean(graph?.loadingMore), onMore: onMore ?? noop, rowAt });

  const head = (
    <PageHead
      title={name}
      pills={facts && <Marks branch={facts.branch} />}
      line={facts?.title ?? undefined}
      fact={facts ? standingLine(t, facts) : null}
      acts={<Button variant="ghost" onClick={onBack}><Icon name="back" size={14} />{t('projects.git.branch.back')}</Button>}
    />
  );

  const reading = <Waiting reading sentence={t('projects.git.branch.reading', { branch: name })} />;
  const shown = (() => {
    if (read.state === 'reading') return reading;
    if (read.state === 'refused') return <Waiting reading={false} sentence={read.sentence} />;
    if (read.state === 'noCheckout') {
      return <Nothing headline={t('projects.git.noCheckout.headline')} body={t('projects.git.noCheckout.branches', { repository })} />;
    }
    if (read.state === 'gone') {
      return <Nothing headline={t('projects.git.branch.gone.headline')} body={t('projects.git.branch.gone.body', { repository, branch: name })} />;
    }
    const line = read.value.line ?? '';
    const walked = read.graph;
    if (walked.state === 'reading') return reading;
    if (walked.state === 'refused') return <Waiting reading={false} sentence={walked.sentence} />;
    if (walked.state === 'noCheckout') {
      return <Nothing headline={t('projects.git.noCheckout.headline')} body={t('projects.git.noCheckout.history', { repository })} />;
    }
    if (walked.state === 'noLine') {
      return (
        <Nothing
          headline={t('projects.git.history.none.headline')}
          body={read.value.line
            ? t('projects.git.history.none.line', { repository, branch: read.value.line })
            : t('projects.git.history.none.unnamed', { repository })}
        />
      );
    }
    const { tips, commits, more, loadingMore, moreError } = walked.value;
    if (commits.length === 0) {
      return <Prose className="text-small text-ink-soft"><Inline text={t('projects.git.branch.same', { branch: name, line })} /></Prose>;
    }
    return (
      <div ref={body} className="grid gap-2">
        <Prose className="text-small text-ink-soft"><Inline text={t('projects.git.branch.head', { branch: name, line })} /></Prose>
        <CommitGraph label={t('projects.git.branch.list', { branch: name, line })} commits={commits} tips={tips} />
        {more && onMore && <GraphFoot shown={commits.length} loadingMore={Boolean(loadingMore)} error={moreError} paging={paging} />}
      </div>
    );
  })();

  // How a landed branch landed stands above its commits, once the list has read it (FLOWCHART1c).
  return (
    <ViewMain header={head}>
      {landing && read.state === 'read'
        ? <div className="grid min-w-0 gap-4"><LandingFlow name={name} door={landing} />{shown}</div>
        : shown}
    </ViewMain>
  );
}

/** Its kind, as a word, and a session's state on the pill the rail gives it. */
function Marks({ branch }: { branch: ListedBranch }) {
  const { t } = useTranslation();
  const kind = kindOf(branch);
  const state = branch.session?.state;
  return (
    <>
      {/* The line's own page is History, so only a list read since says a branch is the line. */}
      <Pill tone="neutral">{kind === 'line' ? t('projects.line') : t(`projects.git.branch.kind.${kind}`)}</Pill>
      {state && <Pill tone={SESSION_TONE[state as ShownState] ?? 'neutral'}>{t(`sessionState.${state}`, { defaultValue: state })}</Pill>}
    </>
  );
}

/**
 * Where it stands, on one wrapping line: against its line, then a landed branch against its own name on origin, its pull
 * request's link as the landing kept it, and what its plugin last answered with when.
 */
function standingLine(t: TFunction, facts: BranchFacts): ReactNode {
  const { branch, line } = facts;
  const landed = branch.landed;
  const sentences = [
    line ? standing(t, line, branch.ahead, branch.behind) : null,
    landed ? landedStanding(t, branch.name, landed) : null,
  ].filter((sentence): sentence is string => Boolean(sentence));
  const said = landed ? pullRequestSaid(t, landed) : [];
  const link = landed?.pushed && landed.pullRequest;
  if (sentences.length === 0 && !link && said.length === 0) return null;
  return (
    <span className="inline-flex flex-wrap items-baseline gap-x-2 gap-y-0.5 text-ink-soft">
      {sentences.map((sentence) => <span key={sentence}><Inline text={sentence} /></span>)}
      {link && <ExternalLink href={link} className="text-accent underline underline-offset-2">{t('work.review.pullRequest')}</ExternalLink>}
      {said.map(({ sentence, at }) => (
        <span key={sentence}>
          <Inline text={sentence} />
          {at && <>{' '}<When at={at} /></>}
        </span>
      ))}
    </span>
  );
}
