import type * as React from 'react';
import { type ReactNode } from 'react';
import { sentence } from '../../locales/format';
import { Button } from '../../design/ui';
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

export function useHelpSlotParts({ shown, limitHeld, roster, t, shownIn, accounts, i18n, startFrom, stop, stopWaiting, accountUse, limitRefusal, beginFrom, live, setLimitRefusal, startedFrom, line, choosing, setChoosing }: {
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
  accountUse: UseMutationResult<{ door: string; action: string; profile?: string | null | undefined; ended?: boolean | undefined; }, Error, { door: string; action: "said" | "order" | "use" | "ready"; accounts?: string[] | undefined; profile?: string | undefined; ids?: string[] | undefined; } & AccountUseChange, unknown>;
  limitRefusal: { session: string; sentence: string; } | null;
  beginFrom: (id: string, chosenIn?: string | undefined) => void;
  live: boolean;
  setLimitRefusal: React.Dispatch<React.SetStateAction<{ session: string; sentence: string; } | null>>;
  startedFrom: { session: string; title: string; } | null;
  line: (key: string, values?: Record<string, string> | undefined) => React.JSX.Element;
  choosing: { from?: string | undefined; unknown?: boolean | undefined; } | null;
  setChoosing: React.Dispatch<React.SetStateAction<{ from?: string | undefined; unknown?: boolean | undefined; } | null>>;
}) {
  // The conversation shown waits for its account's limit, as its events say, above the box. Its way out
  // is Ask Daoris's own door (`ResumeWords.HelpDoor`): a new conversation from this one, never the generic hand-over, which refuses
  // Ask Daoris; *Stop…* while it runs, *Stop waiting* once it ended, and the account's *Try now*.
  const heldShown = shown && limitHeld?.session === shown.id ? limitHeld.held : null;
  const harnessRows = Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : [];
  const limitLines = shown && heldShown
    ? chatLimitWaitSaid(t, {
      held: heldShown,
      wait: limitWaitOf({ session: shown, workspace: shownIn, doors: harnessRows, answer: accounts.data, now: new Date() }),
      label: (account) => namer(t, harnessRows)(shown.door, account),
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
      onGoOnNow={() => beginFrom(shown.id)}
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
          { door: shown.door, action: 'ready', ...(account ? { profile: account } : {}) },
          { onError: (error) => setLimitRefusal({ session: shown.id, sentence: sentence(error) }) },
        );
      }}
    />
  ) : null;
  // A conversation no reply runs in is open all the same: a stop ended the reply alone, and the next word continues it.
  const own: ReactNode = !shown ? undefined
    : startedFrom?.session === shown.id ? line('help.startedFrom', { title: startedFrom.title })
      : live ? undefined
        : line(shown.state === 'stopped' ? 'help.stopped' : 'help.continues');

  // Ahead of what the next words do: words that wait while this machine's workspaces are read, which go once they are.
  const note: ReactNode = choosing ? (
    <div className="flex flex-wrap items-center gap-2">
      <p role="status" className="m-0 text-small text-ink-soft">{t('help.scope.reading')}</p>
      <Button variant="ghost" onClick={() => setChoosing(null)}>{t('common.cancel')}</Button>
    </div>
  ) : own;
  return { limitLine, note };
}
