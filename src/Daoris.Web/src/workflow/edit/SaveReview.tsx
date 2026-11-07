import { useEffect, useId, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, CheckField, Inline, Prose } from '../../design/ui';
import { ChangePlan, type PlanState } from '../place/ChangePlan';
import type { WorkflowSaveAnswer, WorkflowScopeRow, WorkflowUseAnswer } from './answers';

// The save and the choice: a new workflow named, its id made from the name and shown
// only under *Details* (no id is asked for), or a version appended against the one read. *Review and save* opens once this draft's
// judgement holds and says what the save changes and every place an append reaches, which the person lets follow it by one consent bound
// to exactly those places; then the save. *Use here* is a press of its own, offered only on a draft that is the version it is saved as,
// and reads the plan of the choice where the driver announces one.

export type SaveTarget = { id: string; name: string; base: number | null };

/** What the review says, already in words: the steps changed, the part of the person, and what the driver said of the repositories reached. */
export type ReviewFacts = {
  /** The steps of a workflow that is new, none for an append. */
  fresh: number | null;
  added: string[];
  removed: string[];
  changed: string[];
  ways: number;
  part: string[];
  reach: string[];
};

/** The choice, as the editor offers it: not at all, after the changes are saved, or as a press (two where the driver announces a plan). */
export type UseOffer =
  | { is: 'none' }
  | { is: 'unsaved' }
  | { is: 'press'; announced: false }
  | { is: 'press'; announced: true; reading: boolean; plan: PlanState | null; ready: boolean };

export function SaveReview({
  target, id, onName, empty, holds, review, onReview, scopes, consented, onConsent, saving, using, said, onSave, onReread, use, onPlan, onUse,
}: {
  target: SaveTarget;
  /** The id a new workflow is saved under, made from its name. */
  id: string;
  onName: (name: string) => void;
  empty: boolean;
  /** This very draft is judged, and holds. */
  holds: boolean;
  /** The review, open; null while it is not. */
  review: ReviewFacts | null;
  onReview: (open: boolean) => void;
  scopes: WorkflowScopeRow[];
  /** Consent is given to exactly these places. */
  consented: boolean;
  onConsent: (consented: boolean) => void;
  saving: boolean;
  using: boolean;
  said: { saved: WorkflowSaveAnswer | null; used: WorkflowUseAnswer | null; failed: string | null };
  onSave: () => void;
  /** Reads the newest version again after a refusal that the base moved. */
  onReread: () => void;
  use: UseOffer;
  /** *Use here…*: reads the plan of the choice. */
  onPlan: () => void;
  onUse: () => void;
}) {
  const { t } = useTranslation();
  const nameId = useId();
  const reviewId = useId();
  const region = useRef<HTMLElement>(null);
  const opener = useRef<HTMLDivElement>(null);
  const was = useRef(false);
  const open = review !== null;
  // The review takes the focus when it opens, and the button it opened from gets it back when it is given up.
  useEffect(() => {
    if (open) region.current?.focus();
    else if (was.current) opener.current?.querySelector('button')?.focus();
    was.current = open;
  }, [open]);

  const append = target.base !== null;
  const waits = scopes.length > 0 && !consented;
  const stale = said.saved?.refused === 'stale';
  return (
    <div className="grid min-w-0 gap-2 border-t border-line pt-3">
      <h4 className="m-0 text-small font-semibold text-ink">{t('workflow.edit.save.title')}</h4>
      {!append ? (
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
        <Prose className="text-small">{t('workflow.edit.save.appendSays', { version: target.base! + 1, id: target.id })}</Prose>
      )}

      {!open && (
        <div className="grid min-w-0 gap-1">
          <div ref={opener} className="flex min-w-0 flex-wrap items-center gap-2">
            <Button disabled={saving || empty || !holds} onClick={() => onReview(true)}>{t('workflow.edit.review.open')}</Button>
          </div>
          {!empty && !holds && <p className="m-0 text-meta text-ink-soft">{t('workflow.edit.review.waits')}</p>}
        </div>
      )}

      {open && (
        <section
          ref={region}
          tabIndex={-1}
          aria-labelledby={reviewId}
          className="grid min-w-0 gap-2 rounded-card border border-line bg-raised p-3 outline-none"
        >
          <h5 id={reviewId} className="m-0 text-small font-semibold text-ink">{t('workflow.edit.review.title')}</h5>
          <ul className="m-0 grid list-none gap-0.5 p-0 text-small text-ink-soft">
            {review.fresh !== null && <li>{t('workflow.edit.review.fresh', { count: review.fresh })}</li>}
            {review.fresh === null && review.added.length + review.removed.length + review.changed.length + review.ways === 0 && (
              <li>{t('workflow.edit.review.none')}</li>
            )}
            {review.added.length > 0 && <li className="[overflow-wrap:anywhere]">{t('workflow.edit.review.added', { steps: review.added.join(t('workflow.listJoin')) })}</li>}
            {review.removed.length > 0 && <li className="[overflow-wrap:anywhere]">{t('workflow.edit.review.removed', { steps: review.removed.join(t('workflow.listJoin')) })}</li>}
            {review.changed.length > 0 && <li className="[overflow-wrap:anywhere]">{t('workflow.edit.review.changed', { steps: review.changed.join(t('workflow.listJoin')) })}</li>}
            {review.fresh === null && review.ways > 0 && <li>{t('workflow.edit.review.ways', { count: review.ways })}</li>}
            {review.part.length > 0 && <li className="[overflow-wrap:anywhere]">{t('workflow.edit.review.part', { steps: review.part.join(t('workflow.listJoin')) })}</li>}
            {review.reach.map((each) => <li key={each} className="[overflow-wrap:anywhere]"><Inline text={each} /></li>)}
          </ul>
          {scopes.length > 0 && (
            <div className="grid min-w-0 gap-1">
              <p className="m-0 text-small text-ink">{t('workflow.edit.save.reach')}</p>
              <ul className="m-0 grid list-disc gap-0.5 pl-5 text-small text-ink-soft">
                {scopes.map((scope) => {
                  const place = t(`workflow.edit.save.place.${scope.level}`, { name: scope.name });
                  return <li key={scope.key}>{scope.kind ? t('workflow.edit.save.place.kind', { place, kind: scope.kind }) : place}</li>;
                })}
              </ul>
              <CheckField checked={consented} onChange={onConsent} label={t('workflow.edit.save.authorize')} />
            </div>
          )}
          <div className="flex min-w-0 flex-wrap items-center gap-2">
            <Button disabled={saving || empty || !holds || waits} onClick={onSave}>
              {saving ? t('workflow.edit.save.saving') : append ? t('workflow.edit.save.append', { version: target.base! + 1 }) : t('workflow.edit.save.new')}
            </Button>
            <Button variant="ghost" className="border border-line" disabled={saving} onClick={() => onReview(false)}>{t('workflow.edit.review.back')}</Button>
          </div>
        </section>
      )}

      {said.saved?.message && <Prose className="text-small"><Inline text={said.saved.message} /></Prose>}
      {stale && (
        <div className="flex min-w-0 flex-wrap items-center gap-2">
          <Button variant="ghost" className="border border-line" onClick={onReread}>{t('workflow.edit.save.again')}</Button>
        </div>
      )}

      {use.is === 'unsaved' && <p className="m-0 text-small text-ink-soft">{t('workflow.edit.use.unsaved')}</p>}
      {use.is === 'press' && (
        <div className="grid min-w-0 gap-2">
          {use.announced && use.reading && use.plan && <ChangePlan plan={use.plan} />}
          <div className="flex min-w-0 flex-wrap items-center gap-2">
            {use.announced && !use.reading
              ? <Button variant="ghost" className="border border-line" disabled={using} onClick={onPlan}>{t('workflow.edit.use.plan')}</Button>
              : (
                <Button variant="ghost" className="border border-line" disabled={using || (use.announced && !use.ready)} onClick={onUse}>
                  {using ? t('workflow.edit.using') : t('workflow.edit.use')}
                </Button>
              )}
          </div>
        </div>
      )}
      {said.used?.message && <Prose className="text-small"><Inline text={said.used.message} /></Prose>}
      {said.failed && <Prose className="text-small"><Inline text={said.failed} /></Prose>}
    </div>
  );
}
