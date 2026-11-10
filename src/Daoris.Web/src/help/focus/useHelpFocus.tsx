import type * as React from 'react';
import { type ReactNode, useEffect, useState } from 'react';
import { workspaceItem } from '../../app/opener';
import { useHelpFocus as useFocusPress } from '../../bridge/help';
import { useRegistry } from '../../data/queries';
import { type Session } from '../../data/api';
import { sentence } from '../../locales/format';
import { homeFocus, MACHINE, type Place, samePlace } from '../focus';
import { type HelpConversationRow, sameWorkspace } from '../history';
import { type StarterDoor } from '../starters';
import { FocusCrumb } from './FocusCrumb';
import { FocusPicker } from './FocusPicker';
import { type PlaceRepository } from './focusPlaces';
import { keepFocusRecent, readFocusRecent } from './focusRecent';

/**
 * Ask Daoris's focus in the panel (`docs/2026-11-11-ask-scope-ux-design.md` §1-2): the chip for the composer bar and the line
 * under the open conversation's title. A choice goes to the driver at once (`HELP_FOCUS`) for the conversation shown, or for the
 * one opened ahead before any words; one the driver answers by ending what it opened ahead (`reopens`) is opened ahead again
 * under it. Chosen with no conversation to send it to, it waits for the start, which carries it (`startFocus`), as does a new
 * conversation started from one shown (`inherit`).
 *
 * @param window The window's workspace; null for a window on every workspace.
 * @param readied The conversation opened ahead that nobody has spoken in, while it runs.
 */
export function useHelpFocus({ shown, known, shownIn, here, window, readied, setRound, onGo }: {
  shown: Session | null;
  known: HelpConversationRow | null;
  shownIn: string | null;
  here: string | undefined;
  window: string | null;
  readied: string | null;
  setRound: React.Dispatch<React.SetStateAction<number>>;
  onGo: (door: StarterDoor) => void;
}): {
  chip: ReactNode; crumb: ReactNode; startFocus: () => Place | undefined; inherit: () => void; take: (place: Place, into: string | null) => void;
} {
  const registry = useRegistry('machine');
  const press = useFocusPress();
  const [ahead, setAhead] = useState<Place | null>(null);
  const [recent, setRecent] = useState(readFocusRecent);
  const [refused, setRefused] = useState<string | null>(null);
  // A conversation shown has its own focus on its file: what was chosen ahead of it went with its start.
  useEffect(() => {
    if (shown) setAhead(null);
    setRefused(null);
  }, [shown?.id]);

  const repositories: PlaceRepository[] | undefined = Array.isArray(registry.data)
    ? registry.data.map((row) => ({ name: row.repository, workspace: row.workspace || 'default', checkout: Boolean(row.root) }))
    : registry.isError ? [] : undefined;
  const fallback = window === null || here === undefined ? MACHINE : homeFocus(here);
  const focus = shown ? known?.focus ?? homeFocus(shownIn) : ahead ?? fallback;
  const next = shown ? known?.focusNext ?? null : null;

  // A place the registry no longer holds is gone: the next words widen it to its workspace, or to this machine.
  const holds = (place: Place) => repositories === undefined || place.kind === 'machine'
    || repositories.some((each) => sameWorkspace(each.workspace, place.workspace)
      && (place.kind === 'workspace' || each.name.toLowerCase() === place.repository.toLowerCase()));
  const gone = !holds(focus);
  const wider: Place = focus.kind === 'repository' && holds({ kind: 'workspace', workspace: focus.workspace })
    ? { kind: 'workspace', workspace: focus.workspace } : MACHINE;

  const send = (session: string, place: Place) => press.mutate({ session, focus: place }, {
    onSuccess: (answer) => {
      if (answer.refused) setRefused(answer.said);
      else if (answer.reopens) setRound((was) => was + 1);
    },
    onError: (error) => setRefused(sentence(error)),
  });
  // For the conversation `into`, or, where none, the next to start: kept ahead and sent to the one opened ahead.
  const take = (place: Place, into: string | null) => {
    setRefused(null);
    if (into !== null) {
      send(into, place);
      return;
    }
    setAhead(samePlace(place, fallback) ? null : place);
    if (readied) send(readied, place);
  };
  const choose = (place: Place) => {
    setRecent(keepFocusRecent(place));
    take(place, shown?.id ?? null);
  };

  const chip = (
    <div className="grid min-w-0 gap-0.5 @max-[24rem]:basis-full">
      <FocusPicker
        focus={focus}
        next={next}
        window={window}
        repositories={repositories}
        recent={recent}
        gone={gone}
        wider={gone ? wider : null}
        onChoose={choose}
      />
      {refused && <p role="alert" className="m-0 text-small text-danger wrap-anywhere">{refused}</p>}
    </div>
  );
  const crumb = shown ? (
    <FocusCrumb
      focus={focus}
      next={next}
      gone={gone}
      window={window}
      home={shownIn ?? 'default'}
      onOpen={(place) => {
        if (place.kind !== 'machine') onGo({ view: 'projects', item: place.kind === 'workspace' ? workspaceItem(place.workspace) : place.repository });
      }}
    />
  ) : null;

  return {
    chip,
    crumb,
    startFocus: () => (shown ? undefined : ahead ?? undefined),
    inherit: () => {
      if (shown) setAhead(samePlace(focus, fallback) ? null : focus);
    },
    take,
  };
}
