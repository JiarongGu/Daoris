import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { HELP_REPOSITORY } from '../../data/api';
import { useHelpSessions, useWorkspaces } from '../../data/queries';
import { useScope } from '../../data/scope';
import { DEFAULT_WORKSPACE, workspaceOf } from '../../data/workspaces';
import { useHelpReadied, usePollUnlessTold } from '../../bridge/shell';
import { SESSION_ACTIVE } from '../../design/ui';
import { sameWorkspace } from '../history';
import { GOING_ON_POLL } from './askTypes';

export function useHelpPlace() {
  const { t, i18n } = useTranslation();
  // The workspace the panel's conversations are in: the window's; where it
  // shows every workspace, the one there is, or `default` where none is registered. Null where it shows several, so a start
  // asks which; undefined while this machine's workspaces are still being read. One unreadable is none registered.
  const { workspace: scoped, setWorkspace } = useScope();
  const circles = useWorkspaces();
  const registered = circles.data ?? (circles.isError ? [] : undefined);
  const here: string | null | undefined = scoped
    ?? (registered === undefined ? undefined : registered.length > 1 ? null : registered[0] ?? DEFAULT_WORKSPACE);
  const inHere = (workspace?: string) => typeof here === 'string' && sameWorkspace(workspace ?? DEFAULT_WORKSPACE, here);
  // What a start names, read at its press: the workspace it is in, or none where the window chose none and none is registered,
  // which is `default`'s. Never `default` read for a window that shows several.
  const startIn = (workspace: string) => (scoped === null && registered?.length === 0 ? undefined : workspace);
  // The window's workspace now, for an answer that arrives after it moved.
  const hereNow = useRef(here);
  useEffect(() => {
    hereNow.current = here;
  }, [here]);
  // The conversation the person chose from the history, or went on in: shown in the newest's place.
  const [chosen, setChosen] = useState<string | null>(null);
  // Words said to an ended conversation, waiting for its record to go on: the list is asked again until it has.
  const [goingOn, setGoingOn] = useState<string | null>(null);
  // A desktop window is told `sessions` as the driver takes the words up, so only a window with no bus reads on a clock.
  const goingOnPoll = usePollUnlessTold(GOING_ON_POLL);
  const sessions = useHelpSessions(goingOn ? goingOnPoll : false);
  // The session the person started again from: shown no longer, whatever the list still says.
  const [cleared, setCleared] = useState<string | null>(null);
  // The conversation opened as the panel showed, which nobody has spoken in: not shown, so the
  // starters, or the conversation that ended, stay in front of the person until the first words go to it.
  const help = useHelpReadied();
  // The one opened ahead in this window's workspace: one opened in another never takes this one's words.
  const readied = typeof here === 'string' ? help.readiedIn(here) : null;
  const helpSessions = (sessions.data ?? []).filter((session) => session.repository === HELP_REPOSITORY);
  // The window's workspace's conversations: its newest is shown, and its running one carried on. A window on every
  // workspace shows the newest of any, named; one whose workspaces are still being read shows none yet.
  const mine = here === null ? helpSessions : helpSessions.filter((session) => inHere(session.workspace));
  const newest = mine
    .filter((session) => !help.unspoken.includes(session.id))
    .sort((a, b) => b.created.localeCompare(a.created))[0] ?? null;
  const picked = chosen ? helpSessions.find((session) => session.id === chosen) ?? null : null;
  const shown = picked ?? (newest && newest.id !== cleared ? newest : null);
  const live = shown ? SESSION_ACTIVE.has(shown.state) : false;
  // The workspace the conversation shown started in; another workspace's is read here, named, and gone on in only once the
  // window has switched to it: words are never taken up in another workspace from this one.
  const shownIn = shown ? workspaceOf(shown) : null;
  const elsewhere = shown !== null && typeof here === 'string' && !inHere(shown.workspace);
  useEffect(() => {
    if (goingOn && shown?.id === goingOn && live) setGoingOn(null);
  }, [goingOn, shown?.id, live]);
  return { here, registered, chosen, setChosen, shown, live, picked, helpSessions, readied, t, setGoingOn, sessions, help, setCleared, hereNow, elsewhere, startIn, mine, newest, shownIn, i18n, setWorkspace };
}
