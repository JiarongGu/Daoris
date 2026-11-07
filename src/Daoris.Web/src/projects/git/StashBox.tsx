import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, CheckField, Inline, Prose } from '../../design/ui';
import type { GitRead } from './RepositoryGit';

/** What a stash Save rests on (the bridge's `GitStashPlan`, handed as it came). */
export type StashPlanValue = {
  head: string | null;
  stash: string | null;
  tracked: number;
  untracked: number;
  sharedWith: number;
  why: string | null;
  hooks: readonly string[];
  /** The shared stash newest first, each by its commit id; `collides` counts its untracked paths already standing here. */
  entries: readonly { id: string; message: string; untracked: boolean; collides: number; mine: boolean }[];
  /** Why no stash could be applied now; null where one could. */
  applyWhy: string | null;
};

/** An Apply as pressed: the stash's commit id, never its place on the list, and the commit the plan shown stood on. */
export type StashApplyBoxPress = { id: string; head: string | null };

/** The press: untracked files too or not, the message typed, and the tip and stash of the plan shown, which the driver finds unchanged or refuses. */
export type StashBoxPress = { untracked: boolean; message: string; head: string | null; stash: string | null };

/**
 * **The stash box of the Changes tab**: *Save to the stash*, tracked files only unless *Include
 * untracked files* is ticked, and the plan's plain words that the stash is shared by every linked tree, sessions' included, so a Save here is
 * seen there. Beneath it the saved stashes, each with *Apply* by its commit id, which keeps the stash and leaves a conflict to
 * Resolve, and *Drop* by its commit id for a stash a Save of Daoris's made (the answer names the command that stores it again); the others
 * say they are dropped where they were made.
 *
 * @remarks **A molecule**: the plan arrives as a prop. `busy` holds the press while an act runs.
 */
export function StashBox({ read, busy, onPress, onApply, onDrop }: {
  read: GitRead<StashPlanValue>;
  busy: boolean;
  onApply: (press: StashApplyBoxPress) => void;
  /** *Drop* by the stash's commit id, offered only for a stash a Save of Daoris's made. */
  onDrop: (id: string) => void;
  /** `done` is called once the act is done, so the box clears its message. */
  onPress: (press: StashBoxPress, done: () => void) => void;
}) {
  const { t } = useTranslation();
  const [message, setMessage] = useState('');
  const [untracked, setUntracked] = useState(false);
  if (read.state === 'reading') return <Prose className="text-small text-ink-soft">{t('projects.git.stash.reading')}</Prose>;
  if (read.state === 'refused') return <Prose className="text-small text-ink-soft">{read.sentence}</Prose>;
  if (read.state === 'noCheckout') return null;
  const plan = read.value;
  const blocked = plan.why ?? (plan.tracked === 0 && !untracked ? t('projects.git.stash.onlyUntracked') : null);
  return (
    <section aria-label={t('projects.git.stash.heading')} className="grid min-w-0 gap-2 rounded-control border border-line p-2">
      <h3 className="m-0 text-small font-semibold text-ink-faint">{t('projects.git.stash.heading')}</h3>
      <Prose className="min-w-0 text-small text-ink-soft"><Inline text={t('projects.git.stash.shared', { count: plan.sharedWith })} /></Prose>
      <Prose className="min-w-0 text-small text-ink-soft">
        <Inline text={t('projects.git.stash.counts', { tracked: plan.tracked, untracked: plan.untracked })} />
      </Prose>
      <label className="grid min-w-0 gap-1">
        <span className="text-small text-ink-faint">{t('projects.git.stash.message')}</span>
        <input
          value={message}
          disabled={busy}
          onChange={(event) => setMessage(event.target.value)}
          className="w-full min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
        />
      </label>
      <CheckField checked={untracked} onChange={setUntracked} disabled={busy} label={t('projects.git.stash.untracked')} />
      {blocked && <Prose className="min-w-0 text-small text-ink-soft"><Inline text={blocked} /></Prose>}
      <div className="flex flex-wrap gap-2">
        <Button
          disabled={busy || blocked !== null}
          onClick={() => onPress({ untracked, message, head: plan.head, stash: plan.stash }, () => { setMessage(''); setUntracked(false); })}
        >
          {t('projects.git.stash.press')}
        </Button>
      </div>
      <h4 className="m-0 mt-1 text-small font-semibold text-ink-faint">{t('projects.git.stash.saved')}</h4>
      {plan.entries.length === 0 ? (
        <Prose className="min-w-0 text-small text-ink-soft">{t('projects.git.stash.none')}</Prose>
      ) : (
        <>
          <Prose className="min-w-0 text-small text-ink-soft"><Inline text={plan.applyWhy ?? t('projects.git.stash.applyNote')} /></Prose>
          <ul className="m-0 grid min-w-0 list-none gap-2 p-0">
            {plan.entries.map((entry) => (
              <li key={entry.id} className="grid min-w-0 gap-1 rounded-control border border-line p-2">
                <span className="min-w-0 break-words text-small text-ink">{entry.message || t('projects.git.stash.noMessage')}</span>
                <code className="min-w-0 break-all text-small text-ink-faint">{entry.id.slice(0, 12)}</code>
                {entry.untracked && <span className="text-small text-ink-soft">{t('projects.git.stash.holdsUntracked')}</span>}
                {entry.collides > 0 && <span className="text-small text-ink-soft">{t('projects.git.stash.collides', { count: entry.collides })}</span>}
                <div className="flex flex-wrap gap-2">
                  <Button
                    aria-label={t('projects.git.stash.applyOne', { id: entry.id.slice(0, 12) })}
                    disabled={busy || plan.applyWhy !== null || entry.collides > 0}
                    onClick={() => onApply({ id: entry.id, head: plan.head })}
                  >
                    {t('projects.git.stash.apply')}
                  </Button>
                  {entry.mine ? (
                    <Button aria-label={t('projects.git.stash.dropOne', { id: entry.id.slice(0, 12) })} disabled={busy} onClick={() => onDrop(entry.id)}>
                      {t('projects.git.stash.drop')}
                    </Button>
                  ) : (
                    <span className="text-small text-ink-soft">{t('projects.git.stash.notDropped')}</span>
                  )}
                </div>
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  );
}
