import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, CheckField } from '../../design/ui';
import { type Answered, Refused } from '../InlineConfirm';
import type { HelperAskRow } from '../waits/HelperAsks';
import { wentOf } from './teamRows';

/** What *Ask again…* sends: the person's words, and whether the answer goes to the lead rather than to them. */
export type AskedAgain = { question: string; forLead: boolean };

export type MemberThreadProps = {
  /** The lead's title, as the rail names it. */
  lead: string;
  /** The ask this member answers, from its lead's asks; null while they are read or where none names it. */
  ask: HelperAskRow | null;
  /** The member's own record still works. */
  working: boolean;
  /** *Back to lead*: attends the lead at the card it came from. */
  onBack?: () => void;
  /** *Ask again…*, told back how it ended. */
  onAskAgain?: (asked: AskedAgain, answered: Answered) => void;
  /** *Pass to the lead*, told back how it ended. */
  onPass?: (answered: Answered) => void;
  busy?: boolean;
};

/**
 * A member's thread head: *Working for {lead}*, its role and its confinement, and *Back to
 * lead*. While it works, the sentence that a co-worker takes one turn; once it ended, where its answer went, *Pass to the lead* where the
 * answer is the person's, and *Ask again…*: a new helper on a fresh copy, handed this exchange as Daoris's record. Nothing is resumed.
 */
export function MemberThread({ lead, ask, working, onBack, onAskAgain, onPass, busy = false }: MemberThreadProps) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState(false);
  const [question, setQuestion] = useState('');
  const [forLead, setForLead] = useState(false);
  const [refused, setRefused] = useState<string | null>(null);
  const went = ask ? wentOf(ask) : null;
  const answered = ask?.state === 'answered';
  return (
    <section data-member-thread className="grid gap-1.5 rounded-control border-l-[3px] border-l-accent bg-accent-soft/30 px-3 py-2">
      <div className="flex min-w-0 flex-wrap items-baseline gap-x-3 gap-y-1">
        <span className="min-w-0 text-small text-ink wrap-anywhere">{t('work.team.thread.for', { title: lead })}</span>
        <span className="text-meta text-ink-faint">{t('work.team.role.helper')} · {t('work.team.thread.confined')}</span>
        {onBack && <button type="button" className="text-small text-accent underline" onClick={onBack}>{t('work.team.thread.back')}</button>}
      </div>
      {working && <p className="m-0 text-small text-ink-soft">{t('work.team.thread.working')}</p>}
      {!working && went && <p data-went={went} className="m-0 text-small text-ink-soft">{t(`work.team.thread.went.${went}`, { lead })}</p>}
      {!working && ask && (
        <div className="flex flex-wrap items-center gap-2">
          {went === 'person' && ask.kept && onPass && (
            <Button disabled={busy} onClick={() => { setRefused(null); onPass({ done: () => setRefused(null), refused: setRefused }); }}>
              {t('work.team.pass')}
            </Button>
          )}
          {answered && onAskAgain && !asking && <Button onClick={() => setAsking(true)}>{t('work.team.again')}</Button>}
        </div>
      )}
      {asking && onAskAgain && (
        <form
          className="grid gap-1.5"
          onSubmit={(event) => {
            event.preventDefault();
            const words = question.trim();
            if (!words) return;
            setRefused(null);
            onAskAgain({ question: words, forLead }, {
              done: () => { setAsking(false); setQuestion(''); setForLead(false); },
              refused: setRefused,
            });
          }}
        >
          <p className="m-0 text-small text-ink-soft">{t('work.team.again.says')}</p>
          <label className="grid gap-1 text-small text-ink-soft">
            {t('work.team.again.label')}
            <textarea
              value={question}
              onChange={(event) => setQuestion(event.target.value)}
              rows={3}
              className="rounded-control border border-line bg-raised px-2 py-1 text-body text-ink"
            />
          </label>
          <CheckField checked={forLead} onChange={setForLead} label={t('work.team.again.forLead')} />
          <div className="flex gap-2">
            <Button type="submit" variant="primary" disabled={busy || question.trim().length === 0}>{t('work.team.again.send')}</Button>
            <Button variant="ghost" onClick={() => setAsking(false)}>{t('work.team.again.cancel')}</Button>
          </div>
        </form>
      )}
      {refused && <Refused sentence={refused} />}
    </section>
  );
}
