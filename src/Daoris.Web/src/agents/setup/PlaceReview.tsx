import { type ReactNode, useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline, Pill, SkeletonRows } from '../../design/ui';
import { Refused } from '../../work/InlineConfirm';
import { Acceptances } from '../../workflow/place/Acceptances';
import {
  acceptOf, drivingOf, type DrivingState, offered, PARTS, type PlacePart, type PlacePlanned, type PlaceRefusal, type PlaceTick, type SetUpDraft,
} from './placePlan';

const short = (id: string) => id.slice(0, 7);

/** What holding a repository's queued work came to: held, or the driver's sentence for why not. */
export type HoldOutcome = { held: true } | { held: false; said: string };

export type PlaceReviewProps = {
  place: string;
  kind: 'repository' | 'workspace';
  /** The last plan read; it stays while the next is read. */
  plan: PlacePlanned | null;
  draft: SetUpDraft;
  reading: boolean;
  stale: boolean;
  problem: string | null;
  applying: boolean;
  holding: boolean;
  holds: Record<string, HoldOutcome>;
  /** Back to the choices, at a part when a refusal's link names one. */
  onBack: (part?: PlacePart) => void;
  /** A change of selection for a workspace: the plan is read again without the repository. */
  onLeaveOut: (repository: string) => void;
  onAccept: (accept: PlaceTick[]) => void;
  onApply: () => void;
  onHold: (repositories: string[]) => void;
  /** The adoption quest a done apply started, by its id. */
  onOpenQuest?: (quest: string) => void;
  onClose: () => void;
};

/** A section of the review, its heading naming it. */
function Section({ id, title, children }: { id: string; title: string; children: ReactNode }) {
  return (
    <section aria-labelledby={id} className="grid min-w-0 content-start gap-1.5">
      <h3 id={id} className="m-0 text-body font-semibold text-ink">{title}</h3>
      {children}
    </section>
  );
}

const ENDED: Record<DrivingState, 'driven' | 'notDriven' | 'adoption'> = {
  driven: 'driven', starts: 'driven', stops: 'notDriven', adoptionOnly: 'adoption', notDriven: 'notDriven',
};

/**
 * **Set up…**'s review (SETUPUX1k; the setup-UX design §2.2): the plan the choices made, in one order. What changes, each identical change once with
 * its repositories, beside the driver's reach sentences and the lines it could not read; what stops it, linked back; what it starts and what it
 * lowers of your part; the work driving releases, held on a press that is outside the apply, each repository's outcome beside it; the acceptances,
 * unticked and bound to the declaration read; the terminal line; and one *Apply*.
 *
 * @remarks
 * The page judges nothing: every refusal, reach, lowering and `moved` is the driver's sentence. While a plan is read the last stays, marked
 * *updating*, and *Apply* waits. A done apply says what it started and where each repository stands, and opens the adoption quest by a door;
 * *configuration applied; adoption pending* is said as the driver says it.
 *
 * A molecule: the draft, the plan and the last result arrive as props, and every press goes out.
 */
export function PlaceReview({
  place, kind, plan, draft, reading, stale, problem, applying, holding, holds, onBack, onLeaveOut, onAccept, onApply, onHold, onOpenQuest, onClose,
}: PlaceReviewProps) {
  const { t } = useTranslation();
  const own = useId();
  const several = (plan?.repositories.length ?? 0) > 1;
  const updating = reading || stale;
  const busy = updating || applying;
  const count = draft.repositories.length;
  const accepts = offered(plan);
  const ticked = acceptOf(draft.accept, plan);
  const nothing = plan !== null && plan.changes.length === 0 && ticked.length === 0;
  const canApply = plan !== null && plan.applies && !nothing && !busy && count > 0 && draft.preset !== null;
  const refused = plan?.refusals ?? [];
  const releasing = plan?.repositories.filter((row) => row.released.length > 0) ?? [];
  const lowers = plan?.repositories.flatMap((row) => row.lowers.map((each) => ({ repository: row.repository, said: each }))) ?? [];
  const result = draft.result;
  const partName = (part: PlacePart) => t(part === 'delivery' ? 'agents.setup.set.lands' : `agents.setup.set.${part}`);
  const heldOf = Object.entries(holds).filter(([repository]) => draft.repositories.includes(repository));
  const tickId = (one: { repository: string; purpose: string; line: string; blob: string }) => `${one.repository}|${one.purpose}|${one.line}|${one.blob}`;
  const toggle = (one: PlaceTick, on: boolean) => onAccept([
    ...draft.accept.filter((was) => !(was.repository === one.repository && was.purpose === one.purpose)),
    ...(on ? [one] : []),
  ]);

  return (
    <section aria-labelledby={`${own}-title`} aria-busy={busy || undefined} className="grid min-w-0 gap-4">
      <div className="flex min-w-0 flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
        <Button variant="ghost" className="min-h-0 px-2 py-0.5 text-small" onClick={() => onBack()}>{t('agents.setup.set.backToChoices')}</Button>
        <Button variant="ghost" className="min-h-0 px-2 py-0.5 text-small" onClick={onClose}>{t('agents.setup.set.close')}</Button>
      </div>
      <div className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1">
        <h2 id={`${own}-title`} className="m-0 text-body font-semibold text-ink [overflow-wrap:anywhere]">{t('agents.setup.set.reviewTitle', { place })}</h2>
        {updating && <Pill tone="neutral">{t('agents.setup.set.updating')}</Pill>}
      </div>

      {problem && <Refused sentence={problem} />}
      {plan === null
        ? (problem ? null : <div className="grid gap-1"><p className="m-0 text-small text-ink-soft">{t('agents.setup.set.reading')}</p><SkeletonRows rows={3} /></div>)
        : (
          <div className="grid min-w-0 gap-4 @4xl/main:grid-cols-2">
            <div className="grid min-w-0 content-start gap-4">
              <Section id={`${own}-changes`} title={t('agents.setup.set.changes')}>
                {plan.changes.length > 0
                  ? (
                    <ul aria-label={t('agents.setup.set.changes')} className="m-0 grid list-none gap-1 p-0">
                      {plan.changes.map(({ change, repositories }) => (
                        <li key={`${change.key}${change.said}${change.before}${change.after}`} className="m-0 text-small text-ink [overflow-wrap:anywhere]">
                          <Inline text={`${change.said}: ${change.before} → ${change.after}`} />
                          {several && <span className="text-ink-soft">{` (${repositories.join(', ')})`}</span>}
                        </li>
                      ))}
                    </ul>
                  )
                  : refused.length === 0 && <p className="m-0 text-small text-ink-soft">{t('agents.setup.set.nothing')}</p>}
                {plan.repositories.flatMap((row) => row.reach.filter((each) => each.said && each.cannots.length === 0).map((each) => (
                  <p key={`${row.repository}${each.said}`} className="m-0 text-small text-ink-soft [overflow-wrap:anywhere]"><Inline text={each.said!} /></p>
                )))}
                {plan.repositories.filter((row) => row.unread).map((row) => (
                  <p key={`unread-${row.repository}`} className="m-0 text-small text-ink-soft [overflow-wrap:anywhere]">
                    {t('agents.setup.set.unread', { repository: row.repository })} <Inline text={row.unread!} />
                  </p>
                ))}
              </Section>

              {refused.length > 0 && (
                <Section id={`${own}-stops`} title={t('agents.setup.set.stops')}>
                  <ul className="m-0 grid list-none gap-2 p-0">
                    {refused.map((each: PlaceRefusal) => (
                      <li key={`${each.repository ?? ''}${each.code}${each.sentence}`} data-refusal={each.code} className="m-0 grid min-w-0 justify-items-start gap-1 border-l-[3px] border-warn pl-2.5 text-small text-ink-soft [overflow-wrap:anywhere]">
                        <span>
                          {each.repository && <span className="mr-1.5 font-mono text-meta text-ink">{each.repository}</span>}
                          <Inline text={each.sentence} />
                        </span>
                        <Button variant="ghost" className="min-h-0 border border-line px-2.5 py-0.5 text-small" onClick={() => onBack(PARTS.find((part) => part === each.part) ?? 'delivery')}>
                          {`↑ ${partName(PARTS.find((part) => part === each.part) ?? 'delivery')}`}
                        </Button>
                      </li>
                    ))}
                  </ul>
                  {kind === 'workspace' && count > 1 && [...new Set(refused.flatMap((each) => (each.repository ? [each.repository] : [])))].map((repository) => (
                    <div key={repository}>
                      <Button variant="ghost" className="min-h-0 border border-line px-2.5 py-0.5 text-small" onClick={() => onLeaveOut(repository)}>
                        {t('agents.setup.set.leaveOut', { repository })}
                      </Button>
                    </div>
                  ))}
                </Section>
              )}
            </div>

            <div className="grid min-w-0 content-start gap-4">
              <Section id={`${own}-starts`} title={t('agents.setup.set.starts')}>
                <p className="m-0 text-small text-ink [overflow-wrap:anywhere]">
                  {plan.asked.adoption
                    ? t('agents.setup.set.starts.adoption', { count: plan.repositories.length, repositories: plan.repositories.map((row) => row.repository).join(', ') })
                    : t('agents.setup.set.starts.adoptionNone')}
                </p>
                <ul aria-label={t('agents.setup.set.driving')} className="m-0 grid list-none gap-0.5 p-0 text-small text-ink-soft">
                  {drivingOf(plan).map((each) => (
                    <li key={each.state} className="m-0 [overflow-wrap:anywhere]">{t(`agents.setup.set.driving.${each.state}`, { repositories: each.repositories.join(', ') })}</li>
                  ))}
                </ul>
              </Section>

              {lowers.length > 0 && (
                <Section id={`${own}-yours`} title={t('agents.setup.set.yours')}>
                  {lowers.map((each) => (
                    <p key={`${each.repository}${each.said}`} className="m-0 border-l-[3px] border-warn pl-2.5 text-small text-ink [overflow-wrap:anywhere]">
                      {several && <span className="font-mono text-meta">{each.repository}: </span>}
                      <Inline text={each.said} />
                    </p>
                  ))}
                </Section>
              )}

              {releasing.length > 0 && (
                <Section id={`${own}-releases`} title={t('agents.setup.set.releases')}>
                  <ul aria-labelledby={`${own}-releases`} className="m-0 grid list-none gap-0.5 p-0">
                    {releasing.flatMap((row) => row.released.map((each) => (
                      <li key={`${row.repository}${each.quest}`} className="m-0 text-small text-ink [overflow-wrap:anywhere]">
                        {each.title}
                        {several && <span className="text-ink-soft">{` — ${row.repository}`}</span>}
                      </li>
                    )))}
                  </ul>
                  <div>
                    <Button
                      variant="ghost"
                      className="min-h-0 border border-line px-2.5 py-0.5 text-small"
                      disabled={holding || busy}
                      onClick={() => onHold(releasing.map((row) => row.repository))}
                    >
                      {t('agents.setup.set.hold')}
                    </Button>
                  </div>
                </Section>
              )}
              {heldOf.length > 0 && (
                <ul aria-label={t('agents.setup.set.holdOutcomes')} className="m-0 grid list-none gap-0.5 p-0 text-small text-ink">
                  {heldOf.map(([repository, outcome]) => (
                    <li key={repository} className="m-0 [overflow-wrap:anywhere]">
                      {outcome.held
                        ? t('agents.setup.set.holdOutcome.held', { repository })
                        : <>{t('agents.setup.set.holdOutcome.failed', { repository })} <Inline text={outcome.said} /></>}
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </div>
        )}

      {plan !== null && plan.repositories.some((row) => row.declaration && row.declaration.acceptances.length > 0) && (
        <Section id={`${own}-acceptances`} title={t('agents.setup.set.acceptances')}>
          {plan.repositories.filter((row) => row.declaration && row.declaration.acceptances.length > 0).map((row) => {
            const declaration = row.declaration!;
            const ofRow = (purpose: string) => ({ repository: row.repository, purpose, line: declaration.line, blob: declaration.blob });
            return (
              <Acceptances
                key={row.repository}
                title={t('agents.setup.set.declarationAt', { repository: row.repository, line: declaration.line, commit: short(declaration.commit), blob: short(declaration.blob) })}
                fold={t('agents.setup.set.readDeclaration')}
                rows={declaration.acceptances.map((each) => {
                  const one = ofRow(each.purpose);
                  const waits = accepts.some((read) => tickId(read) === tickId(one));
                  const consequence = t(`agents.setup.set.consequence.${each.purpose}.${each.standing}`, { defaultValue: '' });
                  return {
                    id: tickId(one),
                    label: t(`agents.setup.set.accept.${each.purpose}`),
                    why: each.said,
                    said: [t(`agents.setup.set.standing.${each.standing}`, { defaultValue: each.standing }), consequence].filter(Boolean).join(' '),
                    offered: waits,
                    text: declaration.text ?? null,
                    textLabel: t('agents.setup.set.declaration', { repository: row.repository, line: declaration.line, commit: short(declaration.commit) }),
                  };
                })}
                ticked={declaration.acceptances.map((each) => ofRow(each.purpose)).filter((one) => ticked.some((chosen) => chosen.repository === one.repository && chosen.purpose === one.purpose)).map(tickId)}
                onTick={(tick, on) => {
                  const read = accepts.find((one) => tickId(one) === tick);
                  if (read) toggle(read, on);
                }}
                disabled={applying}
                cleared={draft.cleared.filter((one) => one.repository === row.repository).map((one) => t('agents.setup.set.cleared', {
                  repository: one.repository, blob: short(one.blob), line: one.line, purpose: t(`agents.setup.set.accept.${one.purpose}`),
                }))}
              />
            );
          })}
        </Section>
      )}

      {plan !== null && (
        <details className="m-0 min-w-0 text-meta text-ink-faint">
          <summary className="cursor-pointer">{t('agents.setup.set.terminal')}</summary>
          <code className="mt-1 block font-mono [overflow-wrap:anywhere]">{plan.terminal}</code>
        </details>
      )}

      {result && (result.done
        ? (
          <div role="status" className="grid min-w-0 gap-1.5 border-l-[3px] border-accent pl-2.5 text-small text-ink">
            <p className="m-0 [overflow-wrap:anywhere]"><Inline text={result.message} /></p>
            <ul aria-label={t('agents.setup.set.operations')} className="m-0 grid list-none gap-0.5 p-0 text-ink-soft">
              {result.operations.map((each) => (
                <li key={each.id} className="m-0 flex min-w-0 flex-wrap items-baseline gap-x-2 [overflow-wrap:anywhere]">
                  <span><Inline text={`${each.said} — ${t(`agents.setup.set.outcome.${each.outcome}`, { defaultValue: each.outcome })}${each.note ? `: ${each.note}` : ''}`} /></span>
                  {each.quest && onOpenQuest && (
                    <Button variant="ghost" className="min-h-0 border border-line px-2.5 py-0.5 text-small" onClick={() => onOpenQuest(each.quest!)}>
                      {each.repository ? t('agents.setup.set.openQuest', { repository: each.repository }) : t('agents.setup.set.openQuestAny')}
                    </Button>
                  )}
                </li>
              ))}
            </ul>
            {draft.ended && (
              <ul aria-label={t('agents.setup.set.ended')} className="m-0 grid list-none gap-0.5 p-0">
                {draft.ended.map((each) => (
                  <li key={each.state} className="m-0 [overflow-wrap:anywhere]">
                    {t('agents.setup.set.endedLine', { repositories: each.repositories.join(', '), state: t(`agents.setup.page.pill.${ENDED[each.state]}`) })}
                  </li>
                ))}
              </ul>
            )}
          </div>
        )
        : <Refused sentence={result.message} />)}

      <div className="flex flex-wrap items-center gap-2">
        <Button variant="primary" disabled={!canApply} onClick={onApply} data-review-apply="">
          {applying ? t('agents.setup.set.applying') : t('agents.setup.set.applyTo', { count })}
        </Button>
        {plan !== null && !plan.applies && <span className="text-small text-ink-soft">{t('agents.setup.set.cannot')}</span>}
      </div>
    </section>
  );
}
