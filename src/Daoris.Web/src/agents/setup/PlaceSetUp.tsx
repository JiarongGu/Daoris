import { type ReactNode, useEffect, useId, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, CheckField, ChoiceRow, Inline, Pill, SkeletonRows } from '../../design/ui';
import type { PresetWorkflow } from '../../workflow/edit/answers';
import { deliveryFor, deliveryName } from '../../workflow/edit/presetWords';
import { Refused } from '../../work/InlineConfirm';
import {
  BUILDS, doorOf, drivingOf, KEEP, NOT_DRIVEN, PARTS, type PlacePart, type PlacePlanned, type PlaceRefusal, refusalsOf, type SetUpDraft, suggestionsOf,
} from './placePlan';
import { PlaceChooser } from './PlaceChooser';
import { type HoldOutcome, PlaceReview } from './PlaceReview';

export type PlaceSetUpProps = {
  /** The place's name: a repository's, or a workspace's. */
  place: string;
  kind: 'repository' | 'workspace';
  /** The repositories a workspace's plan may cover, each with whether it is driven; none for a repository. */
  choices: { repository: string; driven: boolean }[];
  /** The deliveries the set-up writes, as the page's catalogues word them by id (`deliveriesOf`). */
  deliveries: PresetWorkflow[];
  /** A refusal's door: where its cause is put right, which returns here; none where a refusal has no door. */
  doorFor?: (refusal: PlaceRefusal) => (() => void) | null;
  draft: SetUpDraft;
  onChange: (patch: Partial<SetUpDraft>) => void;
  /** The driver's plan for the draft's ask; the last one stays while the next is read. */
  plan: PlacePlanned | null;
  reading: boolean;
  /** The plan shown answers an earlier ask than the draft's. */
  stale: boolean;
  /** The bridge's sentence where the plan could not be asked (a driver still coming up). */
  problem: string | null;
  applying: boolean;
  holding: boolean;
  /** What holding each repository came to (`SET_HOLD`), beside the work it held. */
  holds: Record<string, HoldOutcome>;
  onApply: () => void;
  /** Hold the repositories whose queued work driving would release. */
  onHold: (repositories: string[]) => void;
  /** The door to the adoption quest a done apply started. */
  onOpenQuest?: (quest: string) => void;
  onClose: () => void;
};

/** A part of the choices, its heading able to take the focus so the plan's line can jump to it. */
function Part({ id, title, children }: { id: string; title: string; children: ReactNode }) {
  return (
    <section aria-labelledby={id} className="grid min-w-0 gap-2">
      <h3 id={id} tabIndex={-1} className="m-0 text-body font-semibold text-ink outline-none focus-visible:outline-2 focus-visible:outline-accent">{title}</h3>
      {children}
    </section>
  );
}

/** Up to three names, then how many more. */
function useNamesSaid() {
  const { t } = useTranslation();
  return (names: readonly string[]) =>
    names.length <= 3 ? names.join(', ') : t('agents.setup.set.names', { names: names.slice(0, 3).join(', '), count: names.length - 3 });
}

/** A part's refusals, whole and under it, each with the door that repairs its cause and returns where it has one. */
function PartRefusals({ refusals, doorFor }: { refusals: readonly PlaceRefusal[]; doorFor?: (refusal: PlaceRefusal) => (() => void) | null }) {
  const { t } = useTranslation();
  if (refusals.length === 0) return null;
  return (
    <ul aria-label={t('agents.setup.set.refused')} className="m-0 grid list-none gap-1.5 p-0">
      {refusals.map((each) => {
        const go = doorFor?.(each) ?? null;
        return (
          <li key={`${each.repository ?? ''}${each.code}${each.sentence}`} data-refusal={each.code} className="m-0 grid min-w-0 justify-items-start gap-1 border-l-[3px] border-warn pl-2.5 text-small text-ink-soft [overflow-wrap:anywhere]">
            <span>
              {each.repository && <span className="mr-1.5 font-mono text-meta text-ink">{each.repository}</span>}
              <Inline text={each.sentence} />
            </span>
            {go && doorOf(each.code) && (
              <Button variant="ghost" className="min-h-0 border border-line px-2.5 py-0.5 text-small" onClick={go}>
                {t(`agents.setup.set.door.${each.code}`)}
              </Button>
            )}
          </li>
        );
      })}
    </ul>
  );
}

/**
 * **Set up…** (AGENTSPLACE1j, SETUPUX1j, SETUPUX1k; the setup-UX design §2.2): a place's quick set-up as two pages over one draft and one plan,
 * the choices and the review. The draft says which is in front, so a door out and back, or a seed from Ask Daoris, finds the page as it was
 * left; moving between them writes nothing, and a refusal's link on the review returns to its part on the choices.
 *
 * @remarks
 * A molecule: the draft, the plan and the last result arrive as props, and every choice and press goes out.
 */
export function PlaceSetUp(props: PlaceSetUpProps) {
  const id = useId();
  const { draft, onChange } = props;
  const back = useRef<PlacePart | null>(null);
  // The part a refusal's link named takes the focus once the choices are back.
  useEffect(() => {
    if (draft.review || back.current === null) return;
    document.getElementById(`${id}-${back.current}`)?.focus();
    back.current = null;
  }, [draft.review, id]);
  if (!draft.review) return <PlaceChoices {...props} id={id} />;
  return (
    <PlaceReview
      place={props.place}
      kind={props.kind}
      plan={props.plan}
      draft={draft}
      reading={props.reading}
      stale={props.stale}
      problem={props.problem}
      applying={props.applying}
      holding={props.holding}
      holds={props.holds}
      onBack={(part) => { back.current = part ?? null; onChange({ review: false }); }}
      onLeaveOut={(repository) => onChange({ repositories: draft.repositories.filter((each) => each !== repository) })}
      onAccept={(accept) => onChange({ accept, cleared: [] })}
      onApply={props.onApply}
      onHold={props.onHold}
      onOpenQuest={props.onOpenQuest}
      onClose={props.onClose}
    />
  );
}

/**
 * **Set up…**'s choices (AGENTSPLACE1j, SETUPUX1j): three parts kept apart, *How it lands* (*Keep how it lands*, the deliveries the data offers,
 * *Not driven*, and the second opinion and your look), *Adoption* with *What it builds*, and *Driving*, each with the refusals about it, whole, and
 * the door that repairs their cause and returns. Below, the plan's line (what it changes and what stops it, with a jump to each) and
 * *Review the plan*. The suggestion is the plan's own and comes preselected where every repository suggests the same, and nothing is preselected
 * where they differ, each choice naming those it suits; roles are no part of it.
 *
 * @remarks
 * The page judges nothing: every refusal is the driver's sentence.
 */
function PlaceChoices({
  id, place, kind, choices, deliveries, doorFor, draft, onChange, plan: read, reading, stale, problem, applying, onClose,
}: PlaceSetUpProps & { id: string }) {
  const { t, i18n } = useTranslation();
  const namesSaid = useNamesSaid();
  // Until the suggestion is preselected the plan in hand is the one that read it, of nothing asked: it is not shown.
  const plan = draft.preset === null ? null : read;
  const delivers = draft.preset !== NOT_DRIVEN;
  const adoption = draft.adoption ?? false;
  const suggestion = suggestionsOf(read);
  const choosing = draft.preset === null && read !== null && suggestion?.mixed === true;
  const refusedIn = (part: PlacePart) => refusalsOf(plan, part);
  const stops = PARTS.filter((part) => refusedIn(part).length > 0);
  const jump = (part: PlacePart) => document.getElementById(`${id}-${part}`)?.focus();
  // The suggested choice leads the deliveries; Keep comes first and Not driven last.
  const ordered = [...deliveries].sort((a, b) => Number(b.id === suggestion?.preset) - Number(a.id === suggestion?.preset));
  const pillOf = (preset: string) => (suggestion?.preset === preset ? <Pill tone="neutral">{t('agents.setup.set.suggested')}</Pill> : null);
  const whyOf = (preset: string) => {
    const why = suggestion?.preset === preset ? suggestion.why : null;
    if (!why) return null;
    return (
      <span className="grid gap-0.5">
        <span className="text-small text-ink-soft">{t('agents.setup.set.whyLine', { reason: t(`agents.setup.set.why.${why.code}`, { defaultValue: why.said }) })}</span>
        {why.said && <span className="text-meta text-ink-faint"><Inline text={why.said} /></span>}
      </span>
    );
  };
  const suitsOf = (preset: string) => {
    const names = suggestion?.mixed ? suggestion.suits[preset] : undefined;
    return names ? <span className="text-small text-ink-soft">{t('agents.setup.set.suits', { repositories: namesSaid(names) })}</span> : null;
  };
  const none = draft.repositories.length === 0;
  const busy = reading || stale || applying;
  const canReview = plan !== null && !none && draft.preset !== null;

  return (
    <section aria-labelledby={`${id}-title`} className="grid min-w-0 gap-4">
      <div className="flex min-w-0 flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
        <h2 id={`${id}-title`} className="m-0 text-body font-semibold text-ink [overflow-wrap:anywhere]">{t('agents.setup.set.title', { place })}</h2>
        <Button variant="ghost" className="min-h-0 px-2 py-0.5 text-small" onClick={onClose}>{t('agents.setup.set.close')}</Button>
      </div>
      <p className="m-0 text-small text-ink-soft">{t('agents.setup.set.lead')}</p>

      {kind === 'workspace' && <PlaceChooser rows={choices} chosen={draft.repositories} onChange={(repositories) => onChange({ repositories })} />}

      <Part id={`${id}-delivery`} title={t('agents.setup.set.lands')}>
        <div role="radiogroup" aria-labelledby={`${id}-delivery`} className="grid min-w-0 gap-1.5">
          {[
            { preset: KEEP, name: t('agents.setup.set.keep'), line: t('agents.setup.set.keepLine'), now: suggestion?.following ? t('agents.setup.set.keepFollowing', { workflow: suggestion.following }) : null },
            ...ordered.map((each) => ({ preset: each.id, name: deliveryName(t, i18n, each.delivery!), line: deliveryFor(t, i18n, each.delivery!), now: null })),
            { preset: NOT_DRIVEN, name: t('agents.setup.set.notDriven'), line: t('agents.setup.set.notDrivenLine'), now: null },
          ].map(({ preset, name, line, now }) => (
            <ChoiceRow
              key={preset}
              name={`${id}-delivery`}
              value={preset}
              checked={draft.preset === preset}
              onChange={() => onChange({ preset })}
              labelledBy={`${id}-p-${preset}`}
              describedBy={`${id}-d-${preset}`}
            >
              <span className="grid min-w-0 gap-0.5">
                <span className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-0.5">
                  <span id={`${id}-p-${preset}`} className="text-body text-ink">{name}</span>
                  {pillOf(preset)}
                </span>
                <span id={`${id}-d-${preset}`} className="grid gap-0.5 text-small text-ink-soft">
                  <span>{line}</span>
                  {now && <span><Inline text={now} /></span>}
                  {suitsOf(preset)}
                </span>
                {whyOf(preset)}
              </span>
            </ChoiceRow>
          ))}
        </div>
        <CheckField checked={delivers && draft.preset !== KEEP && draft.opinion} onChange={(on) => onChange({ opinion: on })} label={t('agents.setup.set.opinion')} disabled={!delivers || draft.preset === KEEP} className="whitespace-normal text-ink" />
        <CheckField checked={delivers && draft.preset !== KEEP && draft.look} onChange={(on) => onChange({ look: on })} label={t('agents.setup.set.look')} disabled={!delivers || draft.preset === KEEP} className="whitespace-normal text-ink" />
        <PartRefusals refusals={refusedIn('delivery')} doorFor={doorFor} />
      </Part>

      <Part id={`${id}-adoption`} title={t('agents.setup.set.adoption')}>
        <CheckField checked={adoption} onChange={(on) => onChange({ adoption: on })} label={t('agents.setup.set.adopt')} className="whitespace-normal text-ink" />
        <p className="m-0 text-small text-ink-soft">{t('agents.setup.set.adoptBody')}</p>
        {adoption && (
          <div className="grid min-w-0 gap-1.5">
            <span id={`${id}-builds`} className="text-small font-medium text-ink">{t('agents.setup.set.builds')}</span>
            <div role="radiogroup" aria-labelledby={`${id}-builds`} className="grid min-w-0 gap-1.5">
              {['', ...BUILDS].map((builds) => (
                <ChoiceRow
                  key={builds || 'none'}
                  name={`${id}-builds`}
                  value={builds}
                  checked={draft.builds === builds}
                  onChange={() => onChange({ builds })}
                  labelledBy={`${id}-b-${builds || 'none'}`}
                >
                  <span id={`${id}-b-${builds || 'none'}`} className="text-body text-ink">{t(`agents.setup.set.builds.${builds || 'none'}`)}</span>
                </ChoiceRow>
              ))}
            </div>
            <p className="m-0 text-small text-ink-soft">{t('agents.setup.set.buildsBody')}</p>
          </div>
        )}
        <PartRefusals refusals={refusedIn('adoption')} doorFor={doorFor} />
      </Part>

      <Part id={`${id}-driving`} title={t('agents.setup.set.driving')}>
        <CheckField checked={delivers && draft.drive} onChange={(on) => onChange({ drive: on })} label={t('agents.setup.set.drive')} disabled={!delivers} className="whitespace-normal text-ink" />
        {plan !== null && (
          <div className="grid gap-0.5 text-small text-ink-soft">
            {!(delivers && draft.drive) && <p className="m-0">{t('agents.setup.set.driving.off')}</p>}
            <ul aria-label={t('agents.setup.set.driving')} className="m-0 grid list-none gap-0.5 p-0">
              {drivingOf(plan).map((each) => (
                <li key={each.state} className="m-0 [overflow-wrap:anywhere]">{t(`agents.setup.set.driving.${each.state}`, { repositories: namesSaid(each.repositories) })}</li>
              ))}
            </ul>
          </div>
        )}
        <PartRefusals refusals={refusedIn('driving')} doorFor={doorFor} />
      </Part>

      <section aria-labelledby={`${id}-plan`} aria-busy={busy || undefined} className="grid min-w-0 gap-2 border-t border-line pt-3">
        <h3 id={`${id}-plan`} className="m-0 text-body font-semibold text-ink">{t('agents.setup.set.plan')}</h3>
        {problem && <Refused sentence={problem} />}
        {none
          ? <p className="m-0 text-small text-ink-soft">{t('agents.setup.set.noneChosen')}</p>
          : choosing
            ? <p className="m-0 text-small text-ink-soft">{t('agents.setup.set.choose')}</p>
            : plan === null
              ? (problem ? null : <div className="grid gap-1"><p className="m-0 text-small text-ink-soft">{t('agents.setup.set.reading')}</p><SkeletonRows rows={3} /></div>)
              : (
              <>
                <div className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-small text-ink">
                  <span>
                    {t('agents.setup.set.planChanges', { count: plan.changes.length })}
                    {' · '}
                    {stops.length === 0 ? t('agents.setup.set.planClear') : t('agents.setup.set.planStops', { count: plan.refusals.length })}
                  </span>
                  {stops.map((part) => (
                    <Button key={part} variant="ghost" className="min-h-0 px-1.5 py-0 text-small" onClick={() => jump(part)}>
                      {`↑ ${t(part === 'delivery' ? 'agents.setup.set.lands' : `agents.setup.set.${part}`)}`}
                    </Button>
                  ))}
                </div>
              </>
            )}
        <div>
          <Button variant="primary" disabled={!canReview || applying} onClick={() => onChange({ review: true })}>{t('agents.setup.set.review')}</Button>
        </div>
      </section>
    </section>
  );
}
