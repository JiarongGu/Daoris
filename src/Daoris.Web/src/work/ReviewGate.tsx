import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Quest, QuestSetUp, SetUpRef } from '../api';
import { ago } from '../format';
import { cn } from '../lib/cn';
import { ExternalLink } from '../links';
import { Button, CodeText, Inline, Pill } from '../ui';
import { type Answered, InlineConfirm, Refused } from './InlineConfirm';
import {
  newestSetUp, type ReviewPress, reviewPresses, type ReviewState, setUpRef, shortCommit, verdictOn,
} from './review';

/**
 * The presses a review's gate is handed (REVIEWENV1g; the review environment design §3.2–§3.3, §3.6), each where the frame can
 * press it: a browser has no shell to show a build again or to say a *not yet*'s words to a session. Each verdict carries the
 * set-up this view drew, named whole, so a set-up shown after it was drawn refuses the verdict as stale (REVIEWENV1b3).
 */
export type ReviewGateActs = {
  reviewed?: (setUp: SetUpRef, answered: Answered) => void;
  /** *Not yet…*, with the person's words, which go to the set-up's session as its next turn (design §3.4). */
  notYet?: (setUp: SetUpRef, words: string, answered: Answered) => void;
  /** *Skip the review for this work…*, with the person's words where they give any (design §3.6). */
  skip?: (words: string | null, answered: Answered) => void;
  /** *Set it up in `<environment>`*: a set-up step published following the work (design §2.1, §3.6). */
  setUp?: (environment: string, answered: Answered) => void;
  /** *Show it again*: the set-up's build served to its tab again, the tab brought forward (REVIEWENV1d). */
  showAgain?: () => void;
  /** The set-up step's own page, where the gate is not drawn on it. */
  open?: () => void;
};

/** The hue a state wears: the person's (open) where it waits on their look, taken's while a step works, neutral otherwise. */
const TONE: Record<ReviewState, 'open' | 'taken' | 'done' | 'neutral'> = {
  'not-shown': 'open',
  'being-set-up': 'taken',
  shown: 'open',
  'not-yet': 'taken',
  'not-held': 'open',
  unread: 'neutral',
  reviewed: 'done',
  skipped: 'neutral',
  none: 'neutral',
};

/**
 * **Review in `<environment>`** (REVIEWENV1g, D154 points 7–8; the review environment design §3.1–§3.3, §3.6): where a chain's
 * work waits for the person's look, what the gate says, what the set-up step showed and where, and the presses that answer it,
 * drawn where *Accept…* would be while the gate holds: the session's head, and a set-up step's page.
 *
 * @remarks
 * **Each state says itself in the reader's language**, from its code (the platform language §4: a sentence the driver writes is
 * chrome, translated by its typed half). Where the driver names a state the page does not know, or could not read the gate,
 * its own sentence follows, marked as the driver's.
 *
 * **What the person looked at is what they answer.** The verdict names the newest set-up this view drew, by its machine and
 * sequence; one shown since refuses it in the host's sentence, said here, and the view is read again.
 *
 * **A local set-up's address is not a link.** A tab opened at it loads whatever holds the address on this machine, the person's
 * own server, with nothing to say so; *Show it again* is the door, which serves the step's build to its tab first. A deployed
 * environment's address is the environment itself, and opens.
 *
 * **Never in the browser** (design §3.3): the page in Daoris's browser has no bridge and an agent can drive it, so this is drawn
 * in the window alone. Every verdict is one of the person's doors (D156 §3.2), and the page reaches them as `api.ts` says.
 *
 * A molecule: it is handed the gate, the step's record and the presses, and reports each press.
 */
export function ReviewGate({
  environment, state, step = null, stepId = null, served = null, said = null, proposal = null, acts = {}, busy = false,
  whole = false, className,
}: {
  environment: string;
  state: ReviewState;
  /** The set-up step's record, where the frame holds it: what it showed, and the set-up a verdict names. */
  step?: Quest | null;
  /** The set-up step's id where the frame names one and holds no record of it. */
  stepId?: string | null;
  /** Whether Daoris still serves the newest set-up's tab here (REVIEWENV1d); null where no shell says. */
  served?: boolean | null;
  /** The driver's own sentence, said beneath where the page words none (an unread gate, a newer driver's state). */
  said?: string | null;
  /** The intake's proposal for this work and its reason (design §1.6), shown beside *Skip…* while nothing shows it. */
  proposal?: { choice: string; reason: string } | null;
  acts?: ReviewGateActs;
  /** A press on the record is on its way: none is offered again until it answers. */
  busy?: boolean;
  /** Every set-up the step said, newest first, as its own page lists them; otherwise the newest alone. */
  whole?: boolean;
  className?: string;
}) {
  const { t } = useTranslation();
  const headingId = useId();
  const [asking, setAsking] = useState<'notYet' | 'skip' | null>(null);
  const [words, setWords] = useState('');
  const [pressing, setPressing] = useState<'reviewed' | 'setUp' | null>(null);
  const [refusal, setRefusal] = useState<string | null>(null);

  const newest = step ? newestSetUp(step) : null;
  const ref = setUpRef(newest);
  const quest = step?.id ?? stepId;
  const reviewedSetUp = step ? [...(step.setUps ?? [])].reverse().find((setUp) => verdictOn(step, setUp, 'reviewed')) : undefined;
  const commit = state === 'not-held' ? reviewedSetUp?.commit ?? newest?.commit : newest?.commit;
  const offered = reviewPresses(state, { local: Boolean(newest?.local) }).filter((press) => {
    switch (press) {
      // A verdict names the set-up the view drew: with none whole, it is not offered.
      case 'reviewed': return Boolean(acts.reviewed && ref);
      case 'notYet': return Boolean(acts.notYet && ref);
      case 'showAgain': return Boolean(acts.showAgain);
      case 'skip': return Boolean(acts.skip);
      case 'setUp': return Boolean(acts.setUp);
    }
  });
  const waiting = busy || pressing !== null;

  // A press told how it ended: done clears what it said, a refusal is said under the presses in the host's words.
  const told: Answered = {
    done: () => { setPressing(null); setRefusal(null); },
    refused: (sentence) => { setPressing(null); setRefusal(sentence); },
  };
  const press = (which: ReviewPress) => {
    setRefusal(null);
    if (which === 'reviewed' && ref) { setPressing('reviewed'); acts.reviewed?.(ref, told); }
    else if (which === 'setUp') { setPressing('setUp'); acts.setUp?.(environment, told); }
    else if (which === 'showAgain') acts.showAgain?.();
    else if (which === 'notYet' || which === 'skip') { setWords(''); setAsking(which); }
  };
  const label = (which: ReviewPress) => {
    switch (which) {
      case 'reviewed': return pressing === 'reviewed' ? t('review.act.reviewing') : t('review.act.reviewed');
      case 'notYet': return t('review.act.notYet');
      case 'showAgain': return t('review.act.showAgain');
      case 'skip': return t('review.act.skip');
      case 'setUp': return pressing === 'setUp' ? t('review.act.settingUp') : t('review.act.setUp', { environment });
    }
  };

  const sentence = (() => {
    switch (state) {
      case 'being-set-up': return quest
        ? t('review.says.being-set-up', { quest, environment })
        : t('review.says.being-set-up-nostep', { environment });
      case 'shown':
      case 'not-yet':
      case 'not-held':
      case 'reviewed': return commit ? t(`review.says.${state}`, { quest, commit: shortCommit(commit), environment }) : null;
      default: return t(`review.says.${state}`, { environment });
    }
  })();
  const older = whole && step ? [...(step.setUps ?? [])].reverse().slice(1) : [];

  return (
    <section
      aria-labelledby={headingId}
      className={cn(
        'grid min-w-0 gap-2 rounded-card border border-line border-l-[3px] bg-raised px-3 py-2.5',
        TONE[state] === 'open' ? 'border-l-st-open' : TONE[state] === 'done' ? 'border-l-st-done' : 'border-l-line-strong',
        className,
      )}
    >
      <div className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
        {/* Status leads (D41 §4): the state, then where the work is reviewed. */}
        <Pill tone={TONE[state]}>{t(`review.state.${state}`)}</Pill>
        <h3 id={headingId} className="m-0 min-w-0 text-small font-semibold text-ink">
          <Inline text={t('review.title', { environment })} />
        </h3>
      </div>

      {sentence && <p className="m-0 text-small text-ink-soft"><Inline text={sentence} /></p>}
      {said && (state === 'unread') && (
        <p className="m-0 text-meta text-ink-faint [overflow-wrap:anywhere]"><Inline text={t('review.driverSaid', { said })} /></p>
      )}
      {proposal && state === 'not-shown' && (
        <p className="m-0 border-l-[3px] border-line-strong pl-2 text-small text-ink-soft">
          <Inline text={t('review.proposal', { choice: choiceWords(t, proposal.choice), reason: proposal.reason })} />
        </p>
      )}

      {newest && (state === 'shown' || state === 'not-yet' || state === 'not-held' || whole) && (
        // Whether it is still served matters only while it waits for the look: a verdict lets the tab go (REVIEWENV1d).
        <SetUpShown setUp={newest} step={step!} served={state === 'shown' ? served : null} lead={t('review.setUp.newest')} full />
      )}

      {(offered.length > 0 || (acts.open && quest && !whole)) && asking === null && (
        <div className="flex flex-wrap items-center gap-2">
          {offered.map((which) => (
            <Button
              key={which}
              variant={which === 'reviewed' || (which === 'setUp' && !offered.includes('reviewed')) ? 'primary' : which === 'skip' ? 'ghost' : 'default'}
              disabled={waiting}
              onClick={() => press(which)}
            >
              {which === 'setUp' ? <Inline text={label(which)} /> : label(which)}
            </Button>
          ))}
          {acts.open && quest && !whole && (
            <Button variant="ghost" onClick={acts.open}>{t('review.act.open', { quest })}</Button>
          )}
        </div>
      )}
      {refusal && <Refused sentence={refusal} />}

      {asking === 'notYet' && ref && (
        /* *Not yet* needs words: they are what the step's session acts on (design §3.3). Open until the host answers, a
           refusal said inside it (UXFIX2). */
        <InlineConfirm
          tone="primary"
          block
          label={t('review.ask.notYetTitle')}
          says={t(newest?.session ? 'review.ask.notYet' : 'review.ask.notYetOwn', { quest })}
          meanIt={t('review.ask.notYetMeanIt')}
          busy={busy}
          ready={words.trim().length > 0}
          onConfirm={(answered) => acts.notYet?.(ref, words.trim(), answered)}
          onClose={() => { setAsking(null); setWords(''); }}
        >
          <textarea
            aria-label={t('review.ask.notYetWords')}
            placeholder={t('review.ask.notYetWords')}
            rows={3}
            value={words}
            onChange={(event) => setWords(event.target.value)}
            className="min-w-0 basis-full resize-y rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
          />
        </InlineConfirm>
      )}
      {asking === 'skip' && (
        /* A skip asks once (design §3.6): the work lands without being shown, on the person's word. */
        <InlineConfirm
          tone="primary"
          label={t('review.ask.skipTitle')}
          says={t('review.ask.skip')}
          meanIt={t('review.ask.skipMeanIt')}
          busy={busy}
          onConfirm={(answered) => acts.skip?.(words.trim() || null, answered)}
          onClose={() => { setAsking(null); setWords(''); }}
        >
          <input
            aria-label={t('review.ask.skipWords')}
            placeholder={t('review.ask.skipWords')}
            value={words}
            onChange={(event) => setWords(event.target.value)}
            className="min-h-[1.9rem] min-w-0 basis-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
          />
        </InlineConfirm>
      )}

      {older.length > 0 && (
        <div className="grid gap-1.5 border-t border-line pt-2">
          <h4 className="m-0 text-meta font-semibold text-ink-faint">{t('review.setUp.older')}</h4>
          {older.map((setUp) => (
            <SetUpShown key={`${setUp.machine ?? ''}:${setUp.sequence ?? setUp.commit}`} setUp={setUp} step={step!} />
          ))}
        </div>
      )}
    </section>
  );
}

/** A review choice in the reader's words (design §1.5): `off`, `on`, or an environment's name. */
export function choiceWords(t: ReturnType<typeof useTranslation>['t'], choice: string): string {
  if (choice === 'off') return t('review.choice.off');
  if (choice === 'on') return t('review.choice.on');
  return t('review.choice.in', { environment: choice });
}

/**
 * One set-up as the step said it (design §2.6, §3.3): what it shows, where (a local one's address as code, a deployed one's as
 * a link), whether Daoris still serves it, how to show it again by hand, who said it and at which commit, and the person's
 * verdict on it where they gave one. Content is shown as it is.
 */
function SetUpShown({ setUp, step, served = null, lead, full = false }: {
  setUp: QuestSetUp;
  step: Quest;
  served?: boolean | null;
  lead?: string;
  full?: boolean;
}) {
  const { t } = useTranslation();
  const verdict = verdictOn(step, setUp, 'reviewed') ?? verdictOn(step, setUp, 'not-yet');
  const when = setUp.at ? ago(setUp.at) : '';
  const commit = shortCommit(setUp.commit);
  return (
    <div className="grid min-w-0 gap-1 text-small">
      {lead && <h4 className="m-0 text-meta font-semibold text-ink-faint">{lead}</h4>}
      {setUp.shows
        ? <p className="m-0 whitespace-pre-wrap text-body text-ink [overflow-wrap:anywhere]">{setUp.shows}</p>
        : <p className="m-0 text-ink-faint">{t('review.setUp.nothingSaid')}</p>}
      <p className="m-0 flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5">
        <span className="text-ink-faint">{t('review.setUp.where')}</span>
        {setUp.look
          ? setUp.local === false
            ? <ExternalLink href={setUp.look} className="min-w-0 text-accent underline-offset-2 hover:underline [overflow-wrap:anywhere]">{setUp.look}</ExternalLink>
            : <CodeText text={setUp.look} className="min-w-0 text-meta" />
          : <span className="text-ink-soft">{t('review.setUp.elsewhere', { machine: setUp.machine ?? '' })}</span>}
      </p>
      {full && served !== null && setUp.local !== false && (
        <p className={cn('m-0', served ? 'text-ink-soft' : 'text-warn')}>
          {served ? t('review.setUp.served') : t('review.setUp.unserved')}
        </p>
      )}
      {full && setUp.again && (
        <p className="m-0 text-ink-soft [overflow-wrap:anywhere]">
          <span className="text-ink-faint">{t('review.setUp.again')} · </span>{setUp.again}
        </p>
      )}
      <p className="m-0 text-meta text-ink-faint">
        <Inline text={setUp.session
          ? t('review.setUp.by', { session: setUp.session, ago: when, commit })
          : t('review.setUp.own', { ago: when, commit })} />
      </p>
      {verdict && (
        <p className="m-0 text-ink-soft [overflow-wrap:anywhere]">
          {(() => {
            const said = t(`review.verdict.${verdict.said === 'reviewed' ? 'reviewed' : 'not-yet'}`, { ago: verdict.at ? ago(verdict.at) : '' });
            // Their words, as they wrote them, after what they said.
            return verdict.words ? t('review.verdict.words', { verdict: said, words: verdict.words }) : said;
          })()}
        </p>
      )}
    </div>
  );
}
