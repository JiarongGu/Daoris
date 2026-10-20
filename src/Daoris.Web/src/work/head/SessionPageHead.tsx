import { type ReactNode, useLayoutEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import type { Session } from '../../data/api';
import { Button, Icon, Inline, Menu, Pill, SESSION_TONE, type ShownState, shownKey } from '../../design/ui';
import { AccessChip, startedFull } from '../AccessFact';
import { ACT_LOOK, actMenu, headerActs, type SessionActId } from '../acts';
import { type Answered, InlineConfirm } from '../InlineConfirm';
import { PageHead } from '../frame/ViewMain';

/**
 * **A session's page header** (SESSUX1d, D126 §3.2): Sessions' main area gains the header every view's chosen item has
 * (D118 §3b), pinned at the top while the conversation scrolls under it, since a session's page runs to thousands of
 * events and its acts must stay in reach there.
 *
 * @remarks
 * **A molecule: every state is reached by its props** (components plan §2). Which acts it offers is the one rule's
 * (`offeredActs`), handed in; a press is reported, and the one owner (`sessionActs.ts`) carries it out.
 *
 * - **On the left** (UX7c, D152 §7, the UX7 design §5.2): its shown word on its pill first, so the word stays in one place
 *   whatever the title's length; its title on one line, whole in its tip; and under them the facts its state makes matter,
 *   handed in (`sessionFacts`), faint, ending in the *Full access* chip where its record says it started so (SESSACCESS1).
 *   Its id is not a line here: it is the ⋯'s *Copy session ID* and the record's *Details*.
 * - **On the right**: the primary act where the state has one (*Try again*, *Review*), then *Stop…* while it is live,
 *   then ⋯ with the rest in §3.1's order. *Answer…* is not here: the card under the header keeps the answer.
 * - **What asks under it** (the stop's ask, §3.3; the delete's, §5.4; the hand-over's, MIXAGENT1b-i) is pinned with it, since the ask is about the acts
 *   beside it.
 * - **It is `PageHead`'s** (HEAD2c, D162; the entry-and-heads design §2): the title takes one line and the acts hold still,
 *   a column of their own above 640 px of the main area and a right-aligned row under the facts at or below it, by the
 *   main area's width, never the window's. *Stop…* stays; it never folds. The quest's whole title is not repeated under the
 *   head: it is a row of the record's *Details*.
 *
 * **It says how tall it is** to the main area it is pinned in (`--session-head`), so the long run's toolbar (SESS1 S9)
 * pins beneath it rather than under it.
 */
export function SessionPageHead({ session, title, shown, facts = [], acts, primary = null, busy = false, onAct, asking }: {
  /** The session it heads, as the frame holds it; its acts and its facts are worked out from it by the frame. */
  session: Session;
  /**
   * What the session is for, as `sessionTitle` names it (its quest's short title where there is one): one line here, whole
   * in its tip and in the record's *Details*, under the head.
   */
  title: string;
  /** Its facts line (`sessionFacts`), said in this order; none draws no line. */
  facts?: readonly string[];
  /** Its shown word (D126 §2.2): the record's state, *idle*, or the reader's *parked* or *awaiting reply*. */
  shown: ShownState;
  /** The acts the one rule offers it in this header, in §3.1's order. */
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
  const offered = onAct ? acts : [];
  // The one split the page's right-click offers whole (CTX1), so the header and the menu never disagree.
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
    // The sticky box holds PageHead (HEAD2a's grid: the acts hold still by the main area's width) and what asks under it;
    // PageHead's own bottom margin is the box's padding instead.
    <div
      ref={own}
      className="sticky -top-3 z-10 -mx-4 -mt-3 mb-3 border-b border-line bg-page px-4 pb-2.5 pt-3 [&>header]:mb-0"
    >
      <PageHead
        title={title}
        clamp={1}
        lead={<Pill tone={SESSION_TONE[shown]}>{t(shownKey(shown))}</Pill>}
        facts={facts}
        // Started under full access, by its record's pauses (SESSACCESS1, D184 point 1): the warning chip, last on the line, so the
        // line wraps before it rather than cutting it. Any other start wears nothing.
        fact={startedFull(session) ? <AccessChip /> : undefined}
        // One group names the buttons together, the ⋯ with them (its name is how the page's tests and readers find them), so
        // the ⋯ is inside `acts` rather than PageHead's `more` slot: the two sit in one flex row either way, and *Stop…* is
        // never PageHead's `fold`.
        acts={offered.length > 0 && (
          <div role="group" aria-label={t('work.head.acts')} className="flex items-center gap-2">
            {lead && (
              <Button variant="primary" disabled={busy} onClick={() => onAct?.(lead)}>{t(ACT_LOOK[lead].label)}</Button>
            )}
            {stop && (
              <Button variant="danger" disabled={busy} onClick={() => onAct?.('stop')}>{t(ACT_LOOK.stop.label)}</Button>
            )}
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
 * **A stop's ask** (D126 §3.3): it ends work in flight, so it asks once, under the header (platform language §4: a
 * destructive edit asks once). It says what follows by what the session is, then the move, *Stop session*, and *Never
 * mind*. The sentence is the catalogue's, and its backticks are code (`Inline`). It is the one inline confirmation
 * (UXFIX2): open until the stop lands, a refusal said inside it.
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
 * **A delete's ask** (SESSUX1f, D126 §5.4): nothing brings a deleted conversation back, so it asks once, under the header
 * as the stop's ask does: what goes, then *Delete session* and *Never mind*. The page offers *Delete…* only where the
 * driver said it would be taken, so the sentence is one for every session that reaches it. The one inline confirmation
 * (UXFIX2).
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
