import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, CheckField, Inline, Prose } from '../../design/ui';
import type { GitRead } from './RepositoryGit';

/** What a commit or an amend rests on (the bridge's `GitCommitPlan`, handed as it came). */
export type CommitPlanValue = {
  head: string | null;
  tree: string | null;
  why: string | null;
  empty: boolean;
  message: string | null;
  hooks: readonly string[];
  identity: string | null;
};

/** The press: the verb, the message typed, and the tip and tree of the plan shown, which the driver finds unchanged or refuses. */
export type CommitBoxPress = { verb: 'commit' | 'amend'; message: string; head: string | null; tree: string };

/**
 * **The commit box of the Changes tab**: the message, *Amend the last commit*, the hooks the
 * repository's git runs (never skipped) and who commits. *Commit* is held where nothing is staged; an amend starts from HEAD's message.
 * What it sends back is the tip and tree it read, so a press is refused where either has moved.
 *
 * @remarks **A molecule**: the plan arrives as a prop, and `amend` is its holder's, since the plan differs by verb (an amend's git runs
 * one more hook). `busy` holds the press while an act runs.
 */
export function CommitBox({ read, amend, onAmend, busy, onPress }: {
  read: GitRead<CommitPlanValue>;
  amend: boolean;
  onAmend: (on: boolean) => void;
  busy: boolean;
  /** `done` is called once the act is done, so the box clears its message. */
  onPress: (press: CommitBoxPress, done: () => void) => void;
}) {
  const { t } = useTranslation();
  const [message, setMessage] = useState('');
  if (read.state === 'reading') return <Prose className="text-small text-ink-soft">{t('projects.git.commit.reading')}</Prose>;
  if (read.state === 'refused') return <Prose className="text-small text-ink-soft">{read.sentence}</Prose>;
  if (read.state === 'noCheckout') return null;
  const plan = read.value;
  const verb = amend ? 'amend' : 'commit';
  const blocked = plan.tree === null ? plan.why : amend ? (plan.head === null ? t('projects.git.commit.nothingToAmend') : null) : plan.empty ? t('projects.git.commit.nothing') : null;
  const toggle = (on: boolean) => {
    if (on && message.trim() === '' && plan.message) setMessage(plan.message.trim());
    onAmend(on);
  };
  return (
    <section aria-label={t('projects.git.commit.heading')} className="grid min-w-0 gap-2 rounded-control border border-line p-2">
      <label className="grid min-w-0 gap-1">
        <span className="text-small text-ink-faint">{t('projects.git.commit.message')}</span>
        <textarea
          value={message}
          rows={3}
          disabled={busy}
          onChange={(event) => setMessage(event.target.value)}
          className="w-full min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
        />
      </label>
      <CheckField checked={amend} onChange={toggle} disabled={busy || plan.head === null} label={t('projects.git.commit.amend')} />
      <Prose className="min-w-0 text-small text-ink-soft">
        <Inline text={plan.hooks.length === 0 ? t('projects.git.commit.hooksNone') : t('projects.git.commit.hooks', { hooks: plan.hooks.map((hook) => `\`${hook}\``).join(', ') })} />
      </Prose>
      <Prose className="min-w-0 text-small text-ink-soft">
        <Inline text={plan.identity ? t('projects.git.commit.identity', { who: plan.identity }) : t('projects.git.commit.identityNone')} />
      </Prose>
      {blocked && <Prose className="min-w-0 text-small text-ink-soft"><Inline text={blocked} /></Prose>}
      <div className="flex flex-wrap gap-2">
        <Button
          disabled={busy || blocked !== null || message.trim() === ''}
          onClick={() => onPress({ verb, message, head: plan.head, tree: plan.tree! }, () => { setMessage(''); onAmend(false); })}
        >
          {t(`projects.git.commit.press.${verb}`)}
        </Button>
      </div>
    </section>
  );
}
