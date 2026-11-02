import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, ChoiceRow, Inline, Pill, SkeletonRows } from '../../design/ui';
import { Refused } from '../../work/InlineConfirm';
import { type ConvertApplied, type ConvertChoices, type ConvertPlanned, type ConvertUndoPlan, savesByShape, terminalOf } from './convertPlan';
import { Section } from './PlaceReview';

export type ConvertReviewProps = {
  /** The last plan read; it stays while the next is read. */
  plan: ConvertPlanned | null;
  choices: ConvertChoices;
  reading: boolean;
  stale: boolean;
  problem: string | null;
  applying: boolean;
  undoing: boolean;
  /** What the last Apply came to. */
  result: ConvertApplied | null;
  /** The undo's plan, where the driver offers it. */
  undo: ConvertUndoPlan | null;
  /** What the last Undo came to. */
  undone: ConvertApplied | null;
  onChoose: (patch: Partial<ConvertChoices>) => void;
  onApply: () => void;
  onUndo: () => void;
  onClose: () => void;
};

const NEVER = ['runs', 'records', 'files', 'places'] as const;

/** What a press came to: the driver's sentence, and each operation with its outcome. */
function Outcome({ result }: { result: ConvertApplied }) {
  const { t } = useTranslation();
  if (!result.done) return <Refused sentence={result.message} />;
  return (
    <div role="status" className="grid min-w-0 gap-1.5 border-l-[3px] border-accent pl-2.5 text-small text-ink">
      <p className="m-0 [overflow-wrap:anywhere]"><Inline text={result.message} /></p>
      <ul aria-label={t('agents.convert.operations')} className="m-0 grid list-none gap-0.5 p-0 text-ink-soft">
        {result.operations.map((each) => (
          <li key={each.id} className="m-0 [overflow-wrap:anywhere]">
            <span><Inline text={`${each.said} — ${t(`agents.convert.outcome.${each.outcome}`, { defaultValue: each.outcome })}${each.note ? `: ${each.note}` : ''}`} /></span>
          </li>
        ))}
      </ul>
    </div>
  );
}

/**
 * **Convert…**'s review (WORKFLOWCLEAN1g-i; the cleanup design §4.3): what converting changes, by shape, each operation once with its places; the
 * row's two choices; what lowers your part; what stays as it is; what it never touches; the terminal line; and one *Apply*. A done press says
 * each operation with its outcome, and *Undo the conversion* while the driver offers it.
 *
 * @remarks
 * The page judges nothing: every operation, lowering, refusal and `moved` is the driver's sentence. While a plan is read the last stays, marked
 * *updating*, and *Apply* waits. A molecule: the plan and the last results arrive as props, and every press goes out.
 */
export function ConvertReview({ plan, choices, reading, stale, problem, applying, undoing, result, undo, undone, onChoose, onApply, onUndo, onClose }: ConvertReviewProps) {
  const { t } = useTranslation();
  const own = useId();
  const updating = reading || stale;
  const busy = updating || applying || undoing;
  const canApply = plan !== null && !plan.empty && !busy;
  const pick = (name: keyof ConvertChoices, value: string, title: string, line: string) => (
    <ChoiceRow
      key={`${name}-${value}`}
      name={`${own}-${name}`}
      value={value}
      checked={choices[name] === value}
      onChange={() => onChoose({ [name]: value })}
      labelledBy={`${own}-${name}-${value}`}
      describedBy={`${own}-${name}-${value}-d`}
    >
      <span className="grid min-w-0 gap-0.5">
        <span id={`${own}-${name}-${value}`} className="text-body text-ink">{title}</span>
        <span id={`${own}-${name}-${value}-d`} className="text-small text-ink-soft">{line}</span>
      </span>
    </ChoiceRow>
  );
  const group = (name: string, label: string, lines: { key: string; text: string; places?: string[] }[]) => lines.length > 0 && (
    <li key={name} className="m-0 grid gap-0.5">
      <span className="text-meta font-medium text-ink-soft">{label}</span>
      <ul className="m-0 grid list-none gap-0.5 p-0">
        {lines.map((each) => (
          <li key={each.key} className="m-0 text-small text-ink [overflow-wrap:anywhere]">
            <Inline text={each.text} />
            {each.places && each.places.length > 0 && <span className="text-ink-soft">{` (${each.places.join(', ')})`}</span>}
          </li>
        ))}
      </ul>
    </li>
  );

  return (
    <section aria-labelledby={`${own}-title`} aria-busy={busy || undefined} className="grid min-w-0 gap-4">
      <div className="flex min-w-0 flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
        <div className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1">
          <h2 id={`${own}-title`} className="m-0 text-body font-semibold text-ink">{t('agents.convert.title')}</h2>
          {updating && <Pill tone="neutral">{t('agents.convert.updating')}</Pill>}
        </div>
        <Button variant="ghost" className="min-h-0 px-2 py-0.5 text-small" onClick={onClose}>{t('agents.convert.close')}</Button>
      </div>
      <p className="m-0 text-small text-ink-soft">{t('agents.convert.lead')}</p>

      {problem && <Refused sentence={problem} />}
      {plan === null
        ? (problem ? null : <div className="grid gap-1"><p className="m-0 text-small text-ink-soft">{t('agents.convert.reading')}</p><SkeletonRows rows={3} /></div>)
        : (
          <div className="grid min-w-0 gap-4 @4xl/main:grid-cols-2">
            <div className="grid min-w-0 content-start gap-4">
              <Section id={`${own}-changes`} title={t('agents.convert.changes')}>
                {plan.empty
                  ? <p className="m-0 text-small text-ink-soft">{t('agents.convert.nothing')}</p>
                  : (
                    <ul aria-label={t('agents.convert.changes')} className="m-0 grid list-none gap-2 p-0">
                      {savesByShape(plan).map(({ shape, saves }) => group(`shape-${shape}`, t(`agents.convert.shape.${shape}`, { defaultValue: shape }), saves.map((each) => ({
                        key: each.id,
                        text: `${each.mode === 'append' ? t('agents.convert.save.append', { id: each.id, version: (each.base ?? 0) + 1 }) : t('agents.convert.save.new', { id: each.id })}: ${each.said}`,
                        places: each.places,
                      }))))}
                      {group('uses', t('agents.convert.group.uses'), plan.uses.map((each) => ({ key: `${each.isWorkspace}${each.place}`, text: each.said })))}
                      {plan.defaultWorkflow && group('default', t('agents.convert.group.default'), [{ key: 'default', text: t('agents.convert.default', { workflow: plan.defaultWorkflow }) }])}
                      {group('policies', t('agents.convert.group.policies'), plan.policies.map((each) => ({ key: `${each.isWorkspace}${each.place}`, text: t('agents.convert.policy', { place: each.place }) })))}
                      {group('removals', t('agents.convert.group.removals'), plan.removals.map((each) => ({ key: `${each.isWorkspace}${each.place}`, text: t('agents.convert.removal', { place: each.place, keys: each.keys.join(', ') }) })))}
                    </ul>
                  )}
              </Section>

              <Section id={`${own}-choices`} title={t('agents.convert.choices')}>
                <div role="radiogroup" aria-label={t('agents.convert.composed')} className="grid min-w-0 gap-1.5">
                  <p className="m-0 text-small text-ink-soft">{t('agents.convert.composed')}</p>
                  {pick('composed', 'none', t('agents.convert.composed.none'), t('agents.convert.composed.noneLine'))}
                  {pick('composed', 'review', t('agents.convert.composed.review'), t('agents.convert.composed.reviewLine'))}
                </div>
                <div role="radiogroup" aria-label={t('agents.convert.automatic')} className="grid min-w-0 gap-1.5">
                  <p className="m-0 text-small text-ink-soft">{t('agents.convert.automatic')}</p>
                  {pick('landAutomatic', 'review', t('agents.convert.automatic.review'), t('agents.convert.automatic.reviewLine'))}
                  {pick('landAutomatic', 'none', t('agents.convert.automatic.none'), t('agents.convert.automatic.noneLine'))}
                </div>
              </Section>

              {plan.lowers.length > 0 && (
                <Section id={`${own}-yours`} title={t('agents.convert.yours')}>
                  {plan.lowers.map((each) => (
                    <p key={each} className="m-0 border-l-[3px] border-warn pl-2.5 text-small text-ink [overflow-wrap:anywhere]"><Inline text={each} /></p>
                  ))}
                </Section>
              )}
            </div>

            <div className="grid min-w-0 content-start gap-4">
              <Section id={`${own}-stays`} title={t('agents.convert.stays')}>
                {plan.census.runs.length > 0 && (
                  <>
                    <p className="m-0 text-small text-ink [overflow-wrap:anywhere]">{t('agents.convert.runs', { count: plan.census.runs.length })}</p>
                    <ul aria-label={t('agents.convert.runsList')} className="m-0 grid list-none gap-0.5 p-0 text-small text-ink-soft">
                      {plan.census.runs.map((each) => (
                        <li key={each.run} className="m-0 [overflow-wrap:anywhere]">
                          <Inline text={t('agents.convert.run', { run: each.run, repository: each.repository, workflow: each.workflow, version: each.version ?? '' })} />
                          {` ${each.door}`}
                        </li>
                      ))}
                    </ul>
                  </>
                )}
                {plan.census.leftovers.map((each) => (
                  <p key={`${each.isWorkspace}${each.place}`} className="m-0 text-small text-ink [overflow-wrap:anywhere]">
                    <Inline text={t('agents.convert.leftover', { place: each.place, keys: each.keys.join(', ') })} /> <Inline text={each.why} />
                  </p>
                ))}
                {plan.kept.map((each) => (
                  <p key={`${each.code}${each.subject}`} className="m-0 text-small text-ink [overflow-wrap:anywhere]"><Inline text={each.said} /></p>
                ))}
                {plan.census.runs.length === 0 && plan.census.leftovers.length === 0 && plan.kept.length === 0 && (
                  <p className="m-0 text-small text-ink-soft">{t('agents.convert.staysNone')}</p>
                )}
              </Section>

              <Section id={`${own}-never`} title={t('agents.convert.never')}>
                <ul className="m-0 grid list-none gap-0.5 p-0 text-small text-ink-soft">
                  {NEVER.map((each) => <li key={each} className="m-0 [overflow-wrap:anywhere]">{t(`agents.convert.never.${each}`)}</li>)}
                </ul>
              </Section>
            </div>
          </div>
        )}

      {plan !== null && (
        <details className="m-0 min-w-0 text-meta text-ink-faint">
          <summary className="cursor-pointer">{t('agents.convert.terminal')}</summary>
          {terminalOf(plan).map((line) => <code key={line} className="mt-1 block font-mono [overflow-wrap:anywhere]">{line}</code>)}
        </details>
      )}

      {result && <Outcome result={result} />}
      {undone && <Outcome result={undone} />}

      <div className="flex flex-wrap items-center gap-2">
        <Button variant="primary" disabled={!canApply} onClick={onApply} data-review-apply="">
          {applying ? t('agents.convert.applying') : t('agents.convert.apply')}
        </Button>
      </div>

      {undo && (
        <Section id={`${own}-undo`} title={t('agents.convert.undo')}>
          <p className="m-0 text-small text-ink-soft">{t('agents.convert.undoLead')}</p>
          <ul className="m-0 grid list-none gap-0.5 p-0 text-small text-ink">
            {undo.lines.map((each) => <li key={each} className="m-0 [overflow-wrap:anywhere]"><Inline text={each} /></li>)}
          </ul>
          <div>
            <Button disabled={undoing || applying} onClick={onUndo}>{undoing ? t('agents.convert.undoing') : t('agents.convert.undo')}</Button>
          </div>
        </Section>
      )}
    </section>
  );
}
