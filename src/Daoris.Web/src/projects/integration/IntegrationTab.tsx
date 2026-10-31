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
};
type IntegrationCandidateRow = {
  candidate: string;
  line: string;
  base: string;
  since: string;
  members: { request: string; branch: string; state: string; said?: string | null }[];
  steps: { step: string; state: string }[];
  at?: string | null;
  holds?: 'conflict' | 'promotion' | null;
  release?: string | null;
  by?: string | null;
};
type IntegrationLook = {
  repository: string;
  queue: IntegrationRequestRow[];
  candidates: IntegrationCandidateRow[];
  handedBack: IntegrationRequestRow[];
  superseded: { request: string; commit: string; subject: string; by: string; files: number }[];
  promoted: IntegrationRequestRow[];
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
};

const short = (tip: string) => tip.slice(0, 7);

/**
 * **A repository's Integration tab** (INTEGRATE1h; the integration design §5.8): the queue, each line's standing candidate with its members
 * and a fixed strip of steps, what holds it and the act that releases it, what was handed back, the superseded commits awaiting a
 * disposition, and the promoted history. Read from the integration record, never copied; a terminal's twin is `daoris-driver integrate`.
 */
export function IntegrationTab({ repository, look, reading = false, refusal = null, onDrop, onRecheck }: IntegrationTabProps & { repository: string }) {
  const { t } = useTranslation();
  if (!look) {
    return (
      <div aria-busy={reading || undefined} className="grid gap-2">
        <Prose className="text-small"><Inline text={refusal ?? t('integration.view.reading')} /></Prose>
        {!refusal && <SkeletonRows rows={3} />}
      </div>
    );
  }

  const nothing = look.queue.length + look.candidates.length + look.handedBack.length + look.superseded.length + look.promoted.length === 0;
  return (
    <div className="grid min-w-0 gap-5">
      <section className="grid min-w-0 gap-1.5">
        <h2 className="m-0 text-title font-semibold text-ink">{t('integration.view.title')}</h2>
        <Prose className="text-small"><Inline text={t('integration.view.body', { repository })} /></Prose>
        <p className="m-0 text-meta text-ink-faint"><Inline text={t('integration.view.twin', { repository })} /></p>
        {onRecheck && <div><Button variant="ghost" className="border border-line px-2.5 py-0.5 text-small" onClick={onRecheck}>{t('integration.view.recheck')}</Button></div>}
      </section>

      {nothing && <Prose className="text-small">{t('integration.view.none')}</Prose>}

      {look.queue.length > 0 && (
        <PageSection title={t('integration.view.queue')}>
          <Requests rows={look.queue} onDrop={onDrop} />
        </PageSection>
      )}

      {look.candidates.map((candidate) => <Candidate key={candidate.candidate} candidate={candidate} />)}

      {look.handedBack.length > 0 && (
        <PageSection title={t('integration.view.handedBack')}>
          <Requests rows={look.handedBack} />
        </PageSection>
      )}

      {look.superseded.length > 0 && (
        <PageSection title={t('integration.view.superseded')}>
          <ul className="m-0 grid list-none gap-1 p-0 text-small">
            {look.superseded.map((each) => (
              <li key={`${each.request}-${each.commit}`}>
                <Inline text={t('integration.view.supersededRow', { commit: short(each.commit), subject: each.subject, by: short(each.by), files: each.files })} />
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
    </div>
  );
}

function Requests({ rows, onDrop }: { rows: IntegrationRequestRow[]; onDrop?: (request: string) => void }) {
  const { t } = useTranslation();
  return (
    <ul className="m-0 grid list-none gap-1.5 p-0 text-small">
      {rows.map((row) => (
        <li key={row.request} className="grid min-w-0 gap-0.5">
          <span className="flex min-w-0 flex-wrap items-baseline gap-x-2">
            <span className="font-mono text-ink [overflow-wrap:anywhere]">{row.branch}</span>
            <span className="font-mono text-meta text-ink-faint">{short(row.tip)}</span>
            <span className="text-meta text-ink-faint">{t('integration.view.into', { line: row.line })}</span>
            {onDrop && (
              <Button variant="ghost" className="border border-line px-2 py-0 text-meta" aria-label={t('integration.view.dropNamed', { branch: row.branch })} onClick={() => onDrop(row.request)}>
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

function Candidate({ candidate }: { candidate: IntegrationCandidateRow }) {
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
      {candidate.holds && (
        <p className="mt-2 mb-0 rounded-control bg-accent-soft px-3 py-2 text-small">
          <Inline text={t(`integration.view.holds.${candidate.holds}`, { act: candidate.release ?? '', by: t(`integration.view.by.${candidate.by ?? 'owner'}`) })} />
        </p>
      )}
    </PageSection>
  );
}
