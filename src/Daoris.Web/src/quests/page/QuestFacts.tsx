import { type Quest, type Session } from '../../data/api';
import { ago, stamp } from '../../locales/format';
import { type Consideration, sittingSentence, type TrustHold, waitsForAccount } from '../../data/signals';
import { Button, Inline, Pill, QUEST_TONE } from '../../design/ui';
import { type WorkDoor, type AbandonOutcome } from '../../work/pausing/pausing';
import { DetailsFold } from '../../work/head/DetailsFold';
import { endOffersStop, endSentences, type GraphEnd } from '../../work/head/milestone';
import { type Answered } from '../../work/InlineConfirm';
import { Quoted } from '../Requirements';
import { type TFunction } from 'i18next';
import { type QuestShown } from '../records';
import { Fact } from './Fact';
import { QUEST_ACT, type QuestAct } from './questActs';

export function QuestFacts({ quest, t, lanes, question, answered, onOpenQuest, because, hold, pausedUnseen, ownPause, work, waiting, press, pausedBy, onOpenAsk, onGrant, onTrusting, trusting, onRetry, retrying, endedHere, session, asking, reads, reopenable, ends, abandoned }: {
  quest: Quest;
  t: TFunction<"translation", undefined>;
  lanes: string | undefined;
  question: { id: string; quest?: Quest | undefined; } | null | undefined;
  answered: boolean;
  onOpenQuest: (id: string) => void;
  because: Consideration | null;
  hold: TrustHold | null | undefined;
  pausedUnseen: boolean;
  ownPause: boolean;
  work: WorkDoor | undefined;
  waiting: boolean;
  press: (act: QuestAct) => void;
  pausedBy: { scope: "quest" | "ask"; id: string; } | null;
  onOpenAsk: ((id: string) => void) | undefined;
  onGrant: ((hold: TrustHold, answered: Answered) => void) | undefined;
  onTrusting: ((open: boolean) => void) | undefined;
  trusting: boolean;
  onRetry: (() => void) | undefined;
  retrying: boolean;
  endedHere: boolean;
  session: Session | null | undefined;
  asking: "done" | "pause" | "decline" | "abandon" | "delete" | "clear" | "clearFailed" | "reopen" | null;
  reads: QuestShown;
  reopenable: boolean;
  ends: GraphEnd[];
  abandoned: { outcome: AbandonOutcome; at: string; } | null;
}) {
  return (
    <>
<DetailsFold
        className="mb-4"
        summary={[
          `#${quest.id}`,
          t('quests.facts.filedAt', { at: stamp(quest.filed) }),
          ...(quest.updated !== quest.filed ? [t('quests.facts.movedAt', { at: stamp(quest.updated) })] : []),
        ].join(' · ')}
        rows={[
          { label: t('work.head.id'), value: `#${quest.id}`, mono: true, copy: quest.id },
          { label: t('quests.detail.from'), value: quest.from },
          { label: t('quests.detail.to'), value: quest.to },
          // Each lane as the repository declares it, named where its registration says.
          { label: t('quests.detail.lanes'), value: lanes || null },
          { label: t('quests.detail.filed'), value: `${stamp(quest.filed)} · ${ago(quest.filed)}` },
          { label: t('quests.detail.moved'), value: quest.updated !== quest.filed ? `${stamp(quest.updated)} · ${ago(quest.updated)}` : null },
          // The short title its publisher gave, where the head says it and the whole title stands below.
          { label: t('quests.field.short'), value: quest.short && quest.short !== quest.title ? quest.short : null },
        ]}
      />

      <dl className="m-0 mb-4 grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1 text-body empty:hidden">
        {question && (
          // What its taker asked and waits on, opened in place: the answer is read there.
          <Fact name={t(answered ? 'quests.detail.asked' : 'quests.detail.waitsOn')}>
            <span className="inline-flex flex-wrap items-baseline gap-1.5">
              {question.quest ? (
                <button
                  type="button"
                  className="cursor-pointer border-0 bg-transparent p-0 text-left text-body text-accent underline-offset-2 hover:underline"
                  onClick={() => onOpenQuest(question.id)}
                >
                  <span className="font-mono text-meta">#{question.id}</span> {question.quest.title}
                </button>
              ) : (
                <span className="font-mono text-meta">#{question.id}</span>
              )}
              {question.quest && (
                <>
                  <span className="text-meta text-ink-faint">→ {question.quest.to}</span>
                  <Pill tone={QUEST_TONE[question.quest.status]}>{t(`status.${question.quest.status}`)}</Pill>
                </>
              )}
            </span>
            <span className="mt-0.5 block text-small text-ink-soft">
              {t(answered ? 'quests.detail.answered' : 'quests.detail.waitsWhy')}
            </span>
          </Fact>
        )}
        {(because || hold || pausedUnseen) && (
          // Why this machine's driver is not starting it, in its own words — the whole sentence here,
          // where there is room. The trust hold stands on its own: it arrives in the same tick as the sentence,
          // and the grant must not wait on a second list having arrived too.
          <Fact name={t('quests.detail.sitting')}>
            {/* A wait for an account (TOOL4g, D125 §4): not parked, so no Retry; it starts by itself at the reset. */}
            {waitsForAccount(because) && (
              <span className="mb-1 block"><Pill tone="neutral">{t('work.waitsForAccount')}</Pill></span>
            )}
            <Inline
              text={because ? sittingSentence(because) : pausedUnseen ? t('quests.detail.pausedOwn') : t('work.attention.trustWhy')}
            />
            {/* A pause comes before every other reason (D132 §2.3): its own is resumed here, where Try again stands for a
                stop; an ask's, or the quest's whose question this is, is resumed on that page, a door away. */}
            {(ownPause || pausedUnseen) && work && (
              <span className="mt-1.5 block">
                <Button disabled={waiting} onClick={() => press('resume')}>{t('quests.detail.resume')}</Button>
              </span>
            )}
            {pausedBy?.scope === 'ask' && onOpenAsk && (
              <span className="mt-1.5 block">
                <Button variant="ghost" onClick={() => onOpenAsk(pausedBy.id)}>{t('quests.detail.openAsk', { id: pausedBy.id })}</Button>
              </span>
            )}
            {pausedBy?.scope === 'quest' && !ownPause && (
              <span className="mt-1.5 block">
                <Button variant="ghost" onClick={() => onOpenQuest(pausedBy.id)}>{t('quests.detail.openPausing', { id: pausedBy.id })}</Button>
              </span>
            )}
            {/* The one hold only the person can lift, offered where it is read (D73). */}
            {hold && onGrant && onTrusting && !trusting && (
              <span className="mt-1.5 block"><Button onClick={() => press('trust')}>{t('trust.open')}</Button></span>
            )}
            {/* And the other two: a quest parked by its strikes, started again on the press (RETRY1), counted from
                where it stands so the next failures park it again; and one the person's stop holds, released from that
                stop (SESSUX1b, D126 §3.4). One act and one word, on this page and on the session that holds it. */}
            {(because?.verdict === 'Exhausted' || because?.verdict === 'Stopped') && onRetry && (
              <span className="mt-1.5 block">
                <Button disabled={retrying} onClick={() => press('retry')}>{t('quests.detail.retry')}</Button>
              </span>
            )}
          </Fact>
        )}
        {endedHere && (
          /* No verdict says why it sits, since nothing here carries a finished session's quest on, so the page
             says it, with the person's done where it is read, as Try again stands under a stop's sentence. */
          <Fact name={t('quests.detail.sitting')}>
            <Inline text={t('quests.detail.endedTaken', { session: session!.id })} />
            {asking !== 'done' && (
              <span className="mt-1.5 block">
                <Button disabled={waiting} onClick={() => press('done')}>{t(QUEST_ACT.done.label)}</Button>
              </span>
            )}
          </Fact>
        )}
        {reads === 'Submitted' && quest.submission && (
          /* When its agent made it ready, and the person's reopen of it where a step stands on it (CLOSE1c3). */
          <Fact name={t('quests.detail.submitted')}>
            {stamp(quest.submission.at)} · {ago(quest.submission.at)}
            {quest.submission.session && <> · {t('quests.detail.submittedBy', { session: quest.submission.session })}</>}
            {reopenable && asking !== 'reopen' && (
              <span className="mt-1.5 block">
                <Button disabled={waiting} onClick={() => press('reopen')}>{t(QUEST_ACT.reopen.label)}</Button>
              </span>
            )}
          </Fact>
        )}
        {quest.status === 'Taken' && !quest.submission && quest.reopened && (
          /* A source the person reopened (FABRIC-CLOSE1d3, after CLOSE1c4 and CLOSE1c8): taken with no submission, which would
             otherwise read as one whose taker has not said ready yet; when, their words as they wrote them, and the session told. */
          <Fact name={t('quests.detail.reopened')}>
            {stamp(quest.reopened.at)} · {ago(quest.reopened.at)}
            <span className="mt-1 block">{t('quests.detail.reopenedWaits')}</span>
            <div className="mt-1.5">
              {quest.reopened.note ? <Quoted words={quest.reopened.note} /> : t('quests.detail.reopenedNoWords')}
            </div>
            {quest.reopened.session && (
              <span className="mt-1.5 block">{t('quests.detail.reopenedTold', { session: quest.reopened.session })}</span>
            )}
          </Fact>
        )}
        {ends.length > 0 && (
          /* Its closure graph's end (FABRIC-CLOSE1j2): no milestone carries a quest's unit, so its page says what the work's foot says
             of one, in the same words; a stop offered wears the danger ink, and its press is What needs you's (CLOSE1j's choice 5). */
          <Fact name={t('work.milestone.closed.label')}>
            {ends.flatMap((end, index) => endSentences(t, end).map((said, part) => (
              <span key={`${end.kind}-${end.at}-${index}-${part}`} className={endOffersStop(end) ? 'block text-ink-danger' : 'block'}>{said}</span>
            )))}
          </Fact>
        )}
        {abandoned && (
          /* When its work was abandoned on this machine (§4.2); the reason is the decline's note, on the record below. */
          <Fact name={t('quests.detail.abandonedAt')}>{stamp(abandoned.at)} · {ago(abandoned.at)}</Fact>
        )}
      </dl>
    </>
  );
}
