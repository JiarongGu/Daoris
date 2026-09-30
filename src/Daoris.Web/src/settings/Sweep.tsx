import { useTranslation } from 'react-i18next';
import { Button, Card, Chip, Inline, Prose, SectionTitle } from '../ui';

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
};

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
 */
export function SweepList({ branches, landed, busy, onLook, onClean }: {
  /** Undefined while the driver is asked; empty when there is no session branch at all. */
  branches?: SweepBranch[];
  /** The branches landings made; the group is drawn only where there is one. */
  landed?: LandedBranch[];
  busy?: boolean;
  onLook: () => void;
  onClean: (only: string[]) => void;
}) {
  const { t } = useTranslation();
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

      {branches?.length === 0 && <Prose className="mt-3">{t('settings.sweep.none')}</Prose>}

      {repositories.map((repository) => (
        <section key={repository} aria-label={repository} className="mt-3 border-t border-line pt-3">
          <div className="text-body font-medium text-ink">{repository}</div>
          <ul className="m-0 mt-1.5 list-none p-0">
            {(branches ?? []).filter((branch) => branch.repository === repository).map((branch) => (
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
                </span>
              </li>
            ))}
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
