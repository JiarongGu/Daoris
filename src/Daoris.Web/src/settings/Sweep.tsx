import { type ReactNode, useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { Button, Card, Chip, Inline, type Notify, PathText, Prose, SectionTitle } from '../ui';
import { type Answered, InlineConfirm } from '../work/InlineConfirm';

/**
 * What a session branch holds (D88), one of the driver's `SweepKind` constants (`Landing.cs`), which the modules send as they
 * are. Only `empty`, `landed` and `carried` go: `carried` is work a completed pull request carried, by its plugin's word
 * where git confirms it (PLUGHOOK1a, D148 point 4).
 */
export type SweepKind = 'empty' | 'landed' | 'carried' | 'unlanded' | 'dirty' | 'in-use';

/** One session branch on this machine, as the driver judged it — the clean-up's list. */
export type SweepBranch = {
  repository: string;
  workspace: string;
  branch: string;
  hasTree: boolean;
  kind: SweepKind;
  /** Unlanded and carried: how many commits only Daoris's branches hold. */
  commits: number;
  /**
   * Landed: the first branch of the person's that holds it, or what holds its work by content (SQUASHTIDY1). Empty: the line.
   * Carried: the landed branch whose completed pull request carried it.
   */
  where?: string | null;
  /** Git's own lines, where they say more. Content, never translated. */
  detail?: string | null;
  removable: boolean;
  /**
   * Whether its discard is offered beside it (LAND3b), by the driver's own rule: a failed or superseded attempt's commits,
   * which no clean-up takes. A shell older than LAND3b answers none, and nothing is offered.
   */
  discardable?: boolean;
  /**
   * The session its tree is, by id and how it ended (LAND4), where its tree is still here and a record of this machine's
   * names it. Absent on a shell older than LAND4.
   */
  session?: { id: string; state: string } | null;
  /**
   * Whether its session's landing is offered beside it (LAND4), by the driver's own rule: its tree still here, holding commits
   * no branch of the person's holds, whatever that session's ending; the line `trees clean` prints beside the same row.
   */
  landable?: boolean;
};

/** What discarding a session branch answered (LAND3b): done, or the driver's sentence for why it was kept. */
export type BranchDiscard = { repository: string; branch: string; done: boolean; message: string };

/**
 * A discard's answer said once (LAND3b): the branch gone in the reader's words, which name no tree's path; a branch kept
 * in the driver's own sentence, whole, since a refusal is the contract. Pressed in an ask, a kept branch is said inside it
 * and the ask closes only once the branch went (UXFIX2); with no ask, in a toast.
 */
export const sayDiscard = (notify: Notify, t: (key: string, options?: Record<string, unknown>) => string, answered?: Answered) =>
  (result: BranchDiscard) => {
    if (!result.done) {
      if (answered) answered.refused(result.message);
      else notify(result.message, 'error');
      return;
    }
    notify(t('settings.sweep.discarded', { branch: result.branch, repository: result.repository }));
    answered?.done();
  };

/**
 * A session branch's discard, asked once (LAND3b, D102's LAND3 note): it names the branch, its repository, its tree where
 * it still has one, and that its commits go with it for good, beside the move and *never mind* (platform language §4).
 * The commits are the ones the driver counted that no branch of the person's holds; the press is forced, as
 * `daoris-driver trees remove <branch> --repository <name> --force` is. The one inline confirmation (UXFIX2): open until
 * the discard lands, a branch the driver kept said inside it.
 */
export function DiscardBranchAsk({ branch, busy = false, onDiscard, onClose, className }: {
  branch: Pick<SweepBranch, 'repository' | 'branch' | 'commits' | 'hasTree'>;
  /** A discard on its way: the presses wait for it. */
  busy?: boolean;
  /** The second press, told how the discard ended. */
  onDiscard: (answered: Answered) => void;
  /** Put down, or the discard landed. */
  onClose: () => void;
  className?: string;
}) {
  const { t } = useTranslation();
  const says = t(branch.hasTree ? 'settings.sweep.discardSaysTree' : 'settings.sweep.discardSays', {
    branch: branch.branch, repository: branch.repository, count: branch.commits,
  });
  return (
    <InlineConfirm
      className={className}
      label={t('settings.sweep.discardTitle', { branch: branch.branch })}
      says={<Inline text={says} />}
      meanIt={t('settings.sweep.discardMeanIt')}
      busy={busy}
      onConfirm={onDiscard}
      onClose={onClose}
    />
  );
}

/** What a branch a landing made holds (WSR5). Only `on-line`, `merged` and `inside` go. */
export type LandedKind =
  | 'on-line' | 'merged' | 'inside' | 'differs' | 'checked-out' | 'ahead-of-remote' | 'leaned-on' | 'unknown';

/** One branch a landing made and recorded on this machine, as the driver judged it against the line. */
export type LandedBranch = {
  repository: string;
  workspace: string;
  branch: string;
  kind: LandedKind;
  /** On the line: the line's form it reads on. Inside: the branch it is inside. Leaned on: the session branch. */
  where?: string | null;
  /** Differs: the files it changed that read otherwise on the line. Content, never translated. */
  files: string[];
  /** Git's own words where it could not tell. Content. */
  detail?: string | null;
  pullRequest?: string | null;
  /** Ahead of its remote: the commits the remote lacks; otherwise those the line lacks. */
  commits: number;
  removable: boolean;
};

/** The key a press names a branch by — the driver's `repository:branch`. */
export const sweepKey = (branch: { repository: string; branch: string }) => `${branch.repository}:${branch.branch}`;

/**
 * A list of branch rows (UXFIX4): its own container, so its rows lay out by the list's width, which is the main area's less
 * the card's gutters, and never by the window's.
 */
export const BRANCH_ROWS = '@container/branches m-0 list-none p-0';

/**
 * **One branch's row** (UXFIX4): what the press does to it, its name, and the sentence. The session branches, the landed
 * group and bringing up to date all draw it, so the three lists cannot lay a row out three ways.
 *
 * @remarks
 * **Its columns follow its list's width** (`BRANCH_ROWS`). Three where the list holds 30rem: the mark, the name up to 16rem,
 * and the sentence never under 13rem, so neither is a word a line at a 680 px window. Narrower, the name stays beside its
 * mark and the sentence goes under the name. At the main area's 400 px floor the three columns stayed, and the sentence was
 * left whatever width the name did not take.
 *
 * **Its name is shown whole and heard whole.** It wraps after its separators as a path does (`PathText`). It is never cut
 * to one line with the rest in a tip, since a tip is reached by neither a keyboard nor a reader. The row is named by
 * that text (`aria-labelledby`), so what is read and what is heard are one.
 */
export function BranchRow({ name, moving, word, children, under }: {
  name: string;
  /** Whether the press acts on it: its mark in the accent. */
  moving: boolean;
  /** The mark's word: *goes*, *kept*, *moves*, *stays*. */
  word: string;
  /** The sentence, and what it adds beneath. */
  children: ReactNode;
  /** A panel under the row, across its columns: a discard's ask. */
  under?: ReactNode;
}) {
  const named = useId();
  return (
    <li
      aria-labelledby={named}
      className="grid grid-cols-[4.5rem_minmax(0,1fr)] items-baseline gap-x-3 gap-y-0.5 py-1.5 @min-[30rem]/branches:grid-cols-[4.5rem_minmax(0,16rem)_minmax(13rem,1fr)] @min-[30rem]/branches:py-1"
    >
      <span><Chip accent={moving}>{word}</Chip></span>
      <span id={named} className="min-w-0 text-small text-ink"><PathText path={name} /></span>
      <span className="col-start-2 min-w-0 text-small text-ink-soft @min-[30rem]/branches:col-start-3">{children}</span>
      {under && <div className="col-span-full mt-1.5">{under}</div>}
    </li>
  );
}

/**
 * Session branches (WSR3, D88): every `daoris/` branch in a repository with a checkout here, with what it
 * holds — then one press removes those whose work is on a branch of the person's, or that hold nothing,
 * with their trees.
 *
 * @remarks
 * Daoris's branches go once their work has merged: the first real workspace left sixteen empty ones in one repository. **The list comes
 * first, and the press removes only what it listed to go** — the driver judges each again right before
 * it goes. Work no branch of the person's holds, uncommitted work, and a tree a session still uses are
 * kept and named, never removed from here. `daoris-driver trees clean` is the terminal's door (D50).
 *
 * **The branches landings made are their own group** (WSR5): a squash merge leaves git calling them
 * unmerged, so each is proven by content — every file it changed reads on the line as it left it, or it is
 * inside another that does — and goes by the same press. Only branches a landing made here are listed.
 *
 * **A session branch a completed pull request carried goes too** (PLUGHOOK1a, D148 point 4): a squash left its commits on no
 * branch of the person's, and the landed branch's plugin answered that its pull request carried them, where git confirms it.
 * Its row names that landed branch (SWEEPCARRIED1).
 *
 * **A failed or superseded attempt's branch is discarded by its own press** (LAND3b, D102's LAND3 note): its commits are on
 * no branch of the person's, so the clean-up keeps it. Beside each row the driver says `discardable`, *Discard branch…*
 * asks once under the row and then discards it, as `daoris-driver trees remove … --force` does beside the same rows. The
 * ask stays open until the driver answers, and says a branch it kept inside itself (UXFIX2).
 *
 * **A kept branch whose tree is still here says how it lands** (LAND4, D102's LAND4 note): where the driver says `landable`,
 * the row names the session its tree is and how it ended, and that its page offers Accept, as `daoris-driver trees land`
 * does from the terminal, whose list prints that line beside the same row. Words only: the press is the session's.
 */
export function SweepList({ branches, landed, busy, onLook, onClean, onDiscard, discarding = null, sync }: {
  /** Undefined while the driver is asked; empty when there is no session branch at all. */
  branches?: SweepBranch[];
  /** The branches landings made; the group is drawn only where there is one. */
  landed?: LandedBranch[];
  busy?: boolean;
  onLook: () => void;
  onClean: (only: string[]) => void;
  /**
   * Discard one branch the driver offers it for, once the person said so (LAND3b), told back to its ask (UXFIX2). Absent,
   * no row offers it.
   */
  onDiscard?: (branch: SweepBranch, answered: Answered) => void;
  /** The branch whose discard is on its way, by its `sweepKey`: its row's presses wait for it. */
  discarding?: string | null;
  /** Bringing repositories up to date after a pull request merged (WSR6), drawn first: it runs before a clean-up. */
  sync?: ReactNode;
}) {
  const { t } = useTranslation();
  // Which row's discard is asking, by its key: one at a time, and the first press is not offered twice while it asks.
  const [asking, setAsking] = useState<string | null>(null);
  const going = [...(branches ?? []).filter((branch) => branch.removable), ...(landed ?? []).filter((branch) => branch.removable)];
  const repositories = [...new Set((branches ?? []).map((branch) => branch.repository))].sort((a, b) => a.localeCompare(b));
  const landedIn = [...new Set((landed ?? []).map((branch) => branch.repository))].sort((a, b) => a.localeCompare(b));

  // Each kind the driver sends has its own case (SWEEPCARRIED1): a kind left to a shared fallthrough said another kind's
  // sentence, as `carried` said the in-use one. A kind this window has no words for is said as the driver named it.
  const holds = (branch: SweepBranch): string => {
    switch (branch.kind) {
      case 'empty': return t('settings.sweep.kind.empty', { line: branch.where ?? '' });
      case 'landed': return branch.where
        ? t('settings.sweep.kind.landedOn', { where: branch.where })
        : t('settings.sweep.kind.landed');
      case 'carried': return t('settings.sweep.kind.carried', { where: branch.where ?? '' });
      case 'unlanded': return branch.commits > 0
        ? t('settings.sweep.kind.unlanded', { count: branch.commits })
        : t('settings.sweep.kind.unknown');
      case 'dirty': return t('settings.sweep.kind.dirty');
      case 'in-use': return t('settings.sweep.kind.inUse');
      default: {
        // Unreachable by type, so a kind added to `SweepKind` without its case fails the build.
        const unknown: never = branch.kind;
        return t('settings.sweep.kind.other', { kind: unknown });
      }
    }
  };

  const shows = (branch: LandedBranch) => {
    switch (branch.kind) {
      case 'on-line': return t('settings.sweep.landed.kind.onLine', { where: branch.where ?? '' });
      case 'merged': return t('settings.sweep.landed.kind.merged', { where: branch.where ?? '' });
      case 'inside': return t('settings.sweep.landed.kind.inside', { where: branch.where ?? '' });
      case 'differs': return t('settings.sweep.landed.kind.differs', { count: branch.files.length });
      case 'checked-out': return t('settings.sweep.landed.kind.checkedOut');
      case 'ahead-of-remote': return t('settings.sweep.landed.kind.aheadOfRemote', { count: branch.commits });
      case 'leaned-on': return t('settings.sweep.landed.kind.leanedOn', { where: branch.where ?? '' });
      default: return t('settings.sweep.landed.kind.unknown');
    }
  };
  // What the row adds beneath its sentence: the files that keep it, or git's own words. Content.
  const beneath = (branch: LandedBranch) => branch.kind === 'differs'
    ? branch.files.join(', ')
    : branch.kind === 'unknown' ? branch.detail ?? null : null;

  return (
    <Card id="settings-sweep" className="mt-3.5 scroll-mt-3">
      <SectionTitle>{t('settings.sweep.title')}</SectionTitle>
      <Prose className="mt-1 text-small text-ink-soft">{t('settings.sweep.body')}</Prose>

      {sync}

      {branches?.length === 0 && <Prose className="mt-3">{t('settings.sweep.none')}</Prose>}

      {repositories.map((repository) => (
        <section key={repository} aria-label={repository} className="mt-3 border-t border-line pt-3">
          <div className="text-body font-medium text-ink">{repository}</div>
          <ul className={cn(BRANCH_ROWS, 'mt-1.5')}>
            {(branches ?? []).filter((branch) => branch.repository === repository).map((branch) => {
              const key = sweepKey(branch);
              const discardable = Boolean(onDiscard && branch.discardable);
              return (
                <BranchRow
                  key={branch.branch}
                  name={branch.branch}
                  moving={branch.removable}
                  word={t(branch.removable ? 'settings.sweep.goes' : 'settings.sweep.kept')}
                  // Open until the discard answers (UXFIX2), its presses waiting for it.
                  under={discardable && asking === key && (
                    <DiscardBranchAsk
                      branch={branch}
                      busy={busy || discarding === key}
                      onDiscard={(answered) => onDiscard!(branch, answered)}
                      onClose={() => setAsking((was) => (was === key ? null : was))}
                    />
                  )}
                >
                  <Inline text={holds(branch)} />
                  {branch.kind === 'unlanded' && branch.detail && (
                    <span className="mt-0.5 block whitespace-pre-line font-mono text-meta text-ink-faint">{branch.detail}</span>
                  )}
                  {/* LAND4: whose work it is, and how it lands: its session's page, or the terminal's line beside the row. */}
                  {branch.landable && branch.session && (
                    <span className="mt-0.5 block">
                      <Inline text={t('settings.sweep.lands', {
                        session: branch.session.id, state: t(`sessionState.${branch.session.state}`),
                      })} />
                    </span>
                  )}
                  {discardable && asking !== key && (
                    <span className="mt-1 block">
                      <Button
                        variant="danger"
                        className="px-2 py-0.5 text-small"
                        disabled={busy || discarding === key}
                        onClick={() => setAsking(key)}
                      >
                        {t('settings.sweep.discard')}
                      </Button>
                    </span>
                  )}
                </BranchRow>
              );
            })}
          </ul>
        </section>
      ))}

      {landed && landed.length > 0 && (
        <section aria-label={t('settings.sweep.landed.title')} className="mt-3 border-t border-line pt-3">
          <div className="text-body font-medium text-ink">{t('settings.sweep.landed.title')}</div>
          <Prose className="mt-1 text-small text-ink-soft">{t('settings.sweep.landed.body')}</Prose>
          {landedIn.map((repository) => (
            <div key={repository} role="group" aria-label={repository} className="mt-2">
              <div className="text-small font-medium text-ink-soft">{repository}</div>
              <ul className={cn(BRANCH_ROWS, 'mt-1')}>
                {landed.filter((branch) => branch.repository === repository).map((branch) => (
                  <BranchRow
                    key={branch.branch}
                    name={branch.branch}
                    moving={branch.removable}
                    word={t(branch.removable ? 'settings.sweep.goes' : 'settings.sweep.kept')}
                  >
                    <Inline text={shows(branch)} />
                    {beneath(branch) && (
                      <span className="mt-0.5 block whitespace-pre-line break-words font-mono text-meta text-ink-faint">{beneath(branch)}</span>
                    )}
                  </BranchRow>
                ))}
              </ul>
            </div>
          ))}
        </section>
      )}

      <div className="mt-3 flex flex-wrap items-center gap-2 border-t border-line pt-3">
        <Button variant="primary" disabled={busy || going.length === 0} onClick={() => onClean(going.map(sweepKey))}>
          {t('settings.sweep.clean', { count: going.length })}
        </Button>
        <Button variant="ghost" disabled={busy} onClick={onLook}>{t('settings.sweep.look')}</Button>
      </div>
    </Card>
  );
}
