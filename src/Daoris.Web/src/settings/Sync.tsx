import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ago } from '../format';
import { cn } from '../lib/cn';
import { Button, CheckField, Chip, Icon, Inline, Prose } from '../ui';
import { BRANCH_ROWS, BranchRow, sweepKey, type LandedBranch } from './Sweep';

/** A press under way, on its own button: the refresh mark turning, and still under reduced motion. */
function Working() {
  return <Icon name="refresh" size={14} className="motion-safe:animate-spin" />;
}

/** What pulling a repository's line would do (WSR6). Only `fast-forward` moves anything. */
export type PullKind =
  | 'fast-forward' | 'up-to-date' | 'ahead' | 'diverged' | 'dirty' | 'checked-out' | 'no-remote' | 'no-local' | 'no-line' | 'unknown';

/** One repository's line, as the driver judged its pull after fetching it. */
export type LinePull = {
  repository: string;
  workspace: string;
  line?: string | null;
  kind: PullKind;
  /** The local line's commit and origin's, short. */
  from?: string | null;
  to?: string | null;
  /** Fast-forward: the commits it takes. Ahead: those origin lacks. Diverged: those the local line lacks. */
  commits: number;
  /** Why the fetch did not happen, in git's words. Content, never translated. */
  fetch?: string | null;
  /** Where it was not fetched: when this checkout last heard from origin, an ISO moment, or none — never (WSR7). */
  lastFetch?: string | null;
  /** Where it was not fetched: how origin is reached — `ssh`, `https`, `http`, `git` or `file` (WSR7). */
  reach?: string | null;
  /** Git's own words where it could not tell. Content. */
  detail?: string | null;
  moves: boolean;
};

/** What bringing one branch up to date would do (WSR6). Only `replay` moves anything. */
export type RebaseKind =
  | 'replay' | 'up-to-date' | 'in-use' | 'dirty' | 'checked-out' | 'pushed' | 'grew-from-unlanded' | 'unknown';

/** One session branch, or one branch a landing made, as the driver judged its replay onto the line. */
export type RebaseBranch = {
  repository: string;
  workspace: string;
  branch: string;
  /** A branch a landing made, rather than a session's own. */
  landed: boolean;
  kind: RebaseKind;
  onto?: string | null;
  /** The commit its own commits start after, short. */
  cut?: string | null;
  /** How that commit was found: it grew from the line, its record, or its content. */
  cutBy?: 'line' | 'record' | 'content' | null;
  /** The step before's branch it grew from, where its record knows one. */
  grewFrom?: string | null;
  commits: number;
  detail?: string | null;
  replays: boolean;
};

/** A repository with a checkout here, and whether it holds a branch of Daoris's (TREES_SYNC_SCOPE, D112). */
export type SyncRepository = { repository: string; workspace: string; holds: boolean };

/** What a look takes beside the repositories holding Daoris's branches (D112): every one, or those named. */
export type SyncInclude = 'all' | string[];

/**
 * The driver's TREES_SYNC_PLAN answer: each line, each branch, and the landed branches whose work reached the line;
 * then what the look took, and every other repository with a checkout, listed apart (D112). A host older than WSR7
 * answers neither of the last two.
 */
export type SyncPlan = {
  lines: LinePull[];
  rebases: RebaseBranch[];
  deletes: LandedBranch[];
  looked?: SyncRepository[];
  apart?: SyncRepository[];
};

/** Whether `include` takes `repository` beside the default. */
const includes = (include: SyncInclude | undefined, repository: string) =>
  include === 'all' || (include ?? []).includes(repository);

/** `was` and `more` together: every one stays every one. */
const together = (was: SyncInclude | undefined, more: SyncInclude): SyncInclude =>
  was === 'all' || more === 'all' ? 'all' : [...new Set([...(was ?? []), ...more])];

/**
 * What was not fetched, said once, before the rows (WSR7): how many, grouped by git's reason, when each last heard from
 * origin — which is what its row is judged against — and what the git Daoris runs needs to reach an origin over SSH or
 * HTTPS. Every fetch failed on the owner's workspace, and the look said so only at the end of each of 29 rows.
 *
 * @remarks
 * The person's own Git client may reach origin where this cannot: one carries its own git and ssh, and the driver runs
 * the git on the path. So the advice names what that git needs, and no product.
 */
function NotFetched({ lines }: { lines: LinePull[] }) {
  const { t } = useTranslation();
  const failed = lines.filter((pull) => pull.fetch);
  if (failed.length === 0) return null;
  const reasons = [...new Set(failed.map((pull) => pull.fetch!))];
  const when = (pull: LinePull) => pull.lastFetch
    ? t('settings.sync.notFetched.when', { repository: pull.repository, when: ago(pull.lastFetch) })
    : t('settings.sync.notFetched.never', { repository: pull.repository });

  return (
    <div role="note" aria-label={t('settings.sync.notFetched.label')} className="mt-2 rounded-control border border-line bg-raised px-3 py-2">
      <p className="m-0 text-small text-ink">{t('settings.sync.notFetched.head', { count: failed.length, total: lines.length })}</p>
      <ul className="m-0 mt-1 list-none p-0">
        {reasons.map((reason) => {
          const these = failed.filter((pull) => pull.fetch === reason);
          return (
            <li key={reason} className="mt-1 text-small">
              <span className="break-words text-ink-soft"><Inline text={reason} /></span>
              <span className="text-meta text-ink-faint"> · {these.length}</span>
              <span className="block text-ink-soft">{these.map(when).join(' · ')}</span>
            </li>
          );
        })}
      </ul>
      {failed.some((pull) => pull.reach === 'ssh') && (
        <Prose className="mt-1 text-small text-ink-soft"><Inline text={t('settings.sync.notFetched.ssh')} /></Prose>
      )}
      {failed.some((pull) => pull.reach === 'https' || pull.reach === 'http') && (
        <Prose className="mt-1 text-small text-ink-soft">{t('settings.sync.notFetched.https')}</Prose>
      )}
    </div>
  );
}

/**
 * The repositories with a checkout here that hold no branch of Daoris's (D112): listed apart, collapsed, and looked at
 * only once the person ticks them — each, or all — and asks.
 */
function ApartList({ apart, busy, onInclude }: {
  apart: SyncRepository[];
  busy?: boolean;
  onInclude: (include: SyncInclude) => void;
}) {
  const { t } = useTranslation();
  const [ticked, setTicked] = useState<string[]>([]);
  // The box for all of them asks for every one, a repository added later included; ticking each asks for those.
  const [all, setAll] = useState(false);
  const tick = (repository: string, on: boolean) => {
    setAll(false);
    setTicked((was) => (on ? [...was, repository] : was.filter((name) => name !== repository)));
  };

  return (
    <details className="mt-3">
      <summary className="cursor-pointer text-small text-ink-soft">{t('settings.sync.apart.summary', { count: apart.length })}</summary>
      <div role="group" aria-label={t('settings.sync.apart.label')} className="mt-2 pl-4">
        <Prose className="text-small text-ink-soft">{t('settings.sync.apart.body')}</Prose>
        <CheckField className="mt-2" checked={ticked.length === apart.length} label={t('settings.sync.apart.all', { count: apart.length })}
          onChange={(on) => { setAll(on); setTicked(on ? apart.map((each) => each.repository) : []); }} />
        <ul className="m-0 mt-1 grid list-none grid-cols-[repeat(auto-fill,minmax(12rem,1fr))] gap-x-3 p-0">
          {apart.map((each) => (
            <li key={each.repository} className="py-0.5">
              <CheckField className="font-mono text-small" checked={ticked.includes(each.repository)} label={each.repository}
                onChange={(on) => tick(each.repository, on)} />
            </li>
          ))}
        </ul>
        <Button className="mt-2" disabled={busy || ticked.length === 0}
          onClick={() => { onInclude(all ? 'all' : ticked); setTicked([]); setAll(false); }}>
          {t('settings.sync.apart.include', { count: ticked.length })}
        </Button>
      </div>
    </details>
  );
}

/** The keys a press names what it acts on by — the driver's `repository:branch`, the line's by its branch. */
export const syncKeys = (plan: SyncPlan) => [
  ...plan.lines.filter((pull) => pull.moves && pull.line).map((pull) => `${pull.repository}:${pull.line}`),
  ...plan.rebases.filter((branch) => branch.replays).map(sweepKey),
  ...plan.deletes.filter((branch) => branch.removable).map(sweepKey),
];

/**
 * Bring up to date (WSR6, D109): after a pull request merges, each repository's line is fetched from origin and
 * fast-forwarded, the branches still at work are replayed onto it — only their own commits, cut at the commit each
 * grew from, so a squash-merged parent's commits drop — and the branches landings made whose work reached it go.
 *
 * @remarks
 * **The list is asked for, never fetched on its own**: looking reaches the network, as the person, so it waits for
 * the press of *Look for updates*. **The press does only what the list showed**, and the driver judges each row again
 * right before it acts: a line only by a fast-forward, in a clean checkout on it or as a ref nothing has checked out;
 * a replay in a tree of Daoris's own, aborted on a conflict. Nothing is pushed, and a branch on its remote is left.
 * `daoris-driver trees sync` is the terminal's door (D50). A section of the session branches card, drawn above them.
 *
 * **It takes the repositories that hold Daoris's branches** (D112). Every other one with a checkout is listed apart,
 * collapsed, and looked at only once the person ticks it — or all of them — and asks; looking again keeps it.
 *
 * **A look takes a while, and says so** (WSR7): a sentence naming how many repositories it is fetching, and a button
 * that reads as busy, since a dimmed one read as nothing happening. A look or a press the page stopped waiting for is
 * said here, where it was asked, not only in a toast that goes.
 */
export function SyncSection({ plan, scope, included, looking, lookingAt, bringing, stopped, onLook, onSync }: {
  /** Undefined until the person looks. */
  plan?: SyncPlan;
  /** Every repository with a checkout here, and whether each holds a branch of Daoris's; undefined until known. */
  scope?: SyncRepository[];
  /** What the last look was asked to take beside the default. */
  included?: SyncInclude;
  /** A look is under way. */
  looking?: boolean;
  /** How many repositories it fetches, where known. */
  lookingAt?: number | null;
  /** A press is under way. */
  bringing?: boolean;
  /** The step the page stopped waiting for, before the driver answered. */
  stopped?: 'look' | 'press';
  onLook: (include: SyncInclude) => void;
  onSync: (only: string[]) => void;
}) {
  const { t } = useTranslation();
  const busy = looking || bringing;
  const acting = plan ? syncKeys(plan) : [];
  const repositories = plan
    ? [...new Set([...plan.lines, ...plan.rebases, ...plan.deletes].map((row) => row.repository))].sort((a, b) => a.localeCompare(b))
    : [];
  // What the look left apart; before one, what it would, from the machine's own reading.
  const apart = plan?.apart ?? (scope ?? []).filter((each) => !each.holds && !includes(included, each.repository));
  const holding = (scope ?? []).filter((each) => each.holds).length;
  const lookedAtNone = plan !== undefined && Array.isArray(plan.looked) && plan.looked.length === 0;

  const pulls = (pull: LinePull) => {
    const line = pull.line ?? '';
    switch (pull.kind) {
      case 'fast-forward': return t('settings.sync.line.kind.fastForward', { count: pull.commits, line });
      case 'up-to-date': return t('settings.sync.line.kind.upToDate', { line });
      case 'ahead': return t('settings.sync.line.kind.ahead', { count: pull.commits, line });
      case 'diverged': return t('settings.sync.line.kind.diverged', { line });
      case 'dirty': return t('settings.sync.line.kind.dirty');
      case 'checked-out': return t('settings.sync.line.kind.checkedOut');
      case 'no-remote': return t('settings.sync.line.kind.noRemote', { line });
      case 'no-local': return t('settings.sync.line.kind.noLocal', { line });
      case 'no-line': return t('settings.sync.line.kind.noLine');
      default: return t('settings.sync.line.kind.unknown');
    }
  };

  const replays = (branch: RebaseBranch) => {
    const onto = branch.onto ?? '';
    switch (branch.kind) {
      case 'replay': {
        if (branch.commits === 0) return t('settings.sync.branch.kind.replayNothing', { onto });
        const replay = t('settings.sync.branch.kind.replay', { count: branch.commits, onto });
        if (branch.cutBy === 'record') {
          return `${replay} ${branch.grewFrom
            ? t('settings.sync.branch.after.record', { from: branch.grewFrom })
            : t('settings.sync.branch.after.recordUnnamed')}`;
        }
        return branch.cutBy === 'content' ? `${replay} ${t('settings.sync.branch.after.content')}` : replay;
      }
      case 'up-to-date': return t('settings.sync.branch.kind.upToDate', { onto });
      case 'in-use': return t('settings.sync.branch.kind.inUse');
      case 'dirty': return t('settings.sync.branch.kind.dirty');
      case 'checked-out': return t('settings.sync.branch.kind.checkedOut');
      case 'pushed': return t('settings.sync.branch.kind.pushed');
      case 'grew-from-unlanded': return branch.grewFrom
        ? t('settings.sync.branch.kind.waits', { from: branch.grewFrom })
        : t('settings.sync.branch.kind.waitsUnnamed');
      default: return t('settings.sync.branch.kind.unknown');
    }
  };

  const goes = (branch: LandedBranch) => branch.kind === 'leaned-on'
    ? t('settings.sweep.landed.kind.leanedOn', { where: branch.where ?? '' })
    : branch.kind === 'merged'
      ? t('settings.sweep.landed.kind.merged', { where: branch.where ?? '' })
      : branch.kind === 'inside'
        ? t('settings.sweep.landed.kind.inside', { where: branch.where ?? '' })
        : t('settings.sweep.landed.kind.onLine', { where: branch.where ?? '' });

  return (
    <section aria-label={t('settings.sync.title')} className="mt-3 border-t border-line pt-3">
      <div className="text-body font-medium text-ink">{t('settings.sync.title')}</div>
      <Prose className="mt-1 text-small text-ink-soft">{t('settings.sync.body')}</Prose>
      {!plan && scope && (
        <Prose className="mt-1 text-small text-ink-soft">
          {holding === 0 ? t('settings.sync.scopeNone') : t('settings.sync.scope', { count: holding })}
        </Prose>
      )}

      {/* What is under way, where the person asked: a look fetches for minutes on a large workspace (WSR7). */}
      {looking && (
        <p role="status" className="mt-2 text-small text-ink">
          {lookingAt ? t('settings.sync.looking', { count: lookingAt }) : t('settings.sync.lookingSome')}
        </p>
      )}
      {bringing && <p role="status" className="mt-2 text-small text-ink">{t('settings.sync.bringing')}</p>}
      {stopped && !busy && (
        <p role="alert" className="mt-2 text-small text-st-declined">
          {t(stopped === 'look' ? 'settings.sync.stopped.look' : 'settings.sync.stopped.press')}
        </p>
      )}

      {plan && <NotFetched lines={plan.lines} />}
      {lookedAtNone && <Prose className="mt-2">{t('settings.sync.noneHeld')}</Prose>}
      {plan && !lookedAtNone && acting.length === 0 && <Prose className="mt-2">{t('settings.sync.nothing')}</Prose>}

      {/* A group, not a region, per repository: the session branches below draw a region of the same name. A look
          again holds the last answer, dimmed, until the new one comes (D41's loading rule). */}
      {plan && repositories.map((repository) => (
        <div key={repository} role="group" aria-label={repository} className={cn('mt-2', looking && 'opacity-60')}>
          <div className="text-small font-medium text-ink-soft">{repository}</div>
          {/* The session branches card's own rows (UXFIX4): a line's and a branch's name whole, stacked by the list's width. */}
          <ul className={cn(BRANCH_ROWS, 'mt-1')}>
            {plan.lines.filter((pull) => pull.repository === repository).map((pull) => (
              <BranchRow key={`line:${pull.line ?? ''}`} name={pull.line ?? t('settings.sync.line.none')} moving={pull.moves}
                word={t(pull.moves ? 'settings.sync.moves' : 'settings.sync.stays')}>
                <Inline text={pulls(pull)} />
                {/* A short mark: the reason is said once, above the rows (WSR7). */}
                {pull.fetch && <span className="mt-0.5 block text-meta text-ink-faint">{t('settings.sync.line.notFetched')}</span>}
                {pull.kind === 'unknown' && pull.detail && (
                  <span className="mt-0.5 block break-words font-mono text-meta text-ink-faint">{pull.detail}</span>
                )}
              </BranchRow>
            ))}
            {plan.rebases.filter((branch) => branch.repository === repository).map((branch) => (
              <BranchRow key={`branch:${branch.branch}`} name={branch.branch} moving={branch.replays}
                word={t(branch.replays ? 'settings.sync.moves' : 'settings.sync.stays')}>
                {branch.landed && <Chip>{t('settings.sync.branch.landed')}</Chip>} <Inline text={replays(branch)} />
                {branch.kind === 'unknown' && branch.detail && (
                  <span className="mt-0.5 block break-words font-mono text-meta text-ink-faint">{branch.detail}</span>
                )}
              </BranchRow>
            ))}
            {plan.deletes.filter((branch) => branch.repository === repository).map((branch) => (
              <BranchRow key={`delete:${branch.branch}`} name={branch.branch} moving={branch.removable}
                word={t(branch.removable ? 'settings.sweep.goes' : 'settings.sweep.kept')}>
                <Inline text={goes(branch)} />
              </BranchRow>
            ))}
          </ul>
        </div>
      ))}

      <div className="mt-3 flex flex-wrap items-center gap-2">
        {plan && (
          <Button variant="primary" disabled={busy || acting.length === 0} aria-busy={bringing || undefined} onClick={() => onSync(acting)}>
            {bringing && <Working />}
            {bringing ? t('settings.sync.bringingButton') : t('settings.sync.apply', { count: acting.length })}
          </Button>
        )}
        <Button variant={plan ? 'ghost' : 'primary'} disabled={busy} aria-busy={looking || undefined} onClick={() => onLook(included ?? [])}>
          {looking && <Working />}
          {looking ? t('settings.sync.lookingButton') : t(plan ? 'settings.sync.lookAgain' : 'settings.sync.look')}
        </Button>
      </div>

      {apart.length > 0 && (
        <ApartList apart={apart} busy={busy} onInclude={(more) => onLook(together(included, more))} />
      )}
    </section>
  );
}
