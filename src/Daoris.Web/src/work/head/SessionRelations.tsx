import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Quest, Session } from '../../data/api';
import { SectionTitle } from '../../design/ui';
import { QuestStatus } from '../../quests/QuestStatus';
import { questName } from '../identity';
import { Note } from '../Note';
import type { Relations } from './relations';

/** How much of an answer the head carries: enough to know what was said, and the quest holds the rest. */
const SAYS = 280;

/** The press that opens or folds the relations, in the ink of the chain's and the parts' own presses. */
const PRESS = 'cursor-pointer border-0 bg-transparent p-0 text-small text-accent hover:underline';

const DOOR ='min-w-0 cursor-pointer truncate border-0 bg-transparent p-0 text-left underline decoration-line-strong underline-offset-2 hover:text-accent hover:decoration-accent';

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
 * **Folded to one line** (HEADFIT1; the UI/UX design §3.7 and R4, the UX7 design §6.2): measured in a real browser the relations cost
 * 126 px between the record's head and its conversation, so the page says who it worked with in a line (who asked, what it carried
 * on after, how many it asked of others) and opens the rest in place, as the milestone and the parts do. The line gives way to the
 * list rather than sitting above it, so the title is said once, and *Hide* is the press back, as the chain's is.
 *
 * A molecule: the relations arrive worked out (`relationsOf`), a press goes out, and its one state is whether it is open. The page
 * mounts it under the session's id, so another record starts folded.
 */
export function SessionRelations({ relations, onQuest, onSession, defaultOpen = false }: {
  relations: Relations;
  onQuest?: (quest: Quest) => void;
  onSession?: (session: Session) => void;
  /** Open on first draw: a story's or a test's, since the page's own default is folded. */
  defaultOpen?: boolean;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(defaultOpen);
  const { askedBy, resumedAfter, asked } = relations;
  if (!askedBy && !resumedAfter && asked.length === 0) return null;

  if (!open) {
    const line = [
      askedBy ? t('work.relations.line.askedBy', { who: askedBy.session?.repository ?? askedBy.id }) : null,
      resumedAfter ? t('work.relations.line.resumed', { id: resumedAfter.id }) : null,
      asked.length > 0 ? t('work.relations.line.asked', { count: asked.length }) : null,
    ].filter(Boolean).join(' · ');
    return (
      <section aria-label={t('work.relations.title')} className="flex min-w-0 flex-wrap items-baseline gap-x-2 text-small text-ink-faint">
        <span className="font-semibold">{t('work.relations.title')}</span>
        <span className="min-w-0 text-ink-soft [overflow-wrap:anywhere]">{line}</span>
        <button type="button" onClick={() => setOpen(true)} className={PRESS}>{t('work.relations.show')}</button>
      </section>
    );
  }

  // Each quest by its name (SESSUX1j), whole title on its tip.
  const title = (quest: Quest) => (onQuest
    ? <button type="button" title={quest.title} onClick={() => onQuest(quest)} className={`${DOOR} text-small text-ink`}>{questName(quest)}</button>
    : <span title={quest.title} className="min-w-0 truncate text-small text-ink">{questName(quest)}</span>);

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
                  <QuestStatus quest={quest} />
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
      <button type="button" onClick={() => setOpen(false)} className={`${PRESS} mt-1`}>{t('work.relations.hide')}</button>
    </section>
  );
}

/** What came back: the close's own words, which are content and never translated. */
function Answer({ quest }: { quest: Quest }) {
  const { t } = useTranslation();
  if (quest.status !== 'Done' && quest.status !== 'Declined') {
    return <p className="m-0 text-meta text-ink-faint">{t('work.relations.waiting')}</p>;
  }
  // A coded note is worded in the reader's language beside the label, one run (QUESTCLOSE1b2).
  if (quest.noteParts?.length) {
    const label = t(quest.status === 'Done' ? 'work.relations.answer' : 'work.relations.declined', { note: '' }).trimEnd();
    return (
      <p className="m-0 text-small text-ink-soft">
        {label}{t('work.note.join', { first: '', second: '' })}<Note note={quest.note} parts={quest.noteParts} compact clamp={2} />
      </p>
    );
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
