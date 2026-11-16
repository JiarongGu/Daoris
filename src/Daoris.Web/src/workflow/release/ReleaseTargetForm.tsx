import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import { sentence } from '../../locales/format';
import { RELEASE_ACTIONS, type FoundReleases, type ReleaseAction, type ReleaseTargetChoice } from './releaseTarget';

/**
 * **The release target's form**: the release manager's holder picks, from what the plugin found, a release definition (with the
 * pipeline whose builds it takes and the branches its trigger takes), one of its stages and the action, says why, and sets it. It never
 * chooses: nothing is picked until the holder picks it. The press is the one door's (`SET_WORKFLOW_VALUES`, twin `daoris workflow
 * values`); a refusal is said where it is pressed.
 *
 * A molecule: what was found and the save arrive as props.
 */
export function ReleaseTargetForm({ found, onSave }: {
  found: FoundReleases;
  onSave: (choice: ReleaseTargetChoice, why: string) => Promise<{ done: boolean; message?: string | null }>;
}) {
  const { t } = useTranslation();
  const options = found.pipelines.flatMap((pipeline) => pipeline.definitions.map((definition) => ({ pipeline: pipeline.id, definition })));
  const [chosen, setChosen] = useState('');
  const [stage, setStage] = useState('');
  const [action, setAction] = useState<ReleaseAction | ''>('');
  const [why, setWhy] = useState('');
  const [said, setSaid] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  if (options.length === 0) return <p className="m-0 text-small text-ink-soft">{t('workflow.release.target.none')}</p>;
  const picked = options.find((each) => `${each.pipeline}:${each.definition.id}` === chosen) ?? null;
  const ready = picked !== null && stage !== '' && action !== '' && why.trim().length > 0 && !sending;
  const field = 'min-h-[1.9rem] min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body';

  const save = () => {
    if (!picked || action === '') return;
    setSending(true);
    setSaid(null);
    onSave({ pipeline: picked.pipeline, releaseDefinition: picked.definition.id, stage: Number(stage), action }, why.trim())
      .then((answer) => setSaid(answer.done ? t('workflow.release.target.set') : answer.message ?? ''))
      .catch((error: unknown) => setSaid(sentence(error)))
      .finally(() => setSending(false));
  };

  return (
    <div className="grid min-w-0 gap-1.5" role="group" aria-label={t('workflow.release.target.title')}>
      <p className="m-0 text-small text-ink">{t('workflow.release.target.title')}</p>
      <select aria-label={t('workflow.release.target.definition')} value={chosen} className={field}
        onChange={(event) => { setChosen(event.target.value); setStage(''); }}>
        <option value="">{t('workflow.release.target.pick')}</option>
        {options.map(({ pipeline, definition }) => (
          <option key={`${pipeline}:${definition.id}`} value={`${pipeline}:${definition.id}`}>
            {t('workflow.release.target.definitionOption', { id: definition.id, name: definition.name, pipeline,
              triggers: definition.triggers.length > 0 ? definition.triggers.join(', ') : t('workflow.release.target.noTrigger') })}
          </option>
        ))}
      </select>
      <select aria-label={t('workflow.release.target.stage')} value={stage} className={field} disabled={!picked} onChange={(event) => setStage(event.target.value)}>
        <option value="">{t('workflow.release.target.pick')}</option>
        {(picked?.definition.stages ?? []).map((each) => (
          <option key={each.id} value={String(each.id)}>{t('workflow.release.target.stageOption', { id: each.id, name: each.name })}</option>
        ))}
      </select>
      <select aria-label={t('workflow.release.target.action')} value={action} className={field} onChange={(event) => setAction(event.target.value as ReleaseAction | '')}>
        <option value="">{t('workflow.release.target.pick')}</option>
        {RELEASE_ACTIONS.map((each) => <option key={each} value={each}>{t(`workflow.release.target.actions.${each}`)}</option>)}
      </select>
      <input aria-label={t('workflow.release.target.why')} placeholder={t('workflow.release.target.why')} value={why} className={field}
        onChange={(event) => setWhy(event.target.value)} />
      <div className="flex flex-wrap items-center gap-1.5">
        <Button variant="ghost" disabled={!ready} className="min-h-0 px-1.5 py-0.5 text-small" onClick={save}>{t('workflow.release.target.save')}</Button>
        {said && <span role="status" className="text-small text-ink-soft">{said}</span>}
      </div>
    </div>
  );
}
