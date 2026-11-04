import type * as React from 'react';
import { type ReactNode } from 'react';
import { sentence } from '../../locales/format';
import { Button } from '../../design/ui';
import { type HelpConversationRow, sameWorkspace } from '../history';
import { accountNames } from '../../work/accountNames';
import { namer } from '../../settings/accounts/namer';
import { type ChatLimitHeld, chatLimitWaitSaid, limitWaitOf } from '../../work/waits/limitWait';
import { LimitWaitLine } from '../../work/waits/LimitWaitLine';
import { type Session } from '../../data/api';
import { type UseQueryResult, type UseMutationResult } from '@tanstack/react-query';
import { type HarnessRoster } from '../../bridge/agents/agents';
import { type TFunction, type i18n } from 'i18next';
import { type AccountsAnswer, type AccountUseChange } from '../../settings/accounts/accounts';
import { type StopAnswer } from '../../bridge/sessions/sessions';
import { type StoppedWaiting } from '../../bridge/sessions/conversation';

export function useHelpSlotParts({ shown, limitHeld, roster, t, shownIn, accounts, i18n, startFrom, stop, stopWaiting, accountUse, limitRefusal, elsewhere, beginFrom, live, setLimitRefusal, startedFrom, line, ended, picked, checking, goesOn, onNew, shownRow, press, choose, setFocusBox, registered, choosing, start, startChosen, setChoosing, here, setWorkspace }: {
  shown: Session | null;
  limitHeld: { session: string; held: ChatLimitHeld; } | null;
  roster: UseQueryResult<HarnessRoster, Error>;
  t: TFunction<"translation", undefined>;
  shownIn: string | null;
  accounts: UseQueryResult<AccountsAnswer, Error>;
  i18n: i18n;
  startFrom: UseMutationResult<{ sessionId: string | null; message: string; from: string; }, Error, { id: string; workspace?: string | undefined; }, unknown>;
  stop: UseMutationResult<StopAnswer, Error, string, unknown>;
  stopWaiting: UseMutationResult<StoppedWaiting, Error, string, unknown>;
  accountUse: UseMutationResult<{ harness: string; action: string; profile?: string | null | undefined; ended?: boolean | undefined; }, Error, { harness: string; action: "said" | "order" | "use" | "ready"; accounts?: string[] | undefined; profile?: string | undefined; ids?: string[] | undefined; } & AccountUseChange, unknown>;
  limitRefusal: { session: string; sentence: string; } | null;
  elsewhere: boolean;
  beginFrom: (id: string, chosenIn?: string | undefined) => void;
  live: boolean;
  setLimitRefusal: React.Dispatch<React.SetStateAction<{ session: string; sentence: string; } | null>>;
  startedFrom: { session: string; title: string; } | null;
  line: (key: string, values?: Record<string, string> | undefined) => React.JSX.Element;
  ended: boolean;
  picked: Session | null;
  checking: boolean;
  goesOn: boolean;
  onNew: () => void;
  shownRow: HelpConversationRow | null;
  press: (key: string, onClick: () => void) => React.JSX.Element;
  choose: (id: string) => void;
  setFocusBox: React.Dispatch<React.SetStateAction<number>>;
  registered: string[] | undefined;
  choosing: { from?: string | undefined; unknown?: boolean | undefined; } | null;
  start: UseMutationResult<{ sessionId: string | null; message: string; running?: boolean | undefined; starting?: boolean | undefined; finishing?: boolean | undefined; }, Error, string | undefined, unknown>;
  startChosen: (circle: string) => void;
  setChoosing: React.Dispatch<React.SetStateAction<{ from?: string | undefined; unknown?: boolean | undefined; } | null>>;
  here: string | null | undefined;
  setWorkspace: (workspace: string | null) => void;
}) {
  // LIMITRESUME1d2 (D206 §3.7): the conversation shown waits for its account's limit, as its events say, above the box. Its way out
  // now is Ask Daoris's own door (`ResumeWords.HelpDoor`): a new conversation from this one, never D163's hand-over, which refuses
  // Ask Daoris; *Stop…* while it runs, *Stop waiting* once it ended, and the account's *Try now*.
  const heldShown = shown && limitHeld?.session === shown.id ? limitHeld.held : null;
  const harnessRows = Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : [];
  const limitLines = shown && heldShown
    ? chatLimitWaitSaid(t, {
      held: heldShown,
      wait: limitWaitOf({ session: shown, workspace: shownIn, doors: harnessRows, answer: accounts.data, now: new Date() }),
      label: (account) => namer(t, harnessRows)(shown.adapter, account),
      language: i18n.language,
      names: accountNames(harnessRows),
      now: new Date(),
    })
    : null;
  const limitLine = shown && limitLines ? (
    <LimitWaitLine
      key={`limit:${shown.id}`}
      lines={limitLines}
      busy={startFrom.isPending || stop.isPending || stopWaiting.isPending || accountUse.isPending}
      refusal={limitRefusal?.session === shown.id ? limitRefusal.sentence : null}
      onGoOnNow={elsewhere ? undefined : () => beginFrom(shown.id)}
      onStop={live ? () => stop.mutate(shown.id) : undefined}
      onStopWaiting={live ? undefined : () => {
        setLimitRefusal(null);
        stopWaiting.mutate(shown.id, {
          onSuccess: (answer) => {
            if (!answer.stopped) setLimitRefusal({ session: shown.id, sentence: answer.message });
          },
          onError: (error) => setLimitRefusal({ session: shown.id, sentence: sentence(error) }),
        });
      }}
      onTryNow={() => {
        setLimitRefusal(null);
        const account = limitWaitOf({ session: shown, workspace: shownIn, doors: harnessRows, answer: accounts.data, now: new Date() })
          ?.account ?? shown.profile ?? null;
        accountUse.mutate(
          { harness: shown.adapter, action: 'ready', ...(account ? { profile: account } : {}) },
          { onError: (error) => setLimitRefusal({ session: shown.id, sentence: sentence(error) }) },
        );
      }}
    />
  ) : null;
  const own: ReactNode = !shown ? undefined
    : startedFrom?.session === shown.id ? line('help.startedFrom', { title: startedFrom.title })
      : !ended ? undefined
        : picked !== null ? (
          checking ? <p role="status" className="m-0 text-small text-ink-soft">{t('help.checking')}</p>
            : goesOn ? line('help.endedGoesOn')
              : (
                <>
                  {line('help.endedAnew')}
                  <div className="flex flex-wrap gap-2">
                    <Button variant="primary" disabled={startFrom.isPending} onClick={() => beginFrom(shown.id)}>
                      {t('help.history.startFrom')}
                    </Button>
                    <Button disabled={startFrom.isPending} onClick={onNew}>{t('help.new')}</Button>
                  </div>
                </>
              )
        )
          // The newest that ended, shown by default: a message starts a new one (HELP5), and it may be gone on in instead.
          : shownRow ? (
            <>
              {line('help.ended')}
              {shownRow.resumable ? press('help.goOn', () => { choose(shown.id); setFocusBox((was) => was + 1); })
                : press('help.history.startFrom', () => beginFrom(shown.id))}
            </>
          ) : undefined;

  // ASKSCOPE1d (D158's ASKSCOPE1 note, points 1 and 6), ahead of what the next words do: which workspace a start on a window
  // that shows several is in, asked with a press per workspace; another workspace's conversation named, with the switch that
  // goes on in it, where this window can show that workspace; and on a window that shows every workspace, whose each is.
  const switchable = shownIn !== null && registered?.find((name) => sameWorkspace(name, shownIn));
  const goesOnThere = elsewhere && (live || goesOn || (picked !== null && checking));
  const note: ReactNode = choosing ? (
    <>
      {line('help.scope.which')}
      <div className="flex flex-wrap gap-2">
        {registered === undefined
          ? <p role="status" className="m-0 text-small text-ink-soft">{t('help.scope.reading')}</p>
          : registered.map((name) => (
            <Button key={name} disabled={start.isPending || startFrom.isPending} onClick={() => startChosen(name)}>
              {t('help.scope.startIn', { workspace: name })}
            </Button>
          ))}
        <Button variant="ghost" onClick={() => setChoosing(null)}>{t('common.cancel')}</Button>
      </div>
    </>
  ) : elsewhere ? (
    <>
      {line('help.scope.elsewhere', { workspace: shownIn!, here: here as string })}
      {!goesOnThere ? own : switchable ? (
        <Button
          variant="primary"
          onClick={() => {
            setWorkspace(switchable);
            setFocusBox((was) => was + 1);
          }}
        >
          {t('help.scope.switch', { workspace: switchable })}
        </Button>
      ) : (
        <>
          {line('help.scope.notOffered', { workspace: shownIn!, here: here as string })}
          {press('help.history.startFrom', () => beginFrom(shown!.id))}
        </>
      )}
    </>
  ) : shown && here === null ? (
    <>
      {line('help.scope.in', { workspace: shownIn! })}
      {own}
    </>
  ) : own;
  return { limitLine, note };
}
