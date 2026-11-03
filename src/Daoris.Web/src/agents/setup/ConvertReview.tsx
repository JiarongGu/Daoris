import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, ChoiceRow, Inline, Pill, SkeletonRows } from '../../design/ui';
import { dayOf, size } from '../../locales/format';
import { Refused } from '../../work/InlineConfirm';
import {
  clearsOf, type ConvertApplied, type ConvertChoices, type ConvertPlanned, type ConvertUndoPlan, otherParts, savesByShape, terminalOf, workflowsOf,
} from './convertPlan';
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
  /** What the last Apply, press again or Leave it came to. */
  result: ConvertApplied | null;
  /** The undo's plan, where the driver offers it. */
  undo: ConvertUndoPlan | null;
  /** What the last Undo came to. */
  undone: ConvertApplied | null;
  onChoose: (patch: Partial<ConvertChoices>) => void;
  onApply: () => void;
  /** The same receipt pressed again: each failed part retried. */
  onAgain: () => void;
  /** A failed part of the receipt left for a later plan. */
  onLeave: (part: string) => void;
  onUndo: () => void;
  onClose: () => void;
};

const NEVER = ['runs', 'records', 'files', 'places'] as const;

/** A part's name in the reader's words; one this page does not know reads as the driver names it. */
const usePartName = () => {
  const { t } = useTranslation();
  return (part: string) => t(`agents.convert.part.${part}`, { defaultValue: part });
};

/**
 * What a press came to: the driver's sentence, and each operation with its outcome. A receipt with a failed part says each part's outcome, offers
 * the receipt pressed again, and *Leave it* on each failed part.
 */
function Outcome({ result, busy, onAgain, onLeave }: { result: ConvertApplied; busy: boolean; onAgain: () => void; onLeave: (part: string) => void }) {
  const { t } = useTranslation();
  const partName = usePartName();
  const parts = result.parts ?? [];
  const failed = parts.filter((each) => each.outcome === 'failed');
  if (!result.done && (result.refused || parts.length === 0)) return <Refused sentence={result.message} />;
  return (
    <div role="status" className={`grid min-w-0 gap-1.5 border-l-[3px] ${result.done ? 'border-accent' : 'border-warn'} pl-2.5 text-small text-ink`}>
      <p className="m-0 [overflow-wrap:anywhere]"><Inline text={result.message} /></p>
      <ul aria-label={t('agents.convert.operations')} className="m-0 grid list-none gap-0.5 p-0 text-ink-soft">
        {result.operations.map((each) => (
          <li key={each.id} className="m-0 [overflow-wrap:anywhere]">
            <span><Inline text={`${each.said} — ${t(`agents.convert.outcome.${each.outcome}`, { defaultValue: each.outcome })}${each.note ? `: ${each.note}` : ''}`} /></span>
          </li>
        ))}
      </ul>
      {parts.length > 1 || failed.length > 0 ? (
        <ul aria-label={t('agents.convert.parts')} className="m-0 grid list-none gap-1 p-0">
          {parts.map((each) => (
            <li key={each.part} className="m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
              <span className="[overflow-wrap:anywhere]">
                <Inline text={`${partName(each.part)} — ${t(`agents.convert.outcome.${each.outcome}`, { defaultValue: each.outcome })}${each.said ? `: ${each.said}` : ''}`} />
              </span>
              {each.outcome === 'failed' && (
                <Button variant="ghost" className="min-h-0 px-2 py-0.5 text-small" disabled={busy} aria-label={t('agents.convert.leaveOf', { part: partName(each.part) })} onClick={() => onLeave(each.part)}>
                  {t('agents.convert.leave')}
                </Button>
              )}
            </li>
          ))}
        </ul>
      ) : null}
      {failed.length > 0 && (
        <div>
          <Button disabled={busy} onClick={onAgain}>{t('agents.convert.again')}</Button>
        </div>
      )}
    </div>
  );
}

/**
 * **Convert…**'s review (WORKFLOWCLEAN1g-i, ONEWAY1ae; the oneway design §1.1, the cleanup design §4.3): what converting changes, by shape for the
 * workflow part and by part for every other, each operation once with its places, and the parts not read; the row's two choices; what lowers your
 * part; what stays as it is; what it never touches; what is removed for good; the terminal line; and one *Apply*. A done press says each operation
 * with its outcome; a receipt with a failed part offers it pressed again or left; *Undo the conversion* while the driver offers it.
 *
 * @remarks
 * The page judges nothing: every operation, lowering, refusal and `moved` is the driver's sentence. While a plan is read the last stays, marked
 * *updating*, and *Apply* waits. At 64 rem of main the first five sections stand in two columns, and in one below. A molecule: the plan and the
 * last results arrive as props, and every press goes out.
 */
export function ConvertReview({ plan, choices, reading, stale, problem, applying, undoing, result, undo, undone, onChoose, onApply, onAgain, onLeave, onUndo, onClose }: ConvertReviewProps) {
  const { t, i18n } = useTranslation();
  const partName = usePartName();
  const own = useId();
  const updating = reading || stale;
  const busy = updating || applying || undoing;
  const canApply = plan !== null && !plan.empty && !busy;
  const workflows = workflowsOf(plan);
  const others = plan ? otherParts(plan) : [];
  const clears = plan ? clearsOf(plan) : [];
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
                      {workflows && savesByShape(workflows).map(({ shape, saves }) => group(`shape-${shape}`, t(`agents.convert.shape.${shape}`, { defaultValue: shape }), saves.map((each) => ({
                        key: each.id,
                        text: `${each.mode === 'append' ? t('agents.convert.save.append', { id: each.id, version: (each.base ?? 0) + 1 }) : t('agents.convert.save.new', { id: each.id })}: ${each.said}`,
                        places: each.places,
                      }))))}
                      {workflows && group('uses', t('agents.convert.group.uses'), workflows.uses.map((each) => ({ key: `${each.isWorkspace}${each.place}`, text: each.said })))}
                      {workflows?.defaultWorkflow && group('default', t('agents.convert.group.default'), [{ key: 'default', text: t('agents.convert.default', { workflow: workflows.defaultWorkflow }) }])}
                      {workflows && group('policies', t('agents.convert.group.policies'), workflows.policies.map((each) => ({ key: `${each.isWorkspace}${each.place}`, text: t('agents.convert.policy', { place: each.place }) })))}
                      {workflows && group('removals', t('agents.convert.group.removals'), workflows.removals.map((each) => ({ key: `${each.isWorkspace}${each.place}`, text: t('agents.convert.removal', { place: each.place, keys: each.keys.join(', ') }) })))}
                      {/* The terminal's lines align their columns with runs of spaces, which the page reads as one. */}
                      {others.map((part) => group(`part-${part.part}`, partName(part.part), part.lines.map((line, at) => ({ key: `${at}`, text: line.trim().replace(/ {2,}/g, ' ') }))))}
                    </ul>
                  )}
                {plan.unread.map((each) => (
                  <p key={each.part} className="m-0 border-l-[3px] border-warn pl-2.5 text-small text-ink [overflow-wrap:anywhere]">
                    {t('agents.convert.unread', { part: partName(each.part) })} <Inline text={each.said} />
                  </p>
                ))}
              </Section>

              {workflows && (
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
              )}

              {workflows && workflows.lowers.length > 0 && (
                <Section id={`${own}-yours`} title={t('agents.convert.yours')}>
                  {workflows.lowers.map((each) => (
                    <p key={each} className="m-0 border-l-[3px] border-warn pl-2.5 text-small text-ink [overflow-wrap:anywhere]"><Inline text={each} /></p>
                  ))}
                </Section>
              )}
            </div>

            <div className="grid min-w-0 content-start gap-4">
              <Section id={`${own}-stays`} title={t('agents.convert.stays')}>
                {workflows && workflows.census.runs.length > 0 && (
                  <>
                    <p className="m-0 text-small text-ink [overflow-wrap:anywhere]">{t('agents.convert.runs', { count: workflows.census.runs.length })}</p>
                    <ul aria-label={t('agents.convert.runsList')} className="m-0 grid list-none gap-0.5 p-0 text-small text-ink-soft">
                      {workflows.census.runs.map((each) => (
                        <li key={each.run} className="m-0 [overflow-wrap:anywhere]">
                          <Inline text={t('agents.convert.run', { run: each.run, repository: each.repository, workflow: each.workflow, version: each.version ?? '' })} />
                          {` ${each.door}`}
                        </li>
                      ))}
                    </ul>
                  </>
                )}
                {workflows?.census.leftovers.map((each) => (
                  <p key={`${each.isWorkspace}${each.place}`} className="m-0 text-small text-ink [overflow-wrap:anywhere]">
                    <Inline text={t('agents.convert.leftover', { place: each.place, keys: each.keys.join(', ') })} /> <Inline text={each.why} />
                  </p>
                ))}
                {workflows?.kept.map((each) => (
                  <p key={`${each.code}${each.subject}`} className="m-0 text-small text-ink [overflow-wrap:anywhere]"><Inline text={each.said} /></p>
                ))}
                {(workflows?.census.runs.length ?? 0) === 0 && (workflows?.census.leftovers.length ?? 0) === 0 && (workflows?.kept.length ?? 0) === 0 && (
                  <p className="m-0 text-small text-ink-soft">{t('agents.convert.staysNone')}</p>
                )}
              </Section>

              <Section id={`${own}-never`} title={t('agents.convert.never')}>
                <ul className="m-0 grid list-none gap-0.5 p-0 text-small text-ink-soft">
                  {NEVER.map((each) => <li key={each} className="m-0 [overflow-wrap:anywhere]">{t(`agents.convert.never.${each}`)}</li>)}
                </ul>
              </Section>

              {clears.length > 0 && (
                <Section id={`${own}-removed`} title={t('agents.convert.removed')}>
                  <p className="m-0 text-small text-ink-soft">{t('agents.convert.removedLead')}</p>
                  <ul aria-label={t('agents.convert.removed')} className="m-0 grid list-none gap-0.5 p-0 text-small text-ink">
                    {clears.map((each) => (
                      <li key={`${each.part}${each.said}`} className="m-0 [overflow-wrap:anywhere]">
                        <Inline text={each.said} />
                        {` — ${[
                          t('agents.convert.clearCount', { count: each.count }),
                          ...(each.from && each.to ? [t('agents.convert.clearSpan', { from: dayOf(each.from, i18n.language), to: dayOf(each.to, i18n.language) })] : []),
                          ...(each.bytes != null ? [size(each.bytes)] : []),
                        ].join(', ')}`}
                      </li>
                    ))}
                  </ul>
                </Section>
              )}
            </div>
          </div>
        )}

      {plan !== null && (
        <details className="m-0 min-w-0 text-meta text-ink-faint">
          <summary className="cursor-pointer">{t('agents.convert.terminal')}</summary>
          {terminalOf(plan).map((line) => <code key={line} className="mt-1 block font-mono [overflow-wrap:anywhere]">{line}</code>)}
        </details>
      )}

      {result && <Outcome result={result} busy={applying || undoing} onAgain={onAgain} onLeave={onLeave} />}
      {undone && <Outcome result={undone} busy={applying || undoing} onAgain={onAgain} onLeave={onLeave} />}

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
