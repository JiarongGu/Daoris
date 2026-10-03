import { type ReactNode, useLayoutEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import type { Session } from '../api';
import { Button, Icon, Inline, Menu, Pill, SESSION_TONE, type ShownState, shownKey } from '../ui';
import { ACT_LOOK, actMenu, headerActs, type SessionActId } from './acts';

/**
 * **A session's page header** (SESSUX1d, D126 §3.2): Sessions' main area gains the header every view's chosen item has
 * (D118 §3b), pinned at the top while the conversation scrolls under it, since a session's page runs to thousands of
 * events and its acts must stay in reach there.
 *
 * @remarks
 * **A molecule: every state is reached by its props** (components plan §2). Which acts it offers is the one rule's
 * (`offeredActs`), handed in; a press is reported, and the one owner (`sessionActs.ts`) carries it out.
 *
 * - **On the left**: its title on one line, whole in its tip, its shown word on its pill, and its id in the meta face.
 * - **On the right**: the primary act where the state has one (*Try again*, *Review*), then *Stop…* while it is live,
 *   then ⋯ with the rest in §3.1's order. *Answer…* is not here: the card under the header keeps the answer.
 * - **What asks under it** (the stop's ask, §3.3; the delete's, §5.4) is pinned with it, since the ask is about the acts
 *   beside it.
 * - **Below 560 px** its acts take their own line under the title, as the plugin page's do (D119 §3.6): by the main
 *   area's width, never the window's.
 *
 * **It says how tall it is** to the main area it is pinned in (`--session-head`), so the long run's toolbar (SESS1 S9)
 * pins beneath it rather than under it.
 */
export function SessionPageHead({ session, title, shown, acts, primary = null, busy = false, onAct, asking }: {
  session: Session;
  /** What the session is for, as `sessionTitle` names it: one line here, whole in the record head below. */
  title: string;
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
  const own = useRef<HTMLElement>(null);
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
    <header
      ref={own}
      className="sticky -top-3 z-10 -mx-4 -mt-3 mb-3 border-b border-line bg-page px-4 pb-2.5 pt-3"
    >
      <div className="flex flex-wrap items-start justify-between gap-x-4 gap-y-2">
        <div className="min-w-0 flex-1 basis-64">
          <div className="flex min-w-0 items-baseline gap-2">
            <h1 title={title} className="m-0 min-w-0 truncate text-title font-[650] leading-[1.35]">{title}</h1>
            <span className="shrink-0"><Pill tone={SESSION_TONE[shown]}>{t(shownKey(shown))}</Pill></span>
          </div>
          <p className="m-0 mt-0.5 font-mono text-meta text-ink-faint">{session.id}</p>
        </div>

        {offered.length > 0 && (
          <div
            role="group"
            aria-label={t('work.head.acts')}
            className="flex shrink-0 flex-wrap items-center gap-2 @max-[35rem]/main:basis-full"
          >
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
                  <Menu.Acts acts={actMenu(rest, t, (act) => onAct?.(act))} />
                </Menu.Content>
              </Menu.Root>
            )}
          </div>
        )}
      </div>

      {asking}
    </header>
  );
}

/**
 * **A stop's ask** (D126 §3.3): it ends work in flight, so it asks once, under the header (platform language §4: a
 * destructive edit asks once). It says what follows by what the session is, then the move, *Stop session*, and *Never
 * mind*. The sentence is the catalogue's, and its backticks are code (`Inline`).
 */
export function StopAsk({ sentence, busy = false, onStop, onCancel }: {
  sentence: string;
  /** A stop on its way: the presses wait for it. */
  busy?: boolean;
  onStop: () => void;
  onCancel: () => void;
}) {
  const { t } = useTranslation();
  return (
    <div
      role="group"
      aria-label={t('work.stop.title')}
      className="mt-2.5 flex flex-wrap items-center gap-2 rounded-control border border-line bg-sunken px-2.5 py-2"
    >
      <span className="min-w-0 flex-1 basis-64 text-small text-ink-soft"><Inline text={sentence} /></span>
      <Button variant="danger" disabled={busy} onClick={onStop}>{t('work.act.stopMeanIt')}</Button>
      <Button variant="ghost" disabled={busy} onClick={onCancel}>{t('common.cancel')}</Button>
    </div>
  );
}

/**
 * **A delete's ask** (SESSUX1f, D126 §5.4): nothing brings a deleted conversation back, so it asks once, under the header
 * as the stop's ask does: what goes, then *Delete session* and *Never mind*. The page offers *Delete…* only where the
 * driver said it would be taken, so the sentence is one for every session that reaches it.
 */
export function DeleteAsk({ busy = false, onDelete, onCancel }: {
  /** A delete on its way: the presses wait for it. */
  busy?: boolean;
  onDelete: () => void;
  onCancel: () => void;
}) {
  const { t } = useTranslation();
  return (
    <div
      role="group"
      aria-label={t('work.delete.title')}
      className="mt-2.5 flex flex-wrap items-center gap-2 rounded-control border border-line bg-sunken px-2.5 py-2"
    >
      <span className="min-w-0 flex-1 basis-64 text-small text-ink-soft">{t('work.delete.means')}</span>
      <Button variant="danger" disabled={busy} onClick={onDelete}>{t('work.act.deleteMeanIt')}</Button>
      <Button variant="ghost" disabled={busy} onClick={onCancel}>{t('common.cancel')}</Button>
    </div>
  );
}
