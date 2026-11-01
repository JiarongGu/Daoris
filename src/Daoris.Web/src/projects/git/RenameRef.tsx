import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Prose } from '../../design/ui';
import type { RefActs } from './NewRef';

const short = (commit: string) => commit.slice(0, 7);

type Step = { is: 'idle' } | { is: 'form' } | { is: 'confirm' } | { is: 'working' } | { is: 'said'; sentence: string };

/** What a rename handed the list: the act it ran as, the names, and whether it stopped before the branch's settings moved. */
export type RenamedRef = { act: string; name: string; to: string; partial: boolean };

/**
 * **Rename a branch** (GITAPP2e, D204; the git-writes design §6): a press on one of *Yours*' rows that asks for the new name, says what will
 * happen and where the old tip stays, and sends nothing until it is confirmed. The press carries the commit the row showed, so the driver
 * refuses a branch that has moved. A refusal is said in the driver's own sentence with the name kept to change; a branch that moved is handed
 * to the list (`onRenamed`), whose notice outlives this row and holds *Finish* and *Undo* (`Renamed`).
 *
 * @remarks
 * **A molecule**: the acts arrive as a prop. It is absent, never disabled, where the row has no commit.
 */
export function RenameRef({ name, commit, acts, onRenamed, initial }: {
  name: string;
  /** The branch's whole commit id, as the list showed it. */
  commit: string;
  acts: RefActs;
  onRenamed: (renamed: RenamedRef) => void;
  /** Open at a step already, as its stories show each. */
  initial?: { step: 'form' | 'confirm' | 'said'; to?: string; sentence?: string };
}) {
  const { t } = useTranslation();
  const [to, setTo] = useState(initial?.to ?? name);
  const [step, setStep] = useState<Step>(
    initial?.step === 'form' ? { is: 'form' }
      : initial?.step === 'confirm' ? { is: 'confirm' }
        : initial?.step === 'said' ? { is: 'said', sentence: initial.sentence ?? '' } : { is: 'idle' },
  );
  const trimmed = to.trim();

  const press = async () => {
    setStep({ is: 'working' });
    try {
      const outcome = await acts.rename({ kind: 'branch', name, to: trimmed, at: commit });
      if (outcome.refused) setStep({ is: 'said', sentence: outcome.said });
      else if (outcome.outcome === 'not-done') setStep({ is: 'said', sentence: t('projects.git.ref.rename.notDone', { said: outcome.said }) });
      else if (outcome.act && (outcome.outcome === 'done' || outcome.outcome === 'partial')) {
        onRenamed({ act: outcome.act, name, to: trimmed, partial: outcome.outcome === 'partial' });
      } else setStep({ is: 'said', sentence: t('projects.git.ref.unknown') });
    } catch (error) {
      setStep({ is: 'said', sentence: error instanceof Error ? error.message : t('projects.git.ref.failed') });
    }
  };

  if (step.is === 'idle') {
    return (
      <div>
        <Button variant="ghost" className="text-small" aria-label={t('projects.git.ref.rename.openLabel', { name })} onClick={() => setStep({ is: 'form' })}>
          {t('projects.git.ref.rename.open')}
        </Button>
      </div>
    );
  }

  const editing = step.is === 'form' || step.is === 'said';
  return (
    <div role="group" aria-label={t('projects.git.ref.rename.openLabel', { name })} className="grid min-w-0 gap-2 rounded-control border border-line p-3">
      {editing && (
        <>
          <label className="grid min-w-0 gap-1">
            <span className="text-small text-ink-faint">{t('projects.git.ref.rename.to')}</span>
            <input
              value={to}
              onChange={(event) => setTo(event.target.value)}
              spellCheck={false}
              autoComplete="off"
              className="w-full min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 font-mono text-body"
            />
          </label>
          {step.is === 'said' && <p role="status" className="m-0 text-small text-ink [overflow-wrap:anywhere]">{step.sentence}</p>}
          <div className="flex flex-wrap gap-2">
            <Button variant="primary" disabled={trimmed.length === 0 || trimmed === name} onClick={() => setStep({ is: 'confirm' })}>
              {t('projects.git.ref.rename.next')}
            </Button>
            <Button variant="ghost" onClick={() => setStep({ is: 'idle' })}>{t('projects.git.ref.cancel')}</Button>
          </div>
        </>
      )}
      {(step.is === 'confirm' || step.is === 'working') && (
        <>
          <Prose className="text-small [overflow-wrap:anywhere]">{t('projects.git.ref.rename.confirm', { name, to: trimmed, id: short(commit) })}</Prose>
          <div className="flex flex-wrap gap-2">
            <Button variant="primary" disabled={step.is === 'working'} onClick={() => { void press(); }}>{t('projects.git.ref.rename.press')}</Button>
            <Button variant="ghost" disabled={step.is === 'working'} onClick={() => setStep({ is: 'form' })}>{t('projects.git.ref.back')}</Button>
          </div>
        </>
      )}
    </div>
  );
}
