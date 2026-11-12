import type * as React from 'react';
import { useEffect, useState } from 'react';
import { DEFAULT_WORKSPACE } from '../../data/workspaces';
import { useHelpConversations, useChatReplaced } from '../../bridge/shell';
import { useRegistry } from '../../data/queries';
import { repositoriesOf } from '../focus/focusPlaces';
import { keepFocusRecent, readFocusRecent } from '../focus/focusRecent';
import { type Showing } from '../historyHome';
import { searchable } from '../../lib/searchable';
import { type HelpConversationRow, sameWorkspace } from '../history';
import { type Session } from '../../data/api';

export function useHelpHistory({ here, registered, helper, chosen, setChosen, shown }: {
  here: string | null | undefined;
  registered: string[] | undefined;
  helper: string | null;
  chosen: string | null;
  setChosen: React.Dispatch<React.SetStateAction<string | null>>;
  shown: Session | null;
}) {
  // The history: open or not, its search, and each conversation's row. The panel's own rows are asked whenever an agent is
  // named, so the conversation shown knows its own.
  const [historyOpen, setHistoryOpen] = useState(false);
  const [search, setSearch] = useState('');
  const [historyRefusal, setHistoryRefusal] = useState<string | null>(null);
  const words = search.trim();
  const searching = searchable(words);
  // What the history shows (`Showing`): the window's workspace's conversations, every workspace's, or those about a place. A
  // window on every workspace shows every one's; so does a machine whose one workspace is `default`, so a conversation of a
  // workspace since gone is still reached, and named once it is opened. Its search runs over what is shown alone.
  const [chosenShowing, setShowing] = useState<Showing>({ kind: 'here' });
  const registry = useRegistry('machine');
  const repositories = Array.isArray(registry.data) ? repositoriesOf(registry.data) : registry.isError ? [] : undefined;
  const [recent, setRecent] = useState(readFocusRecent);
  const hasHome = typeof here === 'string' && ((registered?.length ?? 0) > 1 || !sameWorkspace(here, DEFAULT_WORKSPACE));
  const showing: Showing = chosenShowing.kind === 'here' && !hasHome ? { kind: 'all' } : chosenShowing;
  const listIn = typeof here === 'string' && showing.kind === 'here' ? here : null;
  const about = showing.kind === 'about' ? showing.place : null;
  const historyScope = here === undefined ? undefined : {
    showing, window: here, repositories, recent, workspace: listIn,
    onShowing: (next: Showing) => {
      setShowing(next);
      if (next.kind === 'about') setRecent(keepFocusRecent(next.place));
    },
  };
  // Each a page at a time, every page read joined; asked once the window's workspace is known.
  const rows = useHelpConversations('', helper !== null && here !== undefined, listIn, about);
  // Follow a chosen conversation when the driver continues it on another account. A fresh start has no shared parts.
  useEffect(() => {
    if (!chosen) return;
    const continued = rows.data?.conversations.find((row) => row.session !== chosen && row.parts?.includes(chosen));
    if (continued) setChosen(continued.session);
  }, [chosen, rows.data]);
  // The conversation chosen, refused at its open, goes on in its replacement within the same press.
  useChatReplaced((from, to) => setChosen((was) => (was === from ? to : was)));
  const found = useHelpConversations(words, helper !== null && here !== undefined && historyOpen && searching, listIn, about);
  const listing = searching ? found : rows;
  // The last list answered, held while a newer one comes, so a search typed never blanks the rows.
  const [lastListing, setLastListing] = useState<typeof rows.data>(undefined);
  useEffect(() => {
    if (listing.data) setLastListing(listing.data);
  }, [listing.data]);
  const shownListing = listing.data ?? lastListing;
  // A conversation's row: from the whole list's pages read, else the search's, which reaches every conversation
  // past them; else the row the person opened it from, once no list being asked can name it.
  const [openedRow, setOpenedRow] = useState<HelpConversationRow | null>(null);
  const listed = (id: string) => rows.data?.conversations.find((row) => row.session === id)
    ?? found.data?.conversations.find((row) => row.session === id) ?? null;
  const rowOf = (id: string) => listed(id) ?? (openedRow?.session === id ? openedRow : null);
  // Every conversation shown takes the next word, whatever ended its last reply: the driver resumes its own, or goes on in a
  // new part handed Daoris's brief where its own is lost, so the row answers no question of whether it can.
  const known = shown ? rowOf(shown.id) : null;
  return { setHistoryOpen, setOpenedRow, shownListing, setHistoryRefusal, rowOf, listing, search, historyScope, historyRefusal, historyOpen, setSearch, known, shownRow: known };
}
