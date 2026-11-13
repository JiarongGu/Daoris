import type * as React from 'react';
import { useCallback } from 'react';
import { useAnsweredRead, useMessageAnnouncements } from '../../notifications/announce';
import { whoWords } from '../../notifications/Messages';
import { HELP_REPOSITORY } from '../../data/api';
import { type StarterDoor } from '../../help/starters';
import { askItem, doorOpening, type Opening, type OpenPart } from '../opener';
import { type FrameIntent, type View } from '../../menus/commands';
import { type ListPanes } from '../../work/frame/listPanes';
import { type TFunction } from 'i18next';
import { type AskMessages, type Notice } from '../../bridge/notices';
import { type UseMutationResult } from '@tanstack/react-query';

export function useDoors({ lists, open, setWorkIntent, t, messages, view, attending, markNoticesRead, openHelp, apply, circle }: {
  lists: ListPanes;
  open: (target: View, item?: string | null | undefined, part?: OpenPart | undefined) => void;
  setWorkIntent: React.Dispatch<React.SetStateAction<FrameIntent | null>>;
  t: TFunction<"translation", undefined>;
  messages: { asks: AskMessages[] | undefined; answered: Notice[]; };
  view: View;
  attending: string | null;
  markNoticesRead: UseMutationResult<boolean, Error, string[], unknown>;
  openHelp: () => void;
  apply: (planned: Opening) => void;
  circle: string | null;
}) {
  // The attended session is remembered alongside the view, so a relaunch into Sessions reopens
  // what the person watched rather than an empty column.
  const { choose } = lists;
  const setAttending = useCallback((next: string | null) => choose('sessions', next), [choose]);

  // A door from a record into the session itself. The selection lives here rather than inside the
  // view precisely so a door can name which session it is opening (one selection, every
  // region) — the view change alone would land the person on whatever they last attended.
  const openInWork = (session: string) => open('sessions', session);
  // A door into a run: its session attended in Sessions, the Workflow view opened on it.
  const openRun = (session: string) => { open('sessions', session); setWorkIntent('workflow'); };
  const openReview = (run: string, session?: string | null) => { open('sessions', session ?? attending); setWorkIntent(`outputs/${run}`); };
  // A line of work of an ask folded to a count: its own view, the session attended with *At work* open on it.
  const openAtWork = (session: string) => { open('sessions', session); setWorkIntent('atWork'); };
  // A quest's record and an ask's, where quests are read: an ask is named as one, since Quests' list holds both.
  const openQuest = (id: string) => open('quests', id);
  // A row in Overview's band is a door into whatever is waiting, wherever that exists. A parked
  // session opens in Sessions, which only a shell has. An ask opens its record, where it is answered,
  // and a quest nobody can take opens its own page. Both of those a browser has too.
  const openAsk = (id: string) => open('quests', askItem(id));
  // An agent's or a teammate's urgent message to everyone, said by a toast in this window while it is the one focused,
  // once per message, its *Open* the ask's channel on its page; and a conversation's answer read where its conversation is on screen.
  const messageAnnouncements = useMessageAnnouncements({
    words: (arrived) => t('notifications.messages.toast', { who: whoWords(arrived.newest, t), line: arrived.newest.line }),
    openLabel: t('notifications.toast.open'),
    onOpen: (arrived) => openAsk(arrived.ask),
  });
  useAnsweredRead({
    answered: messages.answered,
    attended: view === 'sessions' ? attending : null,
    onRead: (ids) => markNoticesRead.mutate(ids),
  });
  // A conversation's answer opens the conversation: Ask Daoris's in its region, any other on Sessions.
  const openAnswer = (session: string, repository?: string) => (repository === HELP_REPOSITORY ? openHelp() : openInWork(session));
  // Where a starter's, a setup step's or Ask Daoris's door leads: a view and the item
  // it names, a domain of Settings at the part it names, or one of the Workspace menu's drawers on Repositories.
  const go = (door: StarterDoor) => apply(doorOpening(door, circle));
  return { openInWork, openRun, openReview, openAtWork, openQuest, openAsk, go, messageAnnouncements, setAttending, openAnswer };
}
