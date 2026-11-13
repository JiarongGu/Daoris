import { useTranslation } from 'react-i18next';
import { Pill } from '../design/ui';
import type { AskPlan, AskPlanRevision } from './askPlan';

/** A door into the quest, in the accent, as the page's other doors are. */
const DOOR = 'cursor-pointer border-0 bg-transparent p-0 text-left text-accent underline-offset-2 hover:underline';

/**
 * **An ask's plan**: the parts the lead, Ask Daoris or a person's hand wrote, each in its repository with what a person sees working
 * when it is done and the quest it became, then the order between them and what the judge found. A worker's split of its own part is a
 * plan of its own below the ask's, said by the part it splits.
 *
 * @remarks
 * A molecule: the plan arrives read (`askPlanOf`). Nothing is pressed here: the plan is written through the connector's `team_plan`
 * door, and a part's work is its quest, which `onOpenQuest` opens.
 */
export function AskPlanSection({ plan, onOpenQuest }: { plan: AskPlan; onOpenQuest: (quest: string) => void }) {
  return (
    <div className="grid gap-4">
      {plan.plans.map((revision) => <Revision key={revision.scope ?? ''} revision={revision} onOpenQuest={onOpenQuest} />)}
    </div>
  );
}

function Revision({ revision, onOpenQuest }: { revision: AskPlanRevision; onOpenQuest: (quest: string) => void }) {
  const { t } = useTranslation();
  return (
    <section className="grid gap-2" aria-label={revision.scope ? t('asks.plan.split', { part: revision.scope, revision: revision.revision }) : t('asks.plan.own', { revision: revision.revision })}>
      <p className="m-0 flex flex-wrap items-center gap-x-2 text-meta text-ink-faint">
        <span>{revision.scope ? t('asks.plan.split', { part: revision.scope, revision: revision.revision }) : t('asks.plan.own', { revision: revision.revision })}</span>
        {revision.depth > 1 && <Pill tone="neutral">{t('asks.plan.depth', { count: revision.depth })}</Pill>}
      </p>
      {revision.why && <p className="m-0 wrap-anywhere text-body text-ink">{revision.why}</p>}
      <ul className="m-0 grid list-none gap-2 p-0">
        {revision.parts.map((part) => (
          <li key={part.id} className="grid min-w-0 gap-0.5">
            <span className="flex min-w-0 flex-wrap items-center gap-x-2.5 gap-y-1">
              <span className="min-w-0 wrap-anywhere text-body font-medium text-ink">{part.id}</span>
              <span className="text-meta text-ink-faint">{[part.repository, ...part.lanes].join(' · ')}</span>
              {part.quest
                ? <button type="button" onClick={() => onOpenQuest(part.quest!)} className={`${DOOR} text-meta`}>{t('asks.plan.quest', { id: part.quest })}</button>
                : <Pill tone="neutral">{t('asks.plan.noQuest')}</Pill>}
            </span>
            <span className="min-w-0 wrap-anywhere text-body text-ink-soft">{part.outcome}</span>
          </li>
        ))}
      </ul>
      {revision.edges.length > 0 && (
        <ul className="m-0 grid list-none gap-1 p-0 text-meta text-ink-faint">
          {revision.edges.map((edge) => (
            <li key={`${edge.part}-${edge.kind}-${edge.on}`} className="wrap-anywhere">
              {t(edge.kind === 'orders' ? 'asks.plan.orders' : 'asks.plan.after', { part: edge.part, on: edge.on })}: {edge.why}
            </li>
          ))}
        </ul>
      )}
      {revision.releases.length > 0 && (
        <ul className="m-0 grid list-none gap-1 p-0 text-meta text-ink-faint">
          {revision.releases.map((release) => (
            <li key={`${release.deployable}-${release.source}-${release.on}`} className="flex min-w-0 flex-wrap items-baseline gap-x-2 wrap-anywhere">
              <span>{t('asks.plan.release', { deployable: release.deployable, on: release.on })}: {release.why}</span>
              <Pill tone={release.source === 'policy' ? 'warn' : 'neutral'}>{t(`asks.plan.source.${release.source}`, { defaultValue: release.source })}</Pill>
              {release.evidence && <span>{t('asks.plan.evidence', { where: release.evidence })}</span>}
            </li>
          ))}
        </ul>
      )}
      {revision.questions.length > 0 && (
        <section className="grid gap-1" aria-label={t('asks.plan.questions')}>
          <p className="m-0 text-meta text-ink-faint">{t('asks.plan.questions')}</p>
          <ul className="m-0 grid list-none gap-1.5 p-0">
            {revision.questions.map((question) => (
              <li key={question.key} className="grid min-w-0 gap-0.5 text-meta">
                <span className="wrap-anywhere text-body text-ink">{question.text}</span>
                {question.yes && <span className="text-ink-faint">{t('asks.plan.question.yes', { text: question.yes })}</span>}
                {question.blocks && <span className="text-ink-faint">{t('asks.plan.question.blocks', { text: question.blocks })}</span>}
                {question.default && <span className="text-ink-faint">{t('asks.plan.question.default', { text: question.default })}</span>}
              </li>
            ))}
          </ul>
        </section>
      )}
      {revision.flags.length > 0 && (
        <ul className="m-0 grid list-none gap-1 p-0">
          {revision.flags.map((flag) => (
            <li key={`${flag.kind}-${flag.part ?? ''}-${flag.text}`} className="flex min-w-0 flex-wrap items-baseline gap-x-2 text-meta">
              <Pill tone="warn">{t(`asks.plan.flag.${flag.kind}`, { defaultValue: flag.kind })}</Pill>
              <span className="min-w-0 wrap-anywhere text-ink-soft">{flag.text}</span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
