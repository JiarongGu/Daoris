import { useTranslation } from 'react-i18next';
import type { QuestAnswer, QuestRequirement, QuestStep } from '../api';
import { ago } from '../format';
import { Button, Pill, SectionTitle } from '../ui';

/** The person's words, quoted: set apart by a rule, never slanted, kept as they were said (D133 §1). */
function Quoted({ words }: { words: string }) {
  return <p className="m-0 border-l-2 border-line-strong pl-2.5 text-body text-ink whitespace-pre-wrap wrap-anywhere">{words}</p>;
}

/**
 * **One requirement** (DRIFT1c, D133 §3): its number, the person's words it quotes, the check that proves them, and how the
 * done answered it (DRIFT1d, §4): *met*, with how its check was met, or *departed*, with the reason and the person's words
 * the reason relied on. A quest's page lists every one; Ask Daoris's accept card lists the departures (DRIFT1d2).
 *
 * @remarks
 * **A molecule**: every state is its props. **The words are content** (`translation-parity`): the person's quoted as they
 * said them, the check and the done's words shown as written; only the labels translate. A departure that holds its quest
 * for the person's yes wears open's hue, the person's (D126 §2.3); one accepted is neutral, and *met* wears done's.
 */
export function RequirementItem({ number, requirement, answer, waiting = false }: {
  number: number;
  requirement: QuestRequirement;
  /** How the done answered it; absent before a done answered. */
  answer?: QuestAnswer | null;
  /** A departure that holds its quest for the person's yes. */
  waiting?: boolean;
}) {
  const { t } = useTranslation();
  const met = answer?.met?.trim() ? answer.met : null;
  const departed = !met && answer?.departed?.trim() ? answer.departed : null;
  return (
    <li className="grid grid-cols-[max-content_minmax(0,1fr)] gap-x-2.5">
      <span className="pt-0.5 font-mono text-meta text-ink-faint">{number}</span>
      <div className="min-w-0">
        <Quoted words={requirement.quote} />
        <dl className="m-0 mt-1.5 grid grid-cols-[max-content_minmax(0,1fr)] items-baseline gap-x-3 gap-y-1 text-small">
          <dt className="text-ink-faint">{t('quests.requirements.check')}</dt>
          <dd className="m-0 text-ink-soft wrap-anywhere">{requirement.check}</dd>
          {met && (
            <>
              <dt><Pill tone="done">{t('quests.requirements.met')}</Pill></dt>
              <dd className="m-0 text-ink wrap-anywhere">{met}</dd>
            </>
          )}
          {departed && (
            <>
              <dt><Pill tone={waiting ? 'open' : 'neutral'}>{t('quests.requirements.departed')}</Pill></dt>
              <dd className="m-0 text-ink wrap-anywhere">{departed}</dd>
              {answer?.quote?.trim() && (
                <>
                  <dt className="text-ink-faint">{t('quests.requirements.reliedOn')}</dt>
                  <dd className="m-0"><Quoted words={answer.quote} /></dd>
                </>
              )}
            </>
          )}
        </dl>
      </div>
    </li>
  );
}

/**
 * **What the person required of a quest, and how its done answered** (DRIFT1d2; D133 §3–§4): every requirement, numbered
 * as the service orders them, which is how its done answers them.
 *
 * @remarks
 * - **A held departure is what the quest waits on**: the section says so, with what the yes lets go on, and carries
 *   *Accept the departure*, one press, where the departure is read (as the trust grant is offered where its hold is read,
 *   D73). The quest's page places it above the body then, as it places a conflict.
 * - **An accepted one says when**, and offers no second yes; a done that answered none says so, and a quest still to
 *   close says how its done will answer.
 */
export function QuestRequirements({ id, requirements, answers = [], status, held = false, accepted, next, accepting = false, onAccept, className }: {
  /** The quest's id, which a next step's `{parent}` names. */
  id: string;
  requirements: QuestRequirement[];
  answers?: QuestAnswer[];
  status: 'Open' | 'Taken' | 'Done' | 'Declined';
  /** A departure holds it for the person's yes. */
  held?: boolean;
  /** When the person gave the yes. */
  accepted?: string | null;
  /** The chain's next step it holds, which the yes publishes. */
  next?: QuestStep;
  accepting?: boolean;
  /** The yes (the service's accept door) — absent where there is no door to say it through. */
  onAccept?: () => void;
  className?: string;
}) {
  const { t } = useTranslation();
  const answerOf = (number: number) => answers.find((answer) => answer.requirement === number) ?? null;
  const closed = status === 'Done' || status === 'Declined';
  return (
    <section
      aria-label={t('quests.requirements.title')}
      className={held
        ? `rounded-card border border-line border-l-[3px] border-l-st-open bg-raised px-3 py-2.5 ${className ?? ''}`
        : className}
    >
      <SectionTitle>{t('quests.requirements.title')}</SectionTitle>
      {held && (
        <p className="m-0 mb-2.5 text-small text-ink-soft">
          {/* Two sentences join the catalogue's way: a space after an English full stop, none after a Chinese one. */}
          {next
            ? t('quests.requirements.join', {
              first: t('quests.requirements.held'),
              second: t('quests.requirements.heldNext', { title: next.title.replaceAll('{parent}', id), to: next.to }),
            })
            : t('quests.requirements.held')}
        </p>
      )}
      <ol className="m-0 grid list-none gap-3 p-0">
        {requirements.map((requirement, index) => (
          <RequirementItem
            key={index}
            number={index + 1}
            requirement={requirement}
            answer={answerOf(index + 1)}
            waiting={held}
          />
        ))}
      </ol>
      {!closed && answers.length === 0 && (
        <p className="m-0 mt-2.5 text-small text-ink-faint">{t('quests.requirements.toAnswer')}</p>
      )}
      {status === 'Done' && answers.length === 0 && (
        <p className="m-0 mt-2.5 text-small text-ink-faint">{t('quests.requirements.unanswered')}</p>
      )}
      {accepted && !held && (
        <p className="m-0 mt-2.5 text-small text-ink-soft">{t('quests.requirements.accepted', { ago: ago(accepted) })}</p>
      )}
      {held && onAccept && (
        <Button variant="primary" className="mt-3" disabled={accepting} onClick={onAccept}>
          {t('quests.requirements.accept')}
        </Button>
      )}
    </section>
  );
}
