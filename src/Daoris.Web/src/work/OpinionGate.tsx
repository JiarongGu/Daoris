import { useContext, useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { moment } from '../format';
import { cn } from '../lib/cn';
import { Button, Inline, Pill } from '../ui';
import { type Answered, InlineConfirm, Refused } from './InlineConfirm';
import {
  findingPlace, findingsOf, type OpinionDetail, type OpinionFinding, type OpinionGate as Gate, type OpinionPress,
  OPINION_ASKS_ONCE, opinionPresses, opinionState, type OpinionState, opinionTone, reviewerName,
} from './opinion';
import { FileOpener } from './preview';
import { shortCommit } from './review';

/**
 * The presses a second opinion's gate is handed (XAGENT1g; the second-agent design §8.5), each where the frame can press it:
 * *Ask now*, *Try again* and *Ask again* are one ask, the rule's first reviewer that can; *Ask the same agent, fresh* the working
 * agent's own; *Stop* the reviewer reading; *Go on anyway…* and *I looked myself…* the person's answer with their words; *Send
 * back…* their words to the working session (D137's box). Each tells the gate how it ended, a refusal said inside it.
 */
export type OpinionGateActs = {
  ask?: (answered: Answered) => void;
  sameAgent?: (answered: Answered) => void;
  stop?: (answered: Answered) => void;
  anyway?: (words: string | null, answered: Answered) => void;
  myself?: (words: string | null, answered: Answered) => void;
  sendBack?: (words: string, answered: Answered) => void;
  /** The reviewer's own session, in Sessions, where its conversation and console are. */
  openSession?: (session: string) => void;
};

/** Which act each press needs. */
const ACT: Record<OpinionPress, keyof OpinionGateActs> = {
  askNow: 'ask', tryAgain: 'ask', askAgain: 'ask', sameAgent: 'sameAgent', stop: 'stop', anyway: 'anyway', myself: 'myself',
  sendBack: 'sendBack',
};

/** The border a tone wears, beside its pill's word: never the hue alone (D41 §3). */
const RAIL: Record<ReturnType<typeof opinionTone>, string> = {
  open: 'border-l-st-open', taken: 'border-l-st-taken', done: 'border-l-st-done', neutral: 'border-l-line-strong',
};

/**
 * **Second opinion** (XAGENT1g, D155 points 8–10; the second-agent design §7–§9): where another agent's reading of a session's
 * work stands at its landing's gate, what it found and how the working session answered, and the presses that move it, drawn
 * beside D154's *Review in `<environment>`*, before it, as §7's order has it.
 *
 * @remarks
 * **Each state says itself in the reader's language**, from its code, as the review's gate does (the platform language §4): a
 * sentence the driver writes is chrome. A state the page does not know, or a gate the driver could not read, says the driver's
 * own sentence beneath, marked as the driver's.
 *
 * **Its findings are claims, never facts** (§6.6): each shows its weight, its place (a door to the file's preview at its line,
 * PREVIEW1), its claim, why it matters, how to see it, how sure it is and what it proposes, with the working session's answer
 * beside it, how the driver read that answer, the recheck's word, and whether it is disputed. A fix is *fixed, by its account*,
 * never proven; a pass with no findings says what it read, never *no issues*.
 *
 * **A dispute is the person's** (§8.2): where a press is coming (*Accept…*, or D154's *Reviewed*), the gate says that press answers
 * it; where none is, *Go on anyway…* answers it here. *Send back…* is the person's words to the working session.
 *
 * A molecule: it is handed the gate, its detail and the presses, and reports each press.
 */
export function OpinionGate({
  gate, detail = null, acts = {}, busy = false, pressComing = false, working = null, className,
}: {
  gate: Gate;
  /** The findings and their answers (`OPINION_GATE`'s detail), where the frame read them. */
  detail?: OpinionDetail | null;
  acts?: OpinionGateActs;
  /** A press on the record is on its way: none is offered again until it answers. */
  busy?: boolean;
  /** A press of the person's is coming that answers a dispute or commits nobody read: *Accept…*, or D154's *Reviewed*. */
  pressComing?: boolean;
  /** The working session, which *Send back…* speaks to. */
  working?: string | null;
  className?: string;
}) {
  const { t } = useTranslation();
  const headingId = useId();
  const state = opinionState(gate.state);
  const [asking, setAsking] = useState<OpinionPress | null>(null);
  const [words, setWords] = useState('');
  const [pressing, setPressing] = useState<OpinionPress | null>(null);
  const [refusal, setRefusal] = useState<string | null>(null);
  const findings = findingsOf(detail);
  const [shown, setShown] = useState(state === 'disputed');

  const offered = opinionPresses(state, { required: gate.required === true, pressComing })
    .filter((press) => press !== 'sendBack' || working)
    .filter((press) => acts[ACT[press]] !== undefined);
  const waiting = busy || pressing !== null;
  const told: Answered = {
    done: () => { setPressing(null); setRefusal(null); },
    refused: (sentence) => { setPressing(null); setRefusal(sentence); },
  };
  const press = (which: OpinionPress) => {
    setRefusal(null);
    if (OPINION_ASKS_ONCE.has(which)) { setWords(''); setAsking(which); return; }
    setPressing(which);
    if (which === 'sameAgent') acts.sameAgent?.(told);
    else acts.ask?.(told);
  };
  const close = () => { setAsking(null); setWords(''); };

  const who = reviewerName(gate);
  const tone = opinionTone(state, { holds: gate.holds });
  const sentence = says(t, state, gate, detail, who);
  const unsettled = unsettledWords(t, state, gate);
  const reviewing = gate.reviewing && (state === 'reading' || state === 'read-again') ? gate.reviewing : null;
  const personWords = detail?.person?.words?.trim();

  return (
    <section
      aria-labelledby={headingId}
      className={cn('grid min-w-0 gap-2 rounded-card border border-line border-l-[3px] bg-raised px-3 py-2.5', RAIL[tone], className)}
    >
      <div className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
        {/* Status leads (D41 §4): the state, then what it is and who reads it. */}
        <Pill tone={tone}>{t(`opinion.state.${state}`)}</Pill>
        <h3 id={headingId} className="m-0 min-w-0 text-small font-semibold text-ink">{t('opinion.title')}</h3>
        {who && <span className="min-w-0 text-small text-ink-soft [overflow-wrap:anywhere]">{who}</span>}
      </div>

      {sentence.map((line) => <p key={line} className="m-0 text-small text-ink-soft"><Inline text={line} /></p>)}
      {state === 'unread' && gate.says && (
        <p className="m-0 text-meta text-ink-faint [overflow-wrap:anywhere]"><Inline text={t('opinion.driverSaid', { said: gate.says })} /></p>
      )}
      {personWords && (state === 'anyway' || state === 'myself' || state === 'answered') && (
        <p className="m-0 text-small text-ink-soft [overflow-wrap:anywhere]">{t('opinion.says.personWords', { words: personWords })}</p>
      )}
      {detail?.toPerson && (
        <p className="m-0 text-small text-ink-soft">
          {t(detail.toPerson === 'recheck' || detail.toPerson === 'conversation' ? `opinion.toPerson.${detail.toPerson}` : 'opinion.toPerson.other')}
        </p>
      )}
      {detail?.tier && <p className="m-0 text-meta text-ink-faint">{t(tierKey(detail.tier))}</p>}

      {(findings.length > 0 || detail?.first?.read || detail?.first?.limits) && (
        <div className="grid min-w-0 gap-1.5">
          <Button
            variant="ghost"
            className="justify-self-start px-0 py-0 text-small"
            aria-expanded={shown}
            onClick={() => setShown((was) => !was)}
          >
            {shown ? t('opinion.findingsHide') : findings.length > 0 ? t('opinion.findings', { count: findings.length }) : t('opinion.findingsShow')}
          </Button>
          {shown && <Findings detail={detail!} />}
        </div>
      )}

      {(offered.length > 0 || (reviewing && acts.openSession)) && asking === null && (
        <div className="flex flex-wrap items-center gap-2">
          {offered.map((which) => (
            <Button
              key={which}
              variant={which === 'stop' ? 'danger' : which === offered[0] && !pressComing ? 'primary' : 'default'}
              disabled={waiting}
              onClick={() => press(which)}
            >
              {pressing === which ? t('opinion.act.asking') : t(`opinion.act.${which}`)}
            </Button>
          ))}
          {reviewing && acts.openSession && (
            <Button variant="ghost" onClick={() => acts.openSession?.(reviewing)}>{t('opinion.act.openSession')}</Button>
          )}
        </div>
      )}
      {refusal && <Refused sentence={refusal} />}

      {(asking === 'anyway' || asking === 'myself') && (
        /* Their answer at the gate, with words they may give (§8.5): kept on the gate and the landing record. */
        <InlineConfirm
          tone="primary"
          label={t(asking === 'anyway' ? 'opinion.ask.anywayTitle' : 'opinion.ask.myselfTitle')}
          says={asking === 'anyway' ? t('opinion.ask.anyway', { unsettled }) : t('opinion.ask.myself')}
          meanIt={t(asking === 'anyway' ? 'opinion.ask.anywayMeanIt' : 'opinion.ask.myselfMeanIt')}
          busy={busy}
          onConfirm={(answered) => (asking === 'anyway' ? acts.anyway : acts.myself)?.(words.trim() || null, answered)}
          onClose={close}
        >
          <input
            aria-label={t('opinion.ask.words')}
            placeholder={t('opinion.ask.words')}
            value={words}
            onChange={(event) => setWords(event.target.value)}
            className="min-h-[1.9rem] min-w-0 basis-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
          />
        </InlineConfirm>
      )}
      {asking === 'sendBack' && working && (
        /* D137's box, in the person's own words: they go to the working session as its next turn. */
        <InlineConfirm
          tone="primary"
          block
          label={t('opinion.ask.sendBackTitle')}
          says={t('opinion.ask.sendBack', { session: working })}
          meanIt={t('opinion.ask.sendBackMeanIt')}
          busy={busy}
          ready={words.trim().length > 0}
          onConfirm={(answered) => acts.sendBack?.(words.trim(), answered)}
          onClose={close}
        >
          <textarea
            aria-label={t('opinion.ask.sendBackWords')}
            placeholder={t('opinion.ask.sendBackWords')}
            rows={3}
            value={words}
            onChange={(event) => setWords(event.target.value)}
            className="min-w-0 basis-full resize-y rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
          />
        </InlineConfirm>
      )}
      {asking === 'stop' && (
        /* A stop ends a reading: it asks once, saying the pass then gives no opinion. */
        <InlineConfirm
          label={t('opinion.ask.stopTitle')}
          says={t('opinion.ask.stop', { who })}
          meanIt={t('opinion.ask.stopMeanIt')}
          busy={busy}
          onConfirm={(answered) => acts.stop?.(answered)}
          onClose={close}
        />
      )}
    </section>
  );
}

/** What the gate's state says, in order: the state's sentence, then what answers it or whether it holds. */
function says(
  t: ReturnType<typeof useTranslation>['t'], state: OpinionState, gate: Gate, detail: OpinionDetail | null, who: string,
): string[] {
  const label = gate.label ? t(`opinion.label.${gate.label}`, { defaultValue: gate.label }) : t('opinion.label.another-maker');
  const minutes = detail?.first?.minutes;
  switch (state) {
    case 'waits-chain': return [t('opinion.says.waits-chain', { later: gate.later ?? '' })];
    case 'not-asked': return [t('opinion.says.not-asked')];
    case 'reading': return [typeof minutes === 'number'
      ? t('opinion.says.readingMinutes', { who, label, minutes })
      : t('opinion.says.reading', { who, label })];
    case 'with-session': return [t('opinion.says.with-session', { count: gate.findings ?? 0, who })];
    case 'read-again': return [t('opinion.says.read-again', { who })];
    case 'disputed': return [
      t('opinion.says.disputed', { count: gate.disputes ?? 0, who }),
      t(gate.holds && gate.answers ? 'opinion.says.pressAnswers' : 'opinion.says.noPress'),
    ];
    case 'commits-since': return [
      t('opinion.says.commits-since', { count: gate.since ?? 0 }),
      t(gate.holds && gate.answers ? 'opinion.says.pressAnswers' : 'opinion.says.noPress'),
    ];
    case 'unavailable': return [
      t('opinion.says.unavailable', { why: whyWords(t, gate, who) }),
      t(gate.required ? 'opinion.says.required' : 'opinion.says.optional'),
    ];
    case 'settled': return [(detail?.first?.findings?.length ?? gate.findings ?? 1) === 0
      ? t('opinion.says.settledNothing', { who })
      : t('opinion.says.settled', { who })];
    case 'anyway':
    case 'myself':
    case 'answered':
    case 'unread': return [t(`opinion.says.${state}`)];
    default: return [];
  }
}

/** Why none could be had, by its code (§8.4): a cool-off says until when, in the reader's own time. */
function whyWords(t: ReturnType<typeof useTranslation>['t'], gate: Gate, who: string): string {
  switch (gate.code) {
    case 'no-reviewer':
    case 'signed-out':
    case 'not-independent': return t(`opinion.why.${gate.code}`);
    case 'cooling': return gate.until ? t('opinion.why.coolingUntil', { time: moment(gate.until) }) : t('opinion.why.cooling');
    case 'ended':
    case 'out-of-time': return t(`opinion.why.${gate.code}`, { who: who || gate.reviewer || '' });
    default: return t('opinion.why.other');
  }
}

/** What *Go on anyway…* and *Accept…* leave or answer, in the reader's words. */
export function unsettledWords(t: ReturnType<typeof useTranslation>['t'], state: OpinionState, gate: Gate): string {
  switch (state) {
    case 'disputed': return t('opinion.unsettled.disputed', { count: gate.disputes ?? 0 });
    case 'commits-since': return t('opinion.unsettled.commits', { count: gate.since ?? 0 });
    case 'reading':
    case 'with-session':
    case 'read-again': return t('opinion.unsettled.reading');
    default: return t('opinion.unsettled.none');
  }
}

const tierKey = (tier: string) => (tier === 'person' || tier === 'none' ? `opinion.tier.${tier}` : 'opinion.tier.agent');

/** The findings, each beside its answer, and what the pass read and could not tell (§9). */
function Findings({ detail }: { detail: OpinionDetail }) {
  const { t } = useTranslation();
  const first = detail.first?.findings ?? [];
  const recheck = detail.recheck?.findings ?? [];
  return (
    <div className="grid min-w-0 gap-2">
      {first.length > 0 && (
        <ol className="m-0 grid min-w-0 list-none gap-2 p-0">
          {first.map((finding) => <Finding key={`first:${finding.number}`} finding={finding} />)}
        </ol>
      )}
      {recheck.length > 0 && (
        <>
          <h4 className="m-0 text-meta font-semibold text-ink-faint">{t('opinion.recheckTitle')}</h4>
          <ol className="m-0 grid min-w-0 list-none gap-2 p-0">
            {recheck.map((finding) => <Finding key={`recheck:${finding.number}`} finding={finding} />)}
          </ol>
        </>
      )}
      {[detail.first, detail.recheck].filter(Boolean).map((pass) => (
        <dl key={pass!.id} className="m-0 grid min-w-0 gap-1 text-small">
          {pass!.read && (
            <div className="min-w-0">
              <dt className="text-meta text-ink-faint">{t('opinion.read')}</dt>
              <dd className="m-0 whitespace-pre-wrap text-ink-soft [overflow-wrap:anywhere]">{pass!.read}</dd>
            </div>
          )}
          {pass!.limits && (
            <div className="min-w-0">
              <dt className="text-meta text-ink-faint">{t('opinion.limits')}</dt>
              <dd className="m-0 whitespace-pre-wrap text-ink-soft [overflow-wrap:anywhere]">{pass!.limits}</dd>
            </div>
          )}
        </dl>
      ))}
    </div>
  );
}

/**
 * One finding: its weight leads, then its place and its claim, why it matters, how to see it, how sure, what it proposes, and
 * beside it the working session's answer, how the driver read it and the recheck's word (§6.4–§6.6). The reviewer's words are
 * content, shown as written.
 */
function Finding({ finding }: { finding: OpinionFinding }) {
  const { t } = useTranslation();
  const open = useContext(FileOpener);
  const place = findingPlace(finding.where);
  const where = finding.where === 'general' ? t('opinion.finding.general')
    : /^[0-9a-f]{7,64}$/i.test(finding.where) ? t('opinion.finding.atCommit', { commit: shortCommit(finding.where) })
      : finding.where;
  const { answer, counts, rechecked, disputed } = finding.beside ?? {};
  return (
    <li className={cn('grid min-w-0 gap-1 border-l-2 pl-2.5', disputed ? 'border-l-st-open' : 'border-l-line')}>
      <p className="m-0 flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5 text-small">
        <span className="font-mono text-meta text-ink-faint">#{finding.number}</span>
        <Pill tone={finding.weight === 'must' ? 'open' : 'neutral'}>{t(`opinion.weight.${finding.weight}`, { defaultValue: finding.weight })}</Pill>
        {place && open
          ? (
            <button
              type="button"
              onClick={() => open(place)}
              className="min-w-0 border-0 bg-transparent p-0 text-left font-mono text-meta text-accent underline-offset-2 hover:underline [overflow-wrap:anywhere]"
            >
              {where}
            </button>
          )
          : <span className="min-w-0 font-mono text-meta text-ink-soft [overflow-wrap:anywhere]"><Inline text={where} /></span>}
        {finding.sure && <span className="text-meta text-ink-faint">{t(`opinion.sure.${finding.sure}`, { defaultValue: finding.sure })}</span>}
        {disputed && <span className="text-meta font-semibold text-ink-open">{t('opinion.disputed')}</span>}
      </p>
      <p className="m-0 whitespace-pre-wrap text-body text-ink [overflow-wrap:anywhere]">{finding.claim}</p>
      {finding.consequence && <Said label={t('opinion.finding.consequence')} text={finding.consequence} />}
      {finding.reproduce && <Said label={t('opinion.finding.reproduce')} text={finding.reproduce} />}
      {finding.proposal && <Said label={t('opinion.finding.proposal')} text={finding.proposal} />}
      {(answer || counts) && <p className="m-0 text-small text-ink-soft [overflow-wrap:anywhere]"><Inline text={answerWords(t, finding)} /></p>}
      {rechecked && <p className="m-0 text-meta text-ink-faint">{t(`opinion.rechecked.${rechecked}`, { defaultValue: rechecked })}</p>}
    </li>
  );
}

/** A part of a finding, under its label: the reviewer's words as written. */
function Said({ label, text }: { label: string; text: string }) {
  return (
    <div className="min-w-0 text-small">
      <span className="text-meta text-ink-faint">{label} · </span>
      <span className="whitespace-pre-wrap text-ink-soft [overflow-wrap:anywhere]">{text}</span>
    </div>
  );
}

/**
 * The working session's answer as the person reads it (§6.4, §6.6): a fix the driver checked, *fixed, by its account*; a fix it
 * could not count, why; a rejection with its evidence; unresolved with why; a finding left unanswered, *not answered*.
 */
function answerWords(t: ReturnType<typeof useTranslation>['t'], finding: OpinionFinding): string {
  const { answer, counts } = finding.beside ?? {};
  if (counts?.why) return t(`opinion.counts.${counts.why}`, { defaultValue: counts.why });
  const said = counts?.counts ?? answer?.said;
  if (said === 'fixed') return t('opinion.answer.fixed', { commit: shortCommit(counts?.fix ?? answer?.commit ?? '') });
  if (said === 'rejected') return t('opinion.answer.rejected', { evidence: answer?.evidence ?? '' });
  if (said === 'unresolved') return answer?.why ? t('opinion.answer.unresolved', { why: answer.why }) : t('opinion.answer.notAnswered');
  return t('opinion.answer.none');
}
