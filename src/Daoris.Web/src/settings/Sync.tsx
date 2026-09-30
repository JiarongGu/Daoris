import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Chip, Inline, Prose } from '../ui';
import { sweepKey, type LandedBranch } from './Sweep';

/** One row: what the press does to it, its name, and the sentence — the session branches card's own grid. */
function Row({ name, moving, word, children }: { name: string; moving: boolean; word: string; children: ReactNode }) {
  return (
    <li aria-label={name} className="grid grid-cols-[4.5rem_minmax(0,16rem)_minmax(0,1fr)] items-baseline gap-x-3 py-1">
      <span><Chip accent={moving}>{word}</Chip></span>
      <span className="truncate font-mono text-small text-ink">{name}</span>
      <span className="min-w-0 text-small text-ink-soft">{children}</span>
    </li>
  );
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

/** The driver's TREES_SYNC_PLAN answer: each line, each branch, and the landed branches whose work reached the line. */
export type SyncPlan = { lines: LinePull[]; rebases: RebaseBranch[]; deletes: LandedBranch[] };

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
 */
export function SyncSection({ plan, busy, onLook, onSync }: {
  /** Undefined until the person looks. */
  plan?: SyncPlan;
  busy?: boolean;
  onLook: () => void;
  onSync: (only: string[]) => void;
}) {
  const { t } = useTranslation();
  const acting = plan ? syncKeys(plan) : [];
  const repositories = plan
    ? [...new Set([...plan.lines, ...plan.rebases, ...plan.deletes].map((row) => row.repository))].sort((a, b) => a.localeCompare(b))
    : [];

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

      {plan && acting.length === 0 && <Prose className="mt-2">{t('settings.sync.nothing')}</Prose>}

      {plan && repositories.map((repository) => (
        <section key={repository} aria-label={repository} className="mt-2">
          <div className="text-small font-medium text-ink-soft">{repository}</div>
          <ul className="m-0 mt-1 list-none p-0">
            {plan.lines.filter((pull) => pull.repository === repository).map((pull) => (
              <Row key={`line:${pull.line ?? ''}`} name={pull.line ?? t('settings.sync.line.none')} moving={pull.moves}
                word={t(pull.moves ? 'settings.sync.moves' : 'settings.sync.stays')}>
                <Inline text={pulls(pull)} />
                {pull.fetch && (
                  <span className="mt-0.5 block text-meta text-ink-faint"><Inline text={t('settings.sync.line.notFetched', { why: pull.fetch })} /></span>
                )}
                {pull.kind === 'unknown' && pull.detail && (
                  <span className="mt-0.5 block break-words font-mono text-meta text-ink-faint">{pull.detail}</span>
                )}
              </Row>
            ))}
            {plan.rebases.filter((branch) => branch.repository === repository).map((branch) => (
              <Row key={`branch:${branch.branch}`} name={branch.branch} moving={branch.replays}
                word={t(branch.replays ? 'settings.sync.moves' : 'settings.sync.stays')}>
                {branch.landed && <Chip>{t('settings.sync.branch.landed')}</Chip>} <Inline text={replays(branch)} />
                {branch.kind === 'unknown' && branch.detail && (
                  <span className="mt-0.5 block break-words font-mono text-meta text-ink-faint">{branch.detail}</span>
                )}
              </Row>
            ))}
            {plan.deletes.filter((branch) => branch.repository === repository).map((branch) => (
              <Row key={`delete:${branch.branch}`} name={branch.branch} moving={branch.removable}
                word={t(branch.removable ? 'settings.sweep.goes' : 'settings.sweep.kept')}>
                <Inline text={goes(branch)} />
              </Row>
            ))}
          </ul>
        </section>
      ))}

      <div className="mt-3 flex flex-wrap items-center gap-2">
        {plan && (
          <Button variant="primary" disabled={busy || acting.length === 0} onClick={() => onSync(acting)}>
            {t('settings.sync.apply', { count: acting.length })}
          </Button>
        )}
        <Button variant={plan ? 'ghost' : 'primary'} disabled={busy} onClick={onLook}>
          {t(plan ? 'settings.sync.lookAgain' : 'settings.sync.look')}
        </Button>
      </div>
    </section>
  );
}
