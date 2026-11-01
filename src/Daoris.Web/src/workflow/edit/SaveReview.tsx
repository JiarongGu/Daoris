import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, CheckField, Inline, Prose } from '../../design/ui';
import type { WorkflowSaveAnswer, WorkflowScopeRow, WorkflowUseAnswer } from './answers';

// The save (WORKFLOWEDIT1b, FLOWCHART2b): a new workflow named, its id made from the name and shown only under *Details* (D221 §2.3: no
// id is asked for), or a version appended against the one read, with every place the append reaches named for the person to let follow
// it; then *Use here*, its own press. SETUPUX1s makes it the reviewed save of D221; this row keeps today's presses.

export type SaveTarget = { id: string; name: string; base: number | null };

export function SaveReview({ target, id, onName, scopes, authorized, onAuthorize, empty, saving, using, said, onSave, onUse }: {
  target: SaveTarget;
  /** The id a new workflow is saved under, made from its name. */
  id: string;
  onName: (name: string) => void;
  scopes: WorkflowScopeRow[];
  authorized: boolean;
  onAuthorize: (authorized: boolean) => void;
  empty: boolean;
  saving: boolean;
  using: boolean;
  said: { saved: WorkflowSaveAnswer | null; used: WorkflowUseAnswer | null; failed: string | null };
  onSave: () => void;
  onUse: () => void;
}) {
  const { t } = useTranslation();
  const nameId = useId();
  return (
    <div className="grid min-w-0 gap-2 border-t border-line pt-3">
      <h4 className="m-0 text-small font-semibold text-ink">{t('workflow.edit.save.title')}</h4>
      {target.base === null ? (
        <div className="grid min-w-0 gap-1">
          <label htmlFor={nameId} className="text-meta text-ink-soft">{t('workflow.edit.save.name')}</label>
          <input
            id={nameId}
            className="min-h-[1.9rem] min-w-0 max-w-[32rem] rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
            value={target.name}
            onChange={(event) => onName(event.target.value)}
          />
          <details className="text-meta text-ink-faint">
            <summary className="cursor-pointer">{t('workflow.edit.inspector.details')}</summary>
            <Inline text={t('workflow.edit.save.idSays', { id })} />
          </details>
        </div>
      ) : (
        <Prose className="text-small">{t('workflow.edit.save.appendSays', { version: target.base + 1, id: target.id })}</Prose>
      )}
      {scopes.length > 0 && (
        <div className="grid min-w-0 gap-1">
          <p className="m-0 text-small text-ink">{t('workflow.edit.save.reach')}</p>
          <ul className="m-0 grid list-disc gap-0.5 pl-5 text-small text-ink-soft">
            {scopes.map((scope) => {
              const place = t(`workflow.edit.save.place.${scope.level}`, { name: scope.name });
              return <li key={scope.key}>{scope.kind ? t('workflow.edit.save.place.kind', { place, kind: scope.kind }) : place}</li>;
            })}
          </ul>
          <CheckField checked={authorized} onChange={onAuthorize} label={t('workflow.edit.save.authorize')} />
        </div>
      )}
      <div className="flex min-w-0 flex-wrap items-center gap-2">
        <Button disabled={saving || empty} onClick={onSave}>
          {saving ? t('workflow.edit.save.saving')
            : target.base === null ? t('workflow.edit.save.new') : t('workflow.edit.save.append', { version: target.base + 1 })}
        </Button>
        {target.base !== null && (
          <Button variant="ghost" className="border border-line" disabled={using} onClick={onUse}>
            {using ? t('workflow.edit.using') : t('workflow.edit.use')}
          </Button>
        )}
      </div>
      {said.saved?.message && <Prose className="text-small"><Inline text={said.saved.message} /></Prose>}
      {said.used?.message && <Prose className="text-small"><Inline text={said.used.message} /></Prose>}
      {said.failed && <Prose className="text-small"><Inline text={said.failed} /></Prose>}
    </div>
  );
}
