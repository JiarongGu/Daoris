import { type ReactNode, useLayoutEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import type { Session } from '../../data/api';
import { Button, Icon, Inline, Menu, Pill, SESSION_TONE, type ShownState, shownKey, Tip } from '../../design/ui';
import { useWidth } from '../../design/useWidth';
import { AccessChip, startedFull } from './AccessFact';
import { choiceChip } from '../actors';
import { ChoiceChip } from './ChoiceChip';
import type { SupervisionChip } from './lineSupervision';
import { cn } from '../../lib/cn';
import { ACT_LOOK, actMenu, headerActs, type SessionActId } from '../acts';
import { type Answered, InlineConfirm } from '../InlineConfirm';
import { PageHead } from '../frame/ViewMain';

/**
 * **A session's page header**: Sessions' main area gains the header every view's chosen item has,
 * pinned at the top while the conversation scrolls under it, since a session's page runs to thousands of
 * events and its acts must stay in reach there.
 *
 * @remarks
 * **A molecule: every state is reached by its props**. Which acts it offers is the one rule's
 * (`offeredActs`), handed in; a press is reported, and the one owner (`sessionActs.ts`) carries it out.
 *
 * - **On the left**: its shown word on its pill first, so the word stays in one place
 *   whatever the title's length; its title on one line, whole in its tip; and under them the facts its state makes matter,
 *   handed in (`sessionFacts`), faint, on one line whole in its tip, ending in the *Full access* chip where its
 *   record says it started so.
 *   Its id is not a line here: it is the ⋯'s *Copy session ID* and the record's *Details*.
 * - **On the right**: the primary act where the state has one (*Try again*, *Review*), then *Stop…* while it is live,
 *   then ⋯ with the rest in the one rule's order. *Answer…* is not here: the card under the header keeps the answer.
 * - **What asks under it** (the stop's ask, the delete's, the hand-over's) is pinned with it, since the ask is about the acts
 *   beside it.
 * - **It is `PageHead`'s**: the title takes one line, cut with an ellipsis and
 *   whole in its tip and in the record's *Details*, and the acts hold still beside it at every width (a row
 *   of their own at the narrow width would cost 38 px of the record head's 200). At 640 px of the main area or less, by its width
 *   and never the window's, *Stop…* is its square, its words its accessible name and its tip: still one press, never in the ⋯.
 *
 * **It says how tall it is** to the main area it is pinned in (`--session-head`), so the long run's toolbar
 * pins beneath it rather than under it.
 */
export function SessionPageHead({ session, title, shown, line = null, facts = [], factsTip, acts, primary = null, busy = false, onAct, asking, next, chip = null }: {
  /** Its line's state where another part or a co-worker decides it (`lineWords`), with *Open* to that one. */
  line?: { said: string; onOpen?: () => void } | null;
  /** The count a rule holds the line at, or one short of it (`supervisionChip`), said last on the facts line before the choice's chip. */
  chip?: SupervisionChip | null;
  /** *Next* on the facts line: the press the first wait leads to, and the door to its step (*To the wait*). */
  next?: { press: 'answer' | 'accept' | 'rebase' | 'opinion' | 'review'; onPress: () => void } | null;
  /** The session it heads, as the frame holds it; its acts and its facts are worked out from it by the frame. */
  session: Session;
  /**
   * What the session is for, as `sessionTitle` names it (its quest's short title where there is one): one line here, whole
   * in its tip and in the record's *Details*, under the head.
   */
  title: string;
  /** Its facts line (`sessionFacts`), said in this order; none draws no line. */
  facts?: readonly string[];
  /** The facts line's tip (`sessionFactsTip`): the running call's whole title, where the line says it short. */
  factsTip?: string | null;
  /** Its shown word: the record's state, *idle*, or the reader's *parked* or *awaiting reply*. */
  shown: ShownState;
  /** The acts the one rule offers it in this header, in the rule's order. */
  acts: readonly SessionActId[];
  /** Its loud act, where the state has one (`primaryAct`). */
  primary?: SessionActId | null;
  /** An act is on its way: its buttons wait for it. */
  busy?: boolean;
  /** An act pressed. Absent, nothing can act (a story), and no act is drawn. */
  onAct?: (act: SessionActId) => void;
  /** What asks under the header: the stop's sentence and its second press. */
  asking?: ReactNode;
}) {
  const { t } = useTranslation();
  const own = useRef<HTMLDivElement>(null);
  // The head spans the main area (its gutters undone), so its width is the main area's; unmeasured, the words are drawn.
  const width = useWidth(own);
  const narrow = width !== undefined && width <= 640;
  const offered = onAct ? acts : [];
  // The one split the page's right-click offers whole, so the header and the menu never disagree.
  const { lead, stop, rest } = headerActs(offered, primary);

  // Its height, told to the scroll box it is pinned in, so what pins beneath it (the long run's toolbar) sits below it.
  useLayoutEffect(() => {
    const header = own.current;
    const box = header?.parentElement;
    if (!header || !box) return undefined;
    const tell = () => box.style.setProperty('--session-head', `${header.offsetHeight}px`);
    tell();
    const observer = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(tell);
    observer?.observe(header);
    return () => {
      observer?.disconnect();
      box.style.removeProperty('--session-head');
    };
  }, []);

  return (
    // Pinned across the main area's gutters (`ViewMain`'s session gutters, 16 by 12 px), on the page's own surface so
    // the conversation scrolls under it rather than through it.
    // The sticky box holds PageHead (its grid: the acts hold still by the main area's width) and what asks under it;
    // PageHead's own bottom margin is the box's padding instead.
    <div
      ref={own}
      className="sticky -top-3 z-10 -mx-4 -mt-3 mb-2 border-b border-line bg-page px-4 pb-2.5 pt-3 [&>header]:mb-0"
    >
      <PageHead
        title={title}
        clamp={1}
        wholeElsewhere
        actsBeside
        lead={<Pill tone={SESSION_TONE[shown]}>{t(shownKey(shown))}</Pill>}
        facts={facts}
        // One line: a running call is said in a few words there (`callHead`), and whole in the line's tip.
        factsOneLine
        factsTip={factsTip ?? undefined}
        // Started under full access, by its record's pauses: the warning chip, last on the line, so the
        // line is cut before it rather than cutting it. Any other start wears nothing.
        // The choice its actor ran where it is not the first, before the access chip.
        fact={line || next || chip || choiceChip(t, session) !== null || startedFull(session)
          ? (
            <span className="inline-flex items-center gap-1.5">
              {line && (
                <Tip content={t('work.parts.line.tip')}>
                  <span data-line-state="" className="inline-flex items-baseline gap-1.5 text-small text-ink">
                    {line.said}
                    {line.onOpen && (
                      <button type="button" onClick={line.onOpen} className="cursor-pointer rounded-control border-0 bg-transparent p-0 text-small text-ink-open underline-offset-2 hover:underline">
                        {t('work.parts.line.open')}
                      </button>
                    )}
                  </span>
                </Tip>
              )}
              {next && (
                <Tip content={t('work.wait.toWait', { names: t(`work.wait.name.${next.press === 'answer' ? 'question' : next.press}`) })}>
                  <button type="button" onClick={next.onPress} className="cursor-pointer rounded-control border-0 bg-transparent p-0 text-small text-ink-open underline-offset-2 hover:underline">
                    {t('work.facts.nextIs', { press: t(`work.facts.press.${next.press}`) })}
                  </button>
                </Tip>
              )}
              {chip && (
                <Tip content={t('work.supervision.chipTip', { at: chip.at, n: chip.n })}>
                  <span data-supervision-chip={chip.held ? 'held' : 'near'} className={cn('text-small', chip.held ? 'font-medium text-ink' : 'text-ink-faint')}>
                    {chip.text}
                  </span>
                </Tip>
              )}
              <ChoiceChip session={session} narrow={narrow} />
              {startedFull(session) && <AccessChip />}
            </span>
          )
          : undefined}
        // One group names the buttons together, the ⋯ with them (its name is how the page's tests and readers find them), so
        // the ⋯ is inside `acts` rather than PageHead's `more` slot: the two sit in one flex row either way, and *Stop…* is
        // never PageHead's `fold`.
        acts={offered.length > 0 && (
          <div role="group" aria-label={t('work.head.acts')} className="flex items-center gap-2">
            {lead && (
              <Button variant="primary" disabled={busy} onClick={() => onAct?.(lead)}>{t(ACT_LOOK[lead].label)}</Button>
            )}
            {stop && (narrow
              ? (
                <Tip content={t(ACT_LOOK.stop.label)}>
                  <Button
                    variant="danger" disabled={busy} aria-label={t(ACT_LOOK.stop.label)} onClick={() => onAct?.('stop')}
                    className="h-[1.9rem] w-[1.9rem] justify-center px-0"
                  >
                    <Icon name={ACT_LOOK.stop.icon} size={15} />
                  </Button>
                </Tip>
              )
              : <Button variant="danger" disabled={busy} onClick={() => onAct?.('stop')}>{t(ACT_LOOK.stop.label)}</Button>)}
            {rest.length > 0 && (
              <Menu.Root>
                <Menu.Trigger asChild>
                  <Button variant="ghost" aria-label={t('work.head.more')} className="h-[1.9rem] w-[1.9rem] justify-center px-0">
                    <Icon name="more" size={15} />
                  </Button>
                </Menu.Trigger>
                <Menu.Content side="bottom" align="end" className="min-w-48">
                  <Menu.Acts acts={actMenu(rest, t, (act) => onAct?.(act), false, session)} />
                </Menu.Content>
              </Menu.Root>
            )}
          </div>
        )}
      />

      {asking}
    </div>
  );
}

/**
 * **A stop's ask**: it ends work in flight, so it asks once, under the header (a
 * destructive edit asks once). It says what follows by what the session is, then the move, *Stop session*, and *Never
 * mind*. The sentence is the catalogue's, and its backticks are code (`Inline`). It is the one inline
 * confirmation: open until the stop lands, a refusal said inside it.
 */
export function StopAsk({ sentence, busy = false, onStop, onClose }: {
  sentence: string;
  /** A stop on its way: the presses wait for it. */
  busy?: boolean;
  /** The second press, told how the stop ended. */
  onStop: (answered: Answered) => void;
  /** Put down, or the stop landed. */
  onClose: () => void;
}) {
  const { t } = useTranslation();
  return (
    <InlineConfirm
      className="mt-2.5"
      label={t('work.stop.title')}
      says={<Inline text={sentence} />}
      meanIt={t('work.act.stopMeanIt')}
      busy={busy}
      onConfirm={onStop}
      onClose={onClose}
    />
  );
}

/**
 * **A delete's ask**: nothing brings a deleted conversation back, so it asks once, under the header
 * as the stop's ask does: what goes, then *Delete session* and *Never mind*. The page offers *Delete…* only where the
 * driver said it would be taken, so the sentence is one for every session that reaches it. The one inline confirmation.
 */
export function DeleteAsk({ busy = false, onDelete, onClose }: {
  /** A delete on its way: the presses wait for it. */
  busy?: boolean;
  /** The second press, told how the delete ended. */
  onDelete: (answered: Answered) => void;
  /** Put down, or the delete landed. */
  onClose: () => void;
}) {
  const { t } = useTranslation();
  return (
    <InlineConfirm
      className="mt-2.5"
      label={t('work.delete.title')}
      says={t('work.delete.means')}
      meanIt={t('work.act.deleteMeanIt')}
      busy={busy}
      onConfirm={onDelete}
      onClose={onClose}
    />
  );
}
