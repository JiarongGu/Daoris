import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Prose, Segmented } from '../../design/ui';

export type HistoryEditKind = 'rebase' | 'pick' | 'revert';

/** What a plan showed: the branch's tip, the commit the edit builds on, and the commits it takes, in order. */
export type HistoryPlanShown = { tip: string; onto: string; commits: { id: string; subject: string }[] };

/** The two presses the holder hands: the plan, which changes nothing, and the edit, carrying the ids the plan pinned. */
export type HistoryEdits = {
  plan: (ask: { edit: HistoryEditKind; branch: string; target: string }) => Promise<{ refused: boolean; said: string; plan?: HistoryPlanShown | null }>;
  edit: (ask: { edit: HistoryEditKind; branch: string; target: string; pins: { tip: string; onto: string; commits: string[] } }) =>
    Promise<{ act?: string | null; outcome?: string | null; refused: boolean; said: string; tip?: string | null; kept?: string | null }>;
};

type Stage =
  | { is: 'form' }
  | { is: 'planning' }
  | { is: 'plan'; plan: HistoryPlanShown }
  | { is: 'working'; plan: HistoryPlanShown }
  | { is: 'done'; tip: string; kept: string }
  | { is: 'said'; sentence: string }
  | { is: 'unknown' };

const short = (commit: string) => commit.slice(0, 7);

const KINDS: HistoryEditKind[] = ['rebase', 'pick', 'revert'];

/**
 * **Edit history** (GITAPP2m, D204; the git-writes design §7): a disclosure on a branch's page, for a branch of the person's own. They
 * choose to rebase it onto a commit (its line, at first), or to pick or revert a commit or an `A..B` range onto it; the driver's plan is
 * asked and shown, its commits in the order they are made; only *Confirm* sends the edit, carrying the ids the plan pinned. The driver
 * answers as data: done (the branch's new tip and where its old one is kept), refused in its own words with what was typed kept, or not
 * known, which is said and never retried.
 *
 * @remarks
 * **A molecule**: the presses arrive as a prop. Nothing here touches a working tree: the commits are made from git's objects.
 */
export function HistoryEdit({ branch, line, edits, initial }: {
  branch: string;
  /** The branch's line, the first commit to rebase onto; null where none is named. */
  line: string | null;
  edits: HistoryEdits;
  /** Open at a step already, as its stories show each; absent, it opens closed. */
  initial?: {
    edit?: HistoryEditKind; target?: string; stage?: 'form' | 'plan' | 'done' | 'said' | 'unknown'; plan?: HistoryPlanShown; sentence?: string;
    tip?: string; kept?: string;
  };
}) {
  const { t } = useTranslation();
  const heading = useId();
  const [open, setOpen] = useState(initial !== undefined);
  const [kind, setKind] = useState<HistoryEditKind>(initial?.edit ?? 'rebase');
  const [target, setTarget] = useState(initial?.target ?? ((initial?.edit ?? 'rebase') === 'rebase' ? line ?? '' : ''));
  const [stage, setStage] = useState<Stage>(() => {
    switch (initial?.stage) {
      case 'plan': return initial.plan ? { is: 'plan', plan: initial.plan } : { is: 'form' };
      case 'done': return { is: 'done', tip: initial.tip ?? '', kept: initial.kept ?? '' };
      case 'said': return { is: 'said', sentence: initial.sentence ?? '' };
      case 'unknown': return { is: 'unknown' };
      default: return { is: 'form' };
    }
  });
  const trimmed = target.trim();

  if (!open) {
    return <div><Button variant="ghost" onClick={() => setOpen(true)}>{t('projects.git.edit.open')}</Button></div>;
  }

  const close = () => { setOpen(false); setStage({ is: 'form' }); setKind('rebase'); setTarget(line ?? ''); };
  const choose = (next: HistoryEditKind) => { setKind(next); setTarget(next === 'rebase' ? line ?? '' : ''); setStage({ is: 'form' }); };
  const failed = (error: unknown) => setStage({ is: 'said', sentence: error instanceof Error ? error.message : t('projects.git.edit.failed') });
  const askPlan = async () => {
    setStage({ is: 'planning' });
    try {
      const answer = await edits.plan({ edit: kind, branch, target: trimmed });
      if (answer.refused || !answer.plan) setStage({ is: 'said', sentence: answer.said });
      else setStage({ is: 'plan', plan: answer.plan });
    } catch (error) {
      failed(error);
    }
  };
  const confirm = async (plan: HistoryPlanShown) => {
    setStage({ is: 'working', plan });
    try {
      const pins = { tip: plan.tip, onto: plan.onto, commits: plan.commits.map((each) => each.id) };
      const answer = await edits.edit({ edit: kind, branch, target: trimmed, pins });
      if (!answer.refused && answer.outcome === 'done' && answer.tip) setStage({ is: 'done', tip: answer.tip, kept: answer.kept ?? '' });
      else if (answer.refused) setStage({ is: 'said', sentence: answer.said });
      else if (answer.outcome === 'not-done') setStage({ is: 'said', sentence: t('projects.git.edit.notDone', { said: answer.said }) });
      else setStage({ is: 'unknown' });
    } catch (error) {
      failed(error);
    }
  };

  const editing = stage.is === 'form' || stage.is === 'planning' || stage.is === 'said';
  return (
    <div role="group" aria-labelledby={heading} className="grid min-w-0 gap-2 rounded-control border border-line p-3">
      <h3 id={heading} className="m-0 text-small font-semibold text-ink-faint">{t('projects.git.edit.heading')}</h3>
      {editing && (
        <>
          <Segmented<HistoryEditKind>
            label={t('projects.git.edit.kind')}
            value={kind}
            onChange={choose}
            options={KINDS.map((each) => ({ value: each, label: t(`projects.git.edit.${each}`) }))}
          />
          <label className="grid min-w-0 gap-1">
            <span className="text-small text-ink-faint">{t(`projects.git.edit.target.${kind}`)}</span>
            <input
              value={target}
              onChange={(event) => setTarget(event.target.value)}
              spellCheck={false}
              autoComplete="off"
              className="w-full min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 font-mono text-body"
            />
          </label>
          <Prose className="text-small text-ink-soft">{t('projects.git.edit.about')}</Prose>
          {stage.is === 'planning' && <p role="status" className="m-0 text-small text-ink-soft">{t('projects.git.edit.planning')}</p>}
          {stage.is === 'said' && <p role="status" className="m-0 text-small text-ink [overflow-wrap:anywhere]">{stage.sentence}</p>}
          <div className="flex flex-wrap gap-2">
            <Button variant="primary" disabled={trimmed.length === 0 || stage.is === 'planning'} onClick={() => { void askPlan(); }}>
              {t('projects.git.edit.plan')}
            </Button>
            <Button variant="ghost" onClick={close}>{t('projects.git.edit.cancel')}</Button>
          </div>
        </>
      )}
      {(stage.is === 'plan' || stage.is === 'working') && (
        <>
          <Prose className="text-small [overflow-wrap:anywhere]">
            {t(`projects.git.edit.planned.${kind}`, {
              count: stage.plan.commits.length, branch, target: trimmed, id: short(kind === 'rebase' ? stage.plan.onto : stage.plan.tip),
            })}
          </Prose>
          <ol aria-label={t('projects.git.edit.list')} className="m-0 grid min-w-0 list-none gap-1 p-0">
            {stage.plan.commits.map((commit) => (
              <li key={commit.id} className="flex min-w-0 items-baseline gap-2 text-small">
                <span className="shrink-0 font-mono text-ink-faint">{short(commit.id)}</span>
                <span className="min-w-0 [overflow-wrap:anywhere]">{commit.subject}</span>
              </li>
            ))}
          </ol>
          <Prose className="text-small text-ink-soft">{t('projects.git.edit.about')}</Prose>
          {stage.is === 'working' && <p role="status" className="m-0 text-small text-ink-soft">{t('projects.git.edit.working')}</p>}
          <div className="flex flex-wrap gap-2">
            <Button variant="primary" disabled={stage.is === 'working'} onClick={() => { void confirm(stage.plan); }}>{t('projects.git.edit.confirmPress')}</Button>
            <Button variant="ghost" disabled={stage.is === 'working'} onClick={() => setStage({ is: 'form' })}>{t('projects.git.edit.back')}</Button>
          </div>
        </>
      )}
      {stage.is === 'done' && (
        <>
          <p role="status" className="m-0 text-small text-ink [overflow-wrap:anywhere]">
            {t('projects.git.edit.done', { branch, id: short(stage.tip), kept: stage.kept })}
          </p>
          <div><Button variant="ghost" onClick={close}>{t('projects.git.edit.finish')}</Button></div>
        </>
      )}
      {stage.is === 'unknown' && (
        <>
          <p role="status" className="m-0 text-small text-ink">{t('projects.git.edit.unknown')}</p>
          <div><Button variant="ghost" onClick={close}>{t('projects.git.edit.finish')}</Button></div>
        </>
      )}
    </div>
  );
}
