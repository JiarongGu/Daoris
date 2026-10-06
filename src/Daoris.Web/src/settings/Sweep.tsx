import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Card, Chip, Inline, type Notify, Prose, SectionTitle } from '../ui';

/** What a session branch holds (D88). Only `empty` and `landed` go. */
export type SweepKind = 'empty' | 'landed' | 'unlanded' | 'dirty' | 'in-use';

/** One session branch on this machine, as the driver judged it — the clean-up's list. */
export type SweepBranch = {
  repository: string;
  workspace: string;
  branch: string;
  hasTree: boolean;
  kind: SweepKind;
  /** Unlanded: how many commits only Daoris's branches hold. */
  commits: number;
  /** Landed: the first branch of the person's that holds it. Empty: the line. */
  where?: string | null;
  /** Git's own lines, where they say more. Content, never translated. */
  detail?: string | null;
  removable: boolean;
  /**
   * Whether its discard is offered beside it (LAND3b), by the driver's own rule: a failed or superseded attempt's commits,
   * which no clean-up takes. A shell older than LAND3b answers none, and nothing is offered.
   */
  discardable?: boolean;
};

/** What discarding a session branch answered (LAND3b): done, or the driver's sentence for why it was kept. */
export type BranchDiscard = { repository: string; branch: string; done: boolean; message: string };

/**
 * A discard's answer said once (LAND3b): the branch gone in the reader's words, which name no tree's path; a branch kept
 * in the driver's own sentence, whole, since a refusal is the contract.
 */
export const sayDiscard = (notify: Notify, t: (key: string, options?: Record<string, unknown>) => string) =>
  (result: BranchDiscard) => (result.done
    ? notify(t('settings.sweep.discarded', { branch: result.branch, repository: result.repository }))
    : notify(result.message, 'error'));

/**
 * A session branch's discard, asked once (LAND3b, D102's LAND3 note): it names the branch, its repository, its tree where
 * it still has one, and that its commits go with it for good, beside the move and *never mind* (platform language §4).
 * The commits are the ones the driver counted that no branch of the person's holds; the press is forced, as
 * `daoris-driver trees remove <branch> --repository <name> --force` is.
 */
export function DiscardBranchAsk({ branch, busy = false, onDiscard, onCancel }: {
  branch: Pick<SweepBranch, 'repository' | 'branch' | 'commits' | 'hasTree'>;
  /** A discard on its way: the presses wait for it. */
  busy?: boolean;
  onDiscard: () => void;
  onCancel: () => void;
}) {
  const { t } = useTranslation();
  const says = t(branch.hasTree ? 'settings.sweep.discardSaysTree' : 'settings.sweep.discardSays', {
    branch: branch.branch, repository: branch.repository, count: branch.commits,
  });
  return (
    <div
      role="group"
      aria-label={t('settings.sweep.discardTitle', { branch: branch.branch })}
      className="flex flex-wrap items-center gap-2 rounded-control border border-line bg-sunken px-2.5 py-2"
    >
      <span className="min-w-0 flex-1 basis-64 text-small text-ink-soft"><Inline text={says} /></span>
      <Button variant="danger" disabled={busy} onClick={onDiscard}>{t('settings.sweep.discardMeanIt')}</Button>
      <Button variant="ghost" disabled={busy} onClick={onCancel}>{t('common.cancel')}</Button>
    </div>
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
 * **A failed or superseded attempt's branch is discarded by its own press** (LAND3b, D102's LAND3 note): its commits are on
 * no branch of the person's, so the clean-up keeps it. Beside each row the driver says `discardable`, *Discard branch…*
 * asks once under the row and then discards it, as `daoris-driver trees remove … --force` does beside the same rows.
 */
export function SweepList({ branches, landed, busy, onLook, onClean, onDiscard, discarding = null, sync }: {
  /** Undefined while the driver is asked; empty when there is no session branch at all. */
  branches?: SweepBranch[];
  /** The branches landings made; the group is drawn only where there is one. */
  landed?: LandedBranch[];
  busy?: boolean;
  onLook: () => void;
  onClean: (only: string[]) => void;
  /** Discard one branch the driver offers it for, once the person said so (LAND3b). Absent, no row offers it. */
  onDiscard?: (branch: SweepBranch) => void;
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

  const holds = (branch: SweepBranch) => {
    switch (branch.kind) {
      case 'empty': return t('settings.sweep.kind.empty', { line: branch.where ?? '' });
      case 'landed': return branch.where
        ? t('settings.sweep.kind.landedOn', { where: branch.where })
        : t('settings.sweep.kind.landed');
      case 'unlanded': return branch.commits > 0
        ? t('settings.sweep.kind.unlanded', { count: branch.commits })
        : t('settings.sweep.kind.unknown');
      case 'dirty': return t('settings.sweep.kind.dirty');
      default: return t('settings.sweep.kind.inUse');
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
          <ul className="m-0 mt-1.5 list-none p-0">
            {(branches ?? []).filter((branch) => branch.repository === repository).map((branch) => {
              const key = sweepKey(branch);
              const discardable = Boolean(onDiscard && branch.discardable);
              return (
                <li
                  key={branch.branch}
                  aria-label={branch.branch}
                  className="grid grid-cols-[4.5rem_minmax(0,16rem)_minmax(0,1fr)] items-baseline gap-x-3 py-1"
                >
                  <span>
                    <Chip accent={branch.removable}>{t(branch.removable ? 'settings.sweep.goes' : 'settings.sweep.kept')}</Chip>
                  </span>
                  <span className="truncate font-mono text-small text-ink">{branch.branch}</span>
                  <span className="min-w-0 text-small text-ink-soft">
                    <Inline text={holds(branch)} />
                    {branch.kind === 'unlanded' && branch.detail && (
                      <span className="mt-0.5 block whitespace-pre-line font-mono text-meta text-ink-faint">{branch.detail}</span>
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
                  </span>
                  {discardable && asking === key && (
                    <div className="col-span-full mt-1.5">
                      <DiscardBranchAsk
                        branch={branch}
                        busy={busy || discarding === key}
                        onDiscard={() => {
                          setAsking(null);
                          onDiscard!(branch);
                        }}
                        onCancel={() => setAsking(null)}
                      />
                    </div>
                  )}
                </li>
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
              <ul className="m-0 mt-1 list-none p-0">
                {landed.filter((branch) => branch.repository === repository).map((branch) => (
                  <li
                    key={branch.branch}
                    aria-label={branch.branch}
                    className="grid grid-cols-[4.5rem_minmax(0,16rem)_minmax(0,1fr)] items-baseline gap-x-3 py-1"
                  >
                    <span>
                      <Chip accent={branch.removable}>{t(branch.removable ? 'settings.sweep.goes' : 'settings.sweep.kept')}</Chip>
                    </span>
                    <span className="truncate font-mono text-small text-ink">{branch.branch}</span>
                    <span className="min-w-0 text-small text-ink-soft">
                      <Inline text={shows(branch)} />
                      {beneath(branch) && (
                        <span className="mt-0.5 block whitespace-pre-line break-words font-mono text-meta text-ink-faint">{beneath(branch)}</span>
                      )}
                    </span>
                  </li>
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
