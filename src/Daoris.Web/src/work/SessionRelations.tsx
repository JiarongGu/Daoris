import { useTranslation } from 'react-i18next';
import type { Quest, Session } from '../api';
import { Pill, QUEST_TONE, SectionTitle } from '../ui';
import type { Relations } from './relations';

/** How much of an answer the head carries: enough to know what was said, and the quest holds the rest. */
const SAYS = 280;

const DOOR = 'min-w-0 cursor-pointer truncate border-0 bg-transparent p-0 text-left underline decoration-line-strong underline-offset-2 hover:text-accent hover:decoration-accent';

/**
 * Who a session worked with (SESS1): the session that asked for its
 * quest, the answer it carried on after, and what it asked of other repositories with their answers.
 *
 * @remarks
 * Beside the chain strip, which draws the ask, the steps and a quest's sessions; this says what the
 * strip cannot: the sideways asks (D32's quests between repositories) and the questions a session
 * waited on (D79). **Each is a door**, as the strip's are: a session attends it, a quest opens its
 * record. Nothing to say, and nothing is drawn.
 *
 * A molecule: the relations arrive worked out (`relationsOf`), and a press goes out.
 */
export function SessionRelations({ relations, onQuest, onSession }: {
  relations: Relations;
  onQuest?: (quest: Quest) => void;
  onSession?: (session: Session) => void;
}) {
  const { t } = useTranslation();
  const { askedBy, resumedAfter, asked } = relations;
  if (!askedBy && !resumedAfter && asked.length === 0) return null;

  const title = (quest: Quest) => (onQuest
    ? <button type="button" onClick={() => onQuest(quest)} className={`${DOOR} text-small text-ink`}>{quest.title}</button>
    : <span className="min-w-0 truncate text-small text-ink">{quest.title}</span>);

  return (
    <section aria-label={t('work.relations.title')}>
      <SectionTitle level={3}>{t('work.relations.title')}</SectionTitle>
      <dl className="m-0 grid gap-2.5">
        {askedBy && (
          <div className="grid gap-0.5">
            <dt className="text-meta text-ink-faint">{t('work.relations.askedBy')}</dt>
            <dd className="m-0 font-mono text-meta text-ink-soft">
              {askedBy.session && onSession
                ? (
                  <button type="button" onClick={() => onSession(askedBy.session!)} className={DOOR}>
                    {t('work.relations.session', { id: askedBy.id, repository: askedBy.session.repository })}
                  </button>
                )
                : askedBy.session
                  ? t('work.relations.session', { id: askedBy.id, repository: askedBy.session.repository })
                  // Another machine's, or a record this one no longer holds: still named.
                  : t('work.relations.elsewhere', { id: askedBy.id })}
            </dd>
          </div>
        )}

        {resumedAfter && (
          <div className="grid gap-0.5">
            <dt className="text-meta text-ink-faint">{t('work.relations.resumed', { id: resumedAfter.id })}</dt>
            <dd className="m-0 grid gap-0.5">
              <p className="m-0 flex min-w-0 flex-wrap items-center gap-x-2">
                {title(resumedAfter)}
                <span className="text-meta text-accent">→ {resumedAfter.to}</span>
              </p>
              <Answer quest={resumedAfter} />
            </dd>
          </div>
        )}

        {asked.length > 0 && (
          <div className="grid gap-1">
            <dt className="text-meta text-ink-faint">{t('work.relations.asked', { count: asked.length })}</dt>
            {asked.map(({ quest, question }) => (
              <dd key={quest.id} className="m-0 grid gap-0.5">
                <p className="m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
                  <Pill tone={QUEST_TONE[quest.status]}>{t(`status.${quest.status}`)}</Pill>
                  <span className="text-meta text-accent">→ {quest.to}</span>
                  {title(quest)}
                  {question && <span className="text-meta text-ink-faint">{t('work.relations.question')}</span>}
                </p>
                <Answer quest={quest} />
              </dd>
            ))}
          </div>
        )}
      </dl>
    </section>
  );
}

/** What came back: the close's own words, which are content and never translated. */
function Answer({ quest }: { quest: Quest }) {
  const { t } = useTranslation();
  if (quest.status !== 'Done' && quest.status !== 'Declined') {
    return <p className="m-0 text-meta text-ink-faint">{t('work.relations.waiting')}</p>;
  }
  const note = quest.note?.replace(/\s+/g, ' ').trim();
  if (!note) return null;
  const said = note.length > SAYS ? `${note.slice(0, SAYS).trimEnd()}…` : note;
  return (
    <p className="m-0 text-small text-ink-soft">
      {t(quest.status === 'Done' ? 'work.relations.answer' : 'work.relations.declined', { note: said })}
    </p>
  );
}
