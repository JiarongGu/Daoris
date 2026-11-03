import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline, Prose, SkeletonRows } from '../../design/ui';
import { PageSection } from '../../work/frame/ViewMain';
// The look as the tab draws it; the bridge's `IntegrationLook` is one (D52: the molecule imports nothing of the bridge).
type IntegrationRequestRow = {
  request: string;
  line: string;
  branch: string;
  tip: string;
  since: string;
  unit?: string | null;
  said?: string | null;
  ordered?: 'first' | 'last' | null;
};
type Decide = 'split' | 'hand-back' | 'land';
type IntegrationCandidateRow = {
  candidate: string;
  line: string;
  base: string;
  since: string;
  members: { request: string; branch: string; state: string; said?: string | null }[];
  steps: { step: string; state: string }[];
  at?: string | null;
  holds?: 'decide' | 'conflict' | 'promotion' | null;
  release?: string | null;
  by?: string | null;
  why?: string | null;
  decided?: Decide | null;
};
type IntegrationWaitingRow = { unit: string; branch: string; tip: string; code: string; said: string; since?: string | null };
type IntegrationLook = {
  repository: string;
  queue: IntegrationRequestRow[];
  candidates: IntegrationCandidateRow[];
  handedBack: IntegrationRequestRow[];
  superseded: { request: string; commit: string; subject: string; by: string; files: number }[];
  promoted: IntegrationRequestRow[];
  waiting?: IntegrationWaitingRow[];
};

/** What a page hands its Integration tab: the look, or that it is on its way or was refused, and the owner's acts. */
export type IntegrationTabProps = {
  look?: IntegrationLook | null;
  reading?: boolean;
  refusal?: string | null;
  /** The owner's *Drop* of a queued request; none, and the row offers no press. */
  onDrop?: (request: string) => void;
  /** *Check again*: have the line looked at now. */
  onRecheck?: () => void;
  /** The owner's decide on a candidate the policy held for one (INTEGRATE1h): *Split*, *Hand back*, *Land*. */
  onDecide?: (candidate: string, decide: Decide) => void;
  /** A queued request moved first or last in its line's queue (INTEGRATE1h). */
  onOrder?: (request: string, place: 'first' | 'last') => void;
  /** A superseded commit answered (INTEGRATE1h): *Superseded*, or *Rebase it*, which asks its session. */
  onAnswer?: (request: string, commit: string, answer: 'superseded' | 'rebase') => void;
  /** The owner's card for a landing whose integration waits for it (INTEGRATE1h): *Request into the line*. */
  onRequest?: (branch: string) => void;
  /** The related sessions' read, drawn read-only after the history (LINESTOGETHER1c). */
  related?: ReactNode;
};

const short = (tip: string) => tip.slice(0, 7);

const PRESS = 'border border-line px-2 py-0 text-meta';

/**
 * **A repository's Integration tab** (INTEGRATE1h; the integration design §5.8): the queue in its order, each line's standing candidate with
 * its members and a fixed strip of steps, what holds it and the act that releases it, what was handed back, the superseded commits awaiting
 * a disposition with their answers, the landings waiting for the owner's request, and the promoted history. Read from the integration
 * record, never copied; a terminal's twin is `daoris integrate`.
 */
export function IntegrationTab({
  repository, look, reading = false, refusal = null, onDrop, onRecheck, onDecide, onOrder, onAnswer, onRequest, related,
}: IntegrationTabProps & { repository: string }) {
  const { t } = useTranslation();
  if (!look) {
    return (
      <div aria-busy={reading || undefined} className="grid gap-2">
        <Prose className="text-small"><Inline text={refusal ?? t('integration.view.reading')} /></Prose>
        {!refusal && <SkeletonRows rows={3} />}
      </div>
    );
  }

  const waiting = look.waiting ?? [];
  const nothing = look.queue.length + look.candidates.length + look.handedBack.length + look.superseded.length + look.promoted.length + waiting.length === 0;
  return (
    <div className="grid min-w-0 gap-5">
      <section className="grid min-w-0 gap-1.5">
        <h2 className="m-0 text-title font-semibold text-ink">{t('integration.view.title')}</h2>
        <Prose className="text-small"><Inline text={t('integration.view.body', { repository })} /></Prose>
        <p className="m-0 text-meta text-ink-faint"><Inline text={t('integration.view.twin', { repository })} /></p>
        {onRecheck && <div><Button variant="ghost" className="border border-line px-2.5 py-0.5 text-small" onClick={onRecheck}>{t('integration.view.recheck')}</Button></div>}
      </section>

      {nothing && <Prose className="text-small">{t('integration.view.none')}</Prose>}

      {waiting.length > 0 && (
        <PageSection title={t('integration.view.waiting')}>
          <ul className="m-0 grid list-none gap-1.5 p-0 text-small">
            {waiting.map((each) => (
              <li key={each.unit} className="grid min-w-0 gap-0.5">
                <span className="flex min-w-0 flex-wrap items-baseline gap-x-2">
                  <span className="font-mono text-ink [overflow-wrap:anywhere]">{each.branch}</span>
                  <span className="font-mono text-meta text-ink-faint">{short(each.tip)}</span>
                  {onRequest && (
                    <Button variant="ghost" className={PRESS} aria-label={t('integration.view.requestNamed', { branch: each.branch })} onClick={() => onRequest(each.branch)}>
                      {t('integration.view.request')}
                    </Button>
                  )}
                </span>
                <Prose className="text-meta text-ink-soft">{each.said}</Prose>
              </li>
            ))}
          </ul>
        </PageSection>
      )}

      {look.queue.length > 0 && (
        <PageSection title={t('integration.view.queue')}>
          <Requests label={t('integration.view.queue')} rows={look.queue} numbered onDrop={onDrop} onOrder={onOrder} />
        </PageSection>
      )}

      {look.candidates.map((candidate) => <Candidate key={candidate.candidate} candidate={candidate} onDecide={onDecide} />)}

      {look.handedBack.length > 0 && (
        <PageSection title={t('integration.view.handedBack')}>
          <Requests rows={look.handedBack} />
        </PageSection>
      )}

      {look.superseded.length > 0 && (
        <PageSection title={t('integration.view.superseded')}>
          <ul className="m-0 grid list-none gap-1 p-0 text-small">
            {look.superseded.map((each) => (
              <li key={`${each.request}-${each.commit}`} className="flex min-w-0 flex-wrap items-baseline gap-x-2">
                <Inline text={t('integration.view.supersededRow', { commit: short(each.commit), subject: each.subject, by: short(each.by), files: each.files })} />
                {onAnswer && (
                  <>
                    <Button variant="ghost" className={PRESS} onClick={() => onAnswer(each.request, each.commit, 'superseded')}>{t('integration.view.answer.superseded')}</Button>
                    <Button variant="ghost" className={PRESS} onClick={() => onAnswer(each.request, each.commit, 'rebase')}>{t('integration.view.answer.rebase')}</Button>
                  </>
                )}
              </li>
            ))}
          </ul>
        </PageSection>
      )}

      {look.promoted.length > 0 && (
        <PageSection title={t('integration.view.promoted')}>
          <Requests rows={look.promoted} />
        </PageSection>
      )}

      {related}
    </div>
  );
}

function Requests({ rows, label, numbered = false, onDrop, onOrder }: {
  rows: IntegrationRequestRow[];
  label?: string;
  numbered?: boolean;
  onDrop?: (request: string) => void;
  onOrder?: (request: string, place: 'first' | 'last') => void;
}) {
  const { t } = useTranslation();
  return (
    <ul aria-label={label} className="m-0 grid list-none gap-1.5 p-0 text-small">
      {rows.map((row, at) => (
        <li key={row.request} className="grid min-w-0 gap-0.5">
          <span className="flex min-w-0 flex-wrap items-baseline gap-x-2">
            {numbered && <span className="tabular-nums text-meta text-ink-faint">{t('integration.view.place', { place: at + 1 })}</span>}
            <span className="font-mono text-ink [overflow-wrap:anywhere]">{row.branch}</span>
            <span className="font-mono text-meta text-ink-faint">{short(row.tip)}</span>
            <span className="text-meta text-ink-faint">{t('integration.view.into', { line: row.line })}</span>
            {row.ordered && <span className="text-meta text-ink-soft">{t(`integration.view.ordered.${row.ordered}`)}</span>}
            {onOrder && at > 0 && (
              <Button variant="ghost" className={PRESS} aria-label={t('integration.view.firstNamed', { branch: row.branch })} onClick={() => onOrder(row.request, 'first')}>
                {t('integration.view.first')}
              </Button>
            )}
            {onOrder && at < rows.length - 1 && (
              <Button variant="ghost" className={PRESS} aria-label={t('integration.view.lastNamed', { branch: row.branch })} onClick={() => onOrder(row.request, 'last')}>
                {t('integration.view.last')}
              </Button>
            )}
            {onDrop && (
              <Button variant="ghost" className={PRESS} aria-label={t('integration.view.dropNamed', { branch: row.branch })} onClick={() => onDrop(row.request)}>
                {t('integration.view.drop')}
              </Button>
            )}
          </span>
          {row.said && <Prose className="text-meta text-ink-soft">{row.said}</Prose>}
        </li>
      ))}
    </ul>
  );
}

function Candidate({ candidate, onDecide }: { candidate: IntegrationCandidateRow; onDecide?: (candidate: string, decide: Decide) => void }) {
  const { t } = useTranslation();
  const title = t('integration.view.candidate', { line: candidate.line });
  return (
    <PageSection title={title}>
      <ol aria-label={t('integration.view.steps')} className="m-0 mb-2 flex list-none flex-wrap gap-1.5 p-0 text-meta">
        {candidate.steps.map((step) => (
          <li key={step.step} data-step-state={step.state} className="rounded-full border border-line-strong px-2 py-px text-ink-soft">
            {t(`integration.view.step.${step.step}`)} · {t(`integration.view.state.${step.state}`, { defaultValue: step.state })}
          </li>
        ))}
      </ol>
      <ul aria-label={t('integration.view.members')} className="m-0 grid list-none gap-1 p-0 text-small">
        {candidate.members.map((member) => (
          <li key={member.request} className="grid min-w-0 gap-0.5">
            <span><span className="font-mono">{member.branch}</span> · {t(`integration.view.member.${member.state}`, { defaultValue: member.state })}</span>
            {member.said && <Prose className="text-meta text-ink-soft">{member.said}</Prose>}
          </li>
        ))}
      </ul>
      {candidate.holds === 'decide' && (
        <div className="mt-2 grid gap-1.5 rounded-control bg-accent-soft px-3 py-2 text-small">
          <p className="m-0">
            <Inline text={candidate.decided
              ? t('integration.view.holds.decided', { decide: t(`integration.view.decidedWord.${candidate.decided}`) })
              : t('integration.view.holds.decide', { why: candidate.why ?? '' })} />
          </p>
          {!candidate.decided && candidate.release && onDecide && (
            <div className="flex flex-wrap gap-1.5">
              {(['split', 'hand-back', 'land'] as const).map((decide) => (
                <Button key={decide} variant="ghost" className={PRESS} title={t(`integration.view.decideWhy.${decide}`)} onClick={() => onDecide(candidate.candidate, decide)}>
                  {t(`integration.view.decide.${decide}`)}
                </Button>
              ))}
            </div>
          )}
        </div>
      )}
      {candidate.holds && candidate.holds !== 'decide' && (
        <p className="mt-2 mb-0 rounded-control bg-accent-soft px-3 py-2 text-small">
          <Inline text={t(`integration.view.holds.${candidate.holds}`)} />
        </p>
      )}
    </PageSection>
  );
}
