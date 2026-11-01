import { type ReactNode, useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, CheckField, ChoiceRow, Inline, Pill, SkeletonRows } from '../../design/ui';
import type { PresetWorkflow } from '../../workflow/edit/answers';
import { deliveryFor, deliveryName } from '../../workflow/edit/presetWords';
import { Refused } from '../../work/InlineConfirm';
import {
  BUILDS, doorOf, drivingOf, isChosen, KEEP, NOT_DRIVEN, offered, PARTS, type PlaceAccept, type PlaceDeclaration, type PlacePart, type PlacePlanned,
  type PlaceRefusal, refusalsOf, type SetUpDraft, suggestionsOf,
} from './placePlan';
import { PlaceChooser } from './PlaceChooser';

const short = (id: string) => id.slice(0, 7);

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
  onApply: () => void;
  /** Hold the repositories whose queued work driving would release. */
  onHold: (repositories: string[]) => void;
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

/** The acceptances a declaration is offered for, each its own choice, apart from the text they are of, which is shown once. */
function Declaration({ repository, declaration, accept, onAccept, disabled }: {
  repository: string;
  declaration: PlaceDeclaration;
  accept: readonly PlaceAccept[];
  onAccept: (accept: PlaceAccept[]) => void;
  disabled: boolean;
}) {
  const { t } = useTranslation();
  return (
    <div
      role="group"
      aria-label={t('agents.setup.set.declaration', { repository, line: declaration.line, commit: short(declaration.commit) })}
      className="grid min-w-0 gap-1.5 rounded-control border border-line px-2.5 py-1.5"
    >
      <details open={declaration.acceptances.length > 0} className="m-0 min-w-0 text-small">
        <summary className="cursor-pointer text-ink [overflow-wrap:anywhere]">
          {t('agents.setup.set.declarationAt', { repository, line: declaration.line, commit: short(declaration.commit), blob: short(declaration.blob) })}
        </summary>
        {declaration.text
          ? <pre className="m-0 mt-1 max-h-48 overflow-auto rounded-control border border-line bg-raised px-2 py-1 font-mono text-meta text-ink">{declaration.text}</pre>
          : <p className="m-0 mt-1 text-ink-soft">{t('agents.setup.set.declarationNone')}</p>}
      </details>
      {declaration.acceptances.map((each) => {
        const one = { repository, purpose: each.purpose };
        const waits = each.standing === 'changed' || each.standing === 'unaccepted';
        const label = t(`agents.setup.set.accept.${each.purpose}`);
        return (
          <div key={each.purpose} data-purpose={each.purpose} className="grid min-w-0 gap-0.5">
            {waits
              ? (
                <CheckField
                  checked={isChosen(accept, one)}
                  onChange={(on) => onAccept(on ? [...accept, one] : accept.filter((was) => !isChosen([was], one)))}
                  label={label}
                  disabled={disabled}
                  className="whitespace-normal font-medium text-ink"
                />
              )
              : <span className="text-body font-medium text-ink">{label}</span>}
            <span className="text-small text-ink-soft">{t(`agents.setup.set.standing.${each.standing}`, { defaultValue: each.standing })}</span>
            <span className="text-small text-ink-soft"><Inline text={each.said} /></span>
          </div>
        );
      })}
    </div>
  );
}

/**
 * **Set up…**'s choices (AGENTSPLACE1j, SETUPUX1j; the setup-UX design §2.2): a place's quick set-up as one page of three parts kept apart,
 * *How it lands* (*Keep how it lands*, the deliveries the data offers, *Not driven*, and the second opinion and your look), *Adoption* with
 * *What it builds*, and *Driving*, each with the refusals about it, whole, and the door that repairs their cause and returns. Below, the plan the
 * driver reads for them and the acceptances it offers, and one *Apply*. The suggestion is the plan's own and comes preselected where every
 * repository suggests the same, and nothing is preselected where they differ, each choice naming those it suits; roles are no part of it.
 *
 * @remarks
 * The page judges nothing: every refusal is the driver's sentence, and a plan that refuses offers no Apply. Apply is the person's own
 * press (`PLACE_APPLY`); a plan that moved since it was read is refused `moved` and read again, which is said here too.
 *
 * A molecule: the draft, the plan and the last result arrive as props, and every choice and press goes out.
 */
export function PlaceSetUp({
  place, kind, choices, deliveries, doorFor, draft, onChange, plan: read, reading, stale, problem, applying, holding, onApply, onHold, onClose,
}: PlaceSetUpProps) {
  const { t, i18n } = useTranslation();
  const id = useId();
  const namesSaid = useNamesSaid();
  // Until the suggestion is preselected the plan in hand is the one that read it, of nothing asked: it is not shown.
  const plan = draft.preset === null ? null : read;
  const delivers = draft.preset !== NOT_DRIVEN;
  const adoption = draft.adoption ?? false;
  const suggestion = suggestionsOf(read);
  const choosing = draft.preset === null && read !== null && suggestion?.mixed === true;
  const several = (plan?.repositories.length ?? 0) > 1;
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
  const result = draft.result;
  const releasing = plan?.repositories.filter((row) => row.released.length > 0) ?? [];
  const accepts = offered(plan);
  const chosenAccepts = draft.accept.filter((one) => isChosen(accepts, one));
  const nothing = plan !== null && plan.changes.length === 0 && chosenAccepts.length === 0;
  const busy = reading || stale || applying;
  const canApply = plan !== null && plan.applies && !nothing && !busy && !none && draft.preset !== null;

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
        <CheckField checked={delivers && draft.opinion} onChange={(on) => onChange({ opinion: on })} label={t('agents.setup.set.opinion')} disabled={!delivers} className="whitespace-normal text-ink" />
        <CheckField checked={delivers && draft.look} onChange={(on) => onChange({ look: on })} label={t('agents.setup.set.look')} disabled={!delivers} className="whitespace-normal text-ink" />
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
                  : plan.refusals.length === 0 && <p className="m-0 text-small text-ink-soft">{t('agents.setup.set.nothing')}</p>}
                {plan.repositories.flatMap((row) => row.lowers.map((each) => (
                  <p key={`${row.repository}${each}`} className="m-0 text-small text-ink [overflow-wrap:anywhere]">
                    <span className="font-medium">{t('agents.setup.set.lowers')}</span>{' '}
                    {several && <span className="font-mono text-meta">{row.repository}: </span>}
                    <Inline text={each} />
                  </p>
                )))}
                {plan.repositories.flatMap((row) => row.reach.filter((each) => each.said && each.cannots.length === 0).map((each) => (
                  <p key={`${row.repository}${each.said}`} className="m-0 text-small text-ink-soft [overflow-wrap:anywhere]"><Inline text={each.said!} /></p>
                )))}
                {plan.repositories.filter((row) => row.unread).map((row) => (
                  <p key={`unread-${row.repository}`} className="m-0 text-small text-ink-soft [overflow-wrap:anywhere]">
                    {t('agents.setup.set.unread', { repository: row.repository })} <Inline text={row.unread!} />
                  </p>
                ))}
                {releasing.length > 0 && (
                  <div className="grid min-w-0 gap-1">
                    <p id={`${id}-releases`} className="m-0 text-small font-medium text-ink">{t('agents.setup.set.releases')}</p>
                    <ul aria-labelledby={`${id}-releases`} className="m-0 grid list-none gap-0.5 p-0">
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
                  </div>
                )}
                {plan.repositories.filter((row) => row.declaration).map((row) => (
                  <Declaration
                    key={row.repository}
                    repository={row.repository}
                    declaration={row.declaration!}
                    accept={draft.accept}
                    onAccept={(accept) => onChange({ accept })}
                    disabled={applying}
                  />
                ))}
                <p className="m-0 text-meta text-ink-faint">
                  {t('agents.setup.set.terminal')} <code className="font-mono [overflow-wrap:anywhere]">{plan.terminal}</code>
                </p>
              </>
            )}
      </section>

      {result && (result.done
        ? (
          <div role="status" className="grid min-w-0 gap-1 border-l-[3px] border-accent pl-2.5 text-small text-ink">
            <p className="m-0 [overflow-wrap:anywhere]"><Inline text={result.message} /></p>
            <ul aria-label={t('agents.setup.set.operations')} className="m-0 grid list-none gap-0.5 p-0 text-ink-soft">
              {result.operations.map((each) => (
                <li key={each.id} className="m-0 [overflow-wrap:anywhere]">
                  <Inline text={`${each.said} — ${t(`agents.setup.set.outcome.${each.outcome}`, { defaultValue: each.outcome })}${each.note ? `: ${each.note}` : ''}`} />
                </li>
              ))}
            </ul>
          </div>
        )
        : <Refused sentence={result.refused === 'moved' ? t('agents.setup.set.moved') : result.message} />)}

      <div className="flex flex-wrap items-center gap-2">
        <Button variant="primary" disabled={!canApply} onClick={onApply}>{t(applying ? 'agents.setup.set.applying' : 'agents.setup.set.apply')}</Button>
        {plan !== null && !plan.applies && <span className="text-small text-ink-soft">{t('agents.setup.set.cannot')}</span>}
      </div>
    </section>
  );
}
