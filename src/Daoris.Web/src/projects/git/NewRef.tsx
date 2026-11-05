import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Prose, Segmented } from '../../design/ui';
import { PushRef } from './PushRef';

/** What the driver answers a ref act with (`GIT_REF_CREATE`, `GIT_REF_UNDO`): the journal's act and its outcome, or a refusal in words. */
export type RefOutcome = { act?: string | null; outcome?: string | null; refused: boolean; said: string; code?: string | null };

/**
 * The acts the holder hands: make a ref at a commit, delete or rename one at the commit shown, finish a rename that stopped between its ref
 * and its settings, and undo what an act did.
 */
export type RefActs = {
  make: (ask: { kind: RefKind; name: string; at: string }) => Promise<RefOutcome>;
  remove: (ask: { kind: RefKind; name: string; at: string }) => Promise<RefOutcome>;
  rename: (ask: { kind: RefKind; name: string; to: string; at: string }) => Promise<RefOutcome>;
  finish: (act: string) => Promise<RefOutcome>;
  undo: (act: string) => Promise<RefOutcome>;
  /** *Fetch origin* (GITAPP2f); absent, the tab offers none. */
  fetch?: FetchActs;
  /** *Push* and *Push tag* (GITAPP2g, n); absent, the tab offers neither. */
  push?: PushActs;
};

/**
 * The push the holder hands: whether the driver offers it, why not, origin's push address as the driver redacts it, the place's Dangerous
 * value (which decides a tag at the door), and the press, carrying the commit the page showed.
 */
export type PushActs = {
  offered: boolean;
  reason?: string | null;
  why?: string | null;
  address?: string | null;
  dangerous: string;
  press: (ask: { kind: RefKind; name: string; at: string }) => Promise<FetchOutcome>;
};

/** What a fetch answers: the ref act's outcome, and in `notes` the refs it made, moved and removed. */
export type FetchOutcome = RefOutcome & { notes?: string | null };

/**
 * The fetch the holder hands: whether the driver offers it (its git doors installed, an origin, no setting of origin's that runs another
 * program), why not in a word and a sentence, origin's address as the driver redacts it, and the press.
 */
export type FetchActs = {
  offered: boolean;
  reason?: string | null;
  why?: string | null;
  address?: string | null;
  press: () => Promise<FetchOutcome>;
};

export type RefKind = 'branch' | 'tag';

type Stage =
  | { is: 'form' }
  | { is: 'confirm' }
  | { is: 'working' }
  | { is: 'made'; act: string; kind: RefKind; name: string }
  | { is: 'undoing'; act: string; kind: RefKind; name: string }
  | { is: 'undone'; kind: RefKind; name: string }
  | { is: 'said'; sentence: string }
  | { is: 'unknown' };

const short = (commit: string) => commit.slice(0, 7);

/**
 * **New branch or tag** (GITAPP2c, D204; the git-writes design §6): a disclosure on a repository's Branches. The person names a
 * branch or a lightweight tag, is told what will be made and at which commit, and confirms; only then is the press sent. The
 * driver answers as data: made (with an *Undo*, which deletes the ref only at the id it was made at), refused in its own words
 * with the name kept to change, or not known, which is said and never retried.
 *
 * @remarks
 * **A molecule**: the acts arrive as a prop. It is drawn only where the checkout has a commit to point at.
 */
export function NewRef({ at, acts, initial }: {
  /** Where the ref will point: the line's name and its whole commit id. */
  at: { label: string; commit: string };
  acts: RefActs;
  /** Open at a step already, as its stories show each; absent, it opens closed. */
  initial?: { kind?: RefKind; name?: string; stage?: 'form' | 'confirm' | 'made' | 'said' | 'unknown'; sentence?: string };
}) {
  const { t } = useTranslation();
  const heading = useId();
  const [open, setOpen] = useState(initial !== undefined);
  const [kind, setKind] = useState<RefKind>(initial?.kind ?? 'branch');
  const [name, setName] = useState(initial?.name ?? '');
  const [stage, setStage] = useState<Stage>(() => {
    const made = { act: 'a1', kind: initial?.kind ?? 'branch', name: initial?.name ?? '' };
    switch (initial?.stage) {
      case 'confirm': return { is: 'confirm' };
      case 'made': return { is: 'made', ...made };
      case 'said': return { is: 'said', sentence: initial.sentence ?? '' };
      case 'unknown': return { is: 'unknown' };
      default: return { is: 'form' };
    }
  });
  const trimmed = name.trim();

  if (!open) {
    return <div><Button variant="ghost" onClick={() => setOpen(true)}>{t('projects.git.ref.open')}</Button></div>;
  }

  const close = () => { setOpen(false); setStage({ is: 'form' }); setName(''); };
  const make = async () => {
    setStage({ is: 'working' });
    try {
      const outcome = await acts.make({ kind, name: trimmed, at: at.commit });
      if (outcome.refused) setStage({ is: 'said', sentence: outcome.said });
      else if (outcome.outcome === 'done' && outcome.act) setStage({ is: 'made', act: outcome.act, kind, name: trimmed });
      else if (outcome.outcome === 'not-done') setStage({ is: 'said', sentence: t('projects.git.ref.notDone', { said: outcome.said }) });
      else setStage({ is: 'unknown' });
    } catch (error) {
      setStage({ is: 'said', sentence: error instanceof Error ? error.message : t('projects.git.ref.failed') });
    }
  };
  const undo = async (made: { act: string; kind: RefKind; name: string }) => {
    setStage({ is: 'undoing', ...made });
    try {
      const outcome = await acts.undo(made.act);
      if (!outcome.refused && outcome.outcome === 'done') setStage({ is: 'undone', kind: made.kind, name: made.name });
      else if (outcome.refused || outcome.outcome === 'not-done') setStage({ is: 'said', sentence: outcome.said });
      else setStage({ is: 'unknown' });
    } catch (error) {
      setStage({ is: 'said', sentence: error instanceof Error ? error.message : t('projects.git.ref.failed') });
    }
  };

  const editing = stage.is === 'form' || stage.is === 'said';
  return (
    <div role="group" aria-labelledby={heading} className="grid min-w-0 gap-2 rounded-control border border-line p-3">
      <h3 id={heading} className="m-0 text-small font-semibold text-ink-faint">{t('projects.git.ref.heading')}</h3>
      {editing && (
        <>
          <Segmented<RefKind>
            label={t('projects.git.ref.kind')}
            value={kind}
            onChange={setKind}
            options={[{ value: 'branch', label: t('projects.git.ref.branch') }, { value: 'tag', label: t('projects.git.ref.tag') }]}
          />
          <label className="grid min-w-0 gap-1">
            <span className="text-small text-ink-faint">{t('projects.git.ref.name')}</span>
            <input
              value={name}
              onChange={(event) => setName(event.target.value)}
              spellCheck={false}
              autoComplete="off"
              className="w-full min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 font-mono text-body"
            />
          </label>
          <Prose className="text-small text-ink-soft">{t('projects.git.ref.at', { at: at.label, id: short(at.commit) })}</Prose>
          {stage.is === 'said' && <p role="status" className="m-0 text-small text-ink [overflow-wrap:anywhere]">{stage.sentence}</p>}
          <div className="flex flex-wrap gap-2">
            <Button variant="primary" disabled={trimmed.length === 0} onClick={() => setStage({ is: 'confirm' })}>{t('projects.git.ref.create')}</Button>
            <Button variant="ghost" onClick={close}>{t('projects.git.ref.cancel')}</Button>
          </div>
        </>
      )}
      {(stage.is === 'confirm' || stage.is === 'working') && (
        <>
          <Prose className="text-small [overflow-wrap:anywhere]">
            {t(`projects.git.ref.confirm.${kind}`, { name: trimmed, at: at.label, id: short(at.commit) })}
          </Prose>
          <div className="flex flex-wrap gap-2">
            <Button variant="primary" disabled={stage.is === 'working'} onClick={() => { void make(); }}>{t('projects.git.ref.confirmPress')}</Button>
            <Button variant="ghost" disabled={stage.is === 'working'} onClick={() => setStage({ is: 'form' })}>{t('projects.git.ref.back')}</Button>
          </div>
        </>
      )}
      {(stage.is === 'made' || stage.is === 'undoing') && (
        <>
          <p role="status" className="m-0 text-small text-ink [overflow-wrap:anywhere]">{t(`projects.git.ref.made.${stage.kind}`, { name: stage.name })}</p>
          {stage.kind === 'tag' && acts.push && stage.is === 'made' && <PushRef kind="tag" name={stage.name} commit={at.commit} push={acts.push} />}
          <div className="flex flex-wrap gap-2">
            <Button variant="ghost" disabled={stage.is === 'undoing'} onClick={() => { void undo(stage); }}>{t('projects.git.ref.undo')}</Button>
            <Button variant="ghost" onClick={close}>{t('projects.git.ref.done')}</Button>
          </div>
        </>
      )}
      {stage.is === 'undone' && (
        <>
          <p role="status" className="m-0 text-small text-ink [overflow-wrap:anywhere]">{t(`projects.git.ref.undone.${stage.kind}`, { name: stage.name })}</p>
          <div><Button variant="ghost" onClick={close}>{t('projects.git.ref.done')}</Button></div>
        </>
      )}
      {stage.is === 'unknown' && (
        <>
          <p role="status" className="m-0 text-small text-ink">{t('projects.git.ref.unknown')}</p>
          <div><Button variant="ghost" onClick={close}>{t('projects.git.ref.done')}</Button></div>
        </>
      )}
    </div>
  );
}
