import { useTranslation } from 'react-i18next';
import { ExternalLink } from '../../design/links';
import { Flow } from '../../design/Flow';
import { Icon, Inline } from '../../design/ui';
import { cn } from '../../lib/cn';
import { moment } from '../../locales/format';
import { landingFlow, type LandingOf } from './landingLife';

/**
 * What a landed branch's page holds of how it landed (FLOWCHART1c): the fold, which its holder keeps so the reads are asked only
 * once the person opens it, and the reads.
 */
export type LandingDoor = {
  open: boolean;
  onToggle: () => void;
  /** Once read: the trace's landing for the branch, its due entry and its units; null where no landing on this machine names it. */
  landing?: LandingOf | null;
  /** A first read is on its way. */
  reading?: boolean;
  /** Why the trace's read failed, in its own sentence. */
  refusal?: string | null;
  /** The units' read (`WORK_STATE`) failed or answered nothing of the kind, so no end or clean-up is drawn. */
  unitsUnread?: boolean;
};

/**
 * **How a landed branch landed, drawn as a flowchart** (FLOWCHART1c; D186, the flowchart design §2.7): a section of its page above
 * its commits, folded to its line until it is opened, then its due entry and each try (three of one code stacked), the landing
 * with who accepted it and each later done that moved it on, the push, the pull request's latest answer, the end each unit it
 * carries reached, the clean-up and the recovery kept.
 *
 * @remarks
 * - **Read only when it opens**, as the trace is (D143): the holder reads the trace of the session the branch's record names and
 *   the work's milestones for the branch, and folds it again on a press. The work's milestone and a run's landing step open the
 *   page with it opened.
 * - **The branch's, never one session's**: a landing carrying several sessions' work is read from the one it was made for, which
 *   names every advance and its session (`landingLife`).
 * - A molecule: the reads arrive as props, and its one state is its flow's own.
 */
export function LandingFlow({ name, door, className }: { name: string; door: LandingDoor; className?: string }) {
  const { t } = useTranslation();
  const title = t('landing.title');
  const said = door.refusal ? t('landing.unread', { error: door.refusal })
    : door.reading && door.landing === undefined ? t('landing.reading')
    : door.landing === null ? t('landing.none')
    : t('landing.hint');
  return (
    <section aria-label={title} className={cn('min-w-0', className)}>
      <button
        type="button"
        aria-expanded={door.open}
        onClick={door.onToggle}
        className="flex w-full min-w-0 cursor-pointer items-start gap-1.5 border-0 bg-transparent p-0 text-left text-small text-ink-faint hover:text-ink"
      >
        <Icon name={door.open ? 'chevronDown' : 'chevronRight'} size={12} className="mt-1 shrink-0" />
        <span className="min-w-0 [overflow-wrap:anywhere]">
          <span className="font-semibold">{title}</span>
          <span> · {name}</span>
        </span>
      </button>
      {door.open && (
        <div className="mt-2 grid min-w-0 gap-2 pl-[18px]">
          <p className="m-0 text-small text-ink-faint">{said}</p>
          {door.unitsUnread && <p className="m-0 text-small text-ink-faint">{t('landing.unitsUnread')}</p>}
          {door.landing && <LandingLifeFlow landing={door.landing} />}
        </div>
      )}
    </section>
  );
}

/** The flow itself: each node's lines as the record words them, the pull request's address on its node. */
export function LandingLifeFlow({ landing }: { landing: LandingOf }) {
  const { t, i18n } = useTranslation();
  const life = landingFlow(t, landing, (iso) => moment(iso, i18n.language));
  const nodes = life.nodes.map((node) => {
    const own = life.lines[node.id] ?? [];
    const link = node.id === 'pull-request' && life.pullRequest;
    if (own.length === 0 && !link) return node;
    return {
      ...node,
      lines: (
        <div className="grid min-w-0 justify-items-start gap-0.5">
          {own.map((line, index) => <p key={index} className="m-0 min-w-0 [overflow-wrap:anywhere]"><Inline text={line} /></p>)}
          {link && (
            <ExternalLink href={link} className="text-meta text-accent underline underline-offset-2">{t('work.review.pullRequest')}</ExternalLink>
          )}
        </div>
      ),
    };
  });
  return <Flow label={life.label} nodes={nodes} edges={life.edges} entries={life.entries} />;
}
