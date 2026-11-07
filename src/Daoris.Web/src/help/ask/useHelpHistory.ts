import type * as React from 'react';
import { useEffect, useState } from 'react';
import { DEFAULT_WORKSPACE } from '../../data/workspaces';
import { useHelpConversations, useChatReplaced } from '../../bridge/shell';
import { searchable } from '../../lib/searchable';
import { type HelpConversationRow, sameWorkspace } from '../history';
import { type Session } from '../../data/api';

export function useHelpHistory({ here, registered, helper, chosen, setChosen, shown, live, picked }: {
  here: string | null | undefined;
  registered: string[] | undefined;
  helper: string | null;
  chosen: string | null;
  setChosen: React.Dispatch<React.SetStateAction<string | null>>;
  shown: Session | null;
  live: boolean;
  picked: Session | null;
}) {
  // The history: open or not, its search, and each conversation's row, which says whether one that ended goes on
  // in itself. The panel's own rows are asked whenever an agent is named, so the conversation shown knows its own.
  const [historyOpen, setHistoryOpen] = useState(false);
  const [search, setSearch] = useState('');
  const [historyRefusal, setHistoryRefusal] = useState<string | null>(null);
  const words = search.trim();
  const searching = searchable(words);
  // Whose conversations the history lists: the window's workspace's, unless the person chose every
  // workspace's; a window on every workspace lists every one's. Its search runs over that scope alone. The choice is offered
  // where there is another workspace to see; a machine whose one workspace is `default` lists every one's, unlabelled, as before,
  // so a conversation of a workspace since gone is still reached, and named once it is opened.
  const [everyWorkspace, setEveryWorkspace] = useState(false);
  const offerEvery = typeof here === 'string' && ((registered?.length ?? 0) > 1 || !sameWorkspace(here, DEFAULT_WORKSPACE));
  const listIn = typeof here === 'string' && offerEvery && !everyWorkspace ? here : null;
  const historyScope = here === undefined ? undefined
    : here === null ? { workspace: null }
      : offerEvery ? { workspace: listIn, here, onEvery: setEveryWorkspace } : undefined;
  // Each a page at a time, every page read joined (ASKHIST1d2); asked once the window's workspace is known.
  const rows = useHelpConversations('', helper !== null && here !== undefined, listIn);
  // Follow a chosen conversation when the driver continues it on another account. A fresh start has no shared parts.
  useEffect(() => {
    if (!chosen) return;
    const continued = rows.data?.conversations.find((row) => row.session !== chosen && row.parts?.includes(chosen));
    if (continued) setChosen(continued.session);
  }, [chosen, rows.data]);
  // MODELROLE1d2 (D205 §3.5(b)): the conversation chosen, refused at its open, went on in its replacement within the same press.
  useChatReplaced((from, to) => setChosen((was) => (was === from ? to : was)));
  const found = useHelpConversations(words, helper !== null && here !== undefined && historyOpen && searching, listIn);
  const listing = searching ? found : rows;
  // The last list answered, held while a newer one comes, so a search typed never blanks the rows (UX §4; ASKHIST1c).
  const [lastListing, setLastListing] = useState<typeof rows.data>(undefined);
  useEffect(() => {
    if (listing.data) setLastListing(listing.data);
  }, [listing.data]);
  const shownListing = listing.data ?? lastListing;
  // A conversation's row (ASKHIST1d2): from the whole list's pages read, else the search's, which reaches every conversation
  // past them (ASKHIST1d1); else the row the person opened it from, once no list being asked can name it.
  const [openedRow, setOpenedRow] = useState<HelpConversationRow | null>(null);
  const listed = (id: string) => rows.data?.conversations.find((row) => row.session === id)
    ?? found.data?.conversations.find((row) => row.session === id) ?? null;
  const rowOf = (id: string) => listed(id) ?? (openedRow?.session === id ? openedRow : null);
  // Whether an ended one shown can go on in itself is the history's answer, so until it answers it is being checked:
  // read as "cannot", words typed under one that could would have started a fresh conversation without its own.
  const checking = shown !== null && listed(shown.id) === null && (rows.isPending || rows.isFetching);
  const known = shown ? rowOf(shown.id) : null;
  const shownRow = checking ? null : known;
  // An ended one the person chose goes on in itself with their words, where its own conversation was kept.
  const goesOn = shown !== null && !live && picked !== null && shownRow?.resumable === true;
  // One the person chose that has ended and does not go on, or is still being checked: read, and written in only once they
  // choose a new conversation from it, or a blank one.
  const readOnly = shown !== null && !live && picked !== null && !goesOn;
  return { goesOn, setHistoryOpen, setOpenedRow, shownListing, setHistoryRefusal, rowOf, listing, search, historyScope, historyRefusal, historyOpen, setSearch, known, shownRow, readOnly, checking };
}
