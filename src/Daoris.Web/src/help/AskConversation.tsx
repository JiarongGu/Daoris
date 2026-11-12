
import { type RefObject, useEffect, useRef, useState } from 'react';
import { sentence } from '../locales/format';
import { NO_TURNS, useChatTurns, useDeleteSession, useEditQueued, useEndChat, useHarnesses, useHelpStartFrom, usePinHelp, useRenameHelp, useSendMessage, useStartHelp, useStopSession, useAccounts, useAccountUse, useStopWaiting } from '../bridge/shell';
import { Prose, SESSION_ACTIVE } from '../design/ui';
import { Composer } from '../work/conversation/Composer';
import { focusDividers } from './focus/FocusDivider';
import { beyondBeneath } from './focus/BeyondReceipt';
import { useHelpBeyond } from '../bridge/helpBeyond';
import { useHelpReadings } from '../bridge/helpReadings';
import { readingsBeneath } from './secondCheck/ReadingLine';
import { SecondCheckChip } from './secondCheck/SecondCheckChip';
import { BackToBottom, SessionConversation, type TailState } from '../work/conversation/SessionConversation';
import { AskHistory } from './AskHistory';
import { type AskConversationSlot } from './AskPanel';
import { type StarterDoor } from './starters';
import { type HelpWhere } from './where';
import { type ChatLimitHeld } from '../work/waits/limitWait';
import { type FirstWords, type Sent, type Draft, NO_DRAFT, NEW_DRAFT, type AskOpening } from './ask/askTypes';
import { useHelpSlotParts } from './ask/useHelpSlotParts';
import { useHelpFollow } from './ask/useHelpFollow';
import { GoOpener } from './goLink';
import { AskHeld } from './ask/AskHeld';
import { useHelpMode } from './ask/useHelpMode';
import { useHelpSend } from './ask/useHelpSend';
import { useHelpHistory } from './ask/useHelpHistory';
import { useHelpPlace } from './ask/useHelpPlace';
import type { Place } from './focus';
import { useHelpFocus } from './focus/useHelpFocus';

/**
 * Ask Daoris's conversation: the organism that holds the newest help session's record
 * and its composer, so the panel holds neither.
 *
 * @remarks
 * **One conversation per workspace, carried on across opens**: the panel shows the
 * newest help session of the window's workspace, running or ended. **With none running, one opens as the panel shows**
 *  — the driver writes the room from the machine as it stands, spawns the agent and opens its session — so that is done
 * before the person types, and there is no start button to press before asking. Nobody has spoken in it
 * yet, so the panel goes on showing what it showed until the first message goes to it. One that could not
 * open says nothing: **a message with none to take it starts one**, and says why when it cannot.
 * *New conversation* finishes the one running and clears the panel; the next opens once it has gone.
 *
 * **Where the person is goes ahead of their words**: the view, the workspace in scope, the
 * session they attend and what a parked one asks — said when it changed since the last message to that
 * conversation, so the agent is not handed the same line every turn.
 *
 * Its words reach nothing but the harness, as every conversation's do: the driver makes no model
 * call. Desktop-only for the console's reason: the record arrives over the bridge.
 *
 * **A question can arrive already asked**: the palette's *Ask Daoris: "…"* row hands its words
 * in as `opening`, sent once per `id` exactly as a typed message is, so Quick Ask opens on the question
 * rather than on a box the person must type it into again.
 *
 * **Its conversations are kept**: the history lists them, kept on this machine only, and the one the person
 * picks is shown in the newest's place. **Every one shown takes the next word**: a stop ends the reply, never the
 * conversation, and one no reply runs in continues with the person's next words, its own conversation resumed or, where that
 * is lost, a new part handed Daoris's brief. Nothing opens ahead under one shown. A draft handed in
 * (the composer's *Make it a task with Daoris…*) shows that new conversation: the panel clears first, ending nothing.
 */
export function useAskConversation(
  scroller: RefObject<HTMLElement | null>,
  where?: HelpWhere,
  opening?: AskOpening | null,
  /** The agent Ask Daoris runs on, or null: with none named, nothing is opened ahead of the person. */
  helper: string | null = null,
  /**
   * Told once an opening has been sent, so its holder lets it go: this organism is drawn again
   * with its tab or its box, and a question still held would be asked again by the next drawing.
   */
  onOpened?: () => void,
  /** Where a go the person applied takes them: the starters' own door, so nothing else changes. */
  onGo?: (door: StarterDoor) => void,
  changeFocus?: number,
) {
  const { here, every, scoped, registered, chosen, setChosen, shown, live, picked, helpSessions, readied, t, setGoingOn, sessions, help, setCleared, hereNow, startIn, mine, newest, shownIn, i18n } = useHelpPlace();
  const { setHistoryOpen, setOpenedRow, shownListing, setHistoryRefusal, rowOf, listing, search, historyScope, historyRefusal, historyOpen, setSearch, known, shownRow } = useHelpHistory({ here: every ? null : here, registered, helper, chosen, setChosen, shown });
  // The focus a start carries, read at its press from the focus hook below, which needs the opening round this one keeps; and
  // where a focus handed in with a draft goes.
  const startFocus = useRef<() => Place | undefined>(() => undefined);
  const takeFocus = useRef<(focus: Place, into: string | null) => void>(() => {});
  // Where the person left the list from: the row they opened and the list's scroll, put back as they return; and
  // what is put back the next time the list opens.
  const [left, setLeft] = useState<{ row: string; scroll: number } | null>(null);
  const [restore, setRestore] = useState<{ row: string | null; scroll: number } | null>(null);
  // Where the focus goes next, once: the open conversation's heading as a row is opened, or its box as a new one starts.
  // Each is let go after the drawing that takes it, so a later drawing never takes the focus back.
  const [focusHead, setFocusHead] = useState(0);
  const [focusBox, setFocusBox] = useState(0);
  useEffect(() => {
    if (focusHead) setFocusHead(0);
  }, [focusHead]);
  useEffect(() => {
    if (focusBox) setFocusBox(0);
  }, [focusBox]);
  // A conversation started from an earlier one takes the focus in its box once the list holds it: the driver answers before the
  // sessions the state hub asks again name it, and a box asked for before then would take nothing. Let go if
  // the person chose another meanwhile.
  const [focusWhenListed, setFocusWhenListed] = useState<string | null>(null);
  useEffect(() => {
    if (focusWhenListed === null) return;
    if (chosen !== focusWhenListed) {
      setFocusWhenListed(null);
    } else if (picked?.id === focusWhenListed) {
      setFocusWhenListed(null);
      setFocusBox((was) => was + 1);
    }
  }, [focusWhenListed, chosen, picked?.id]);
  // Whether the reader left the conversation's tail, and the way back: drawn in a strip above the box.
  const [tail, setTail] = useState<TailState | null>(null);
  // A conversation started from an earlier one, which says so until the person has spoken in it.
  const [startedFrom, setStartedFrom] = useState<{ session: string; title: string } | null>(null);
  const rename = useRenameHelp();
  const pin = usePinHelp();
  const startFrom = useHelpStartFrom();
  const remove = useDeleteSession();
  // Not in the list yet is still opening, which is alive; one the list says ended can take no words.
  const readiedRow = helpSessions.find((session) => session.id === readied);
  const readiedLive = readied !== null && (readiedRow === undefined || SESSION_ACTIVE.has(readiedRow.state));

  const start = useStartHelp();
  // Its own mutation, so opening ahead never reads as the person's send pending, which disables the box.
  const readying = useStartHelp();
  const send = useSendMessage();
  const end = useEndChat();
  const stop = useStopSession();
  const editQueued = useEditQueued(shown?.id ?? null);
  const roster = useHarnesses();
  // The accounts the limit's line words its wait from, its presses, and the wait its events hold.
  const accounts = useAccounts();
  const accountUse = useAccountUse();
  const stopWaiting = useStopWaiting();
  const [limitHeld, setLimitHeld] = useState<{ session: string; held: ChatLimitHeld } | null>(null);
  const [limitRefusal, setLimitRefusal] = useState<{ session: string; sentence: string } | null>(null);
  const held = useChatTurns(shown && live ? [shown.id] : []);
  const turns = held[shown?.id ?? ''] ?? NO_TURNS;

  // What the person is writing, kept per conversation and apart for a new one: its words and its files, held here
  // rather than in the box, so going through the history and back loses neither, and one conversation's draft never follows
  // the person into another. The box writes the conversation it would send to: the one shown where words reach it, else a
  // new one's.
  const [drafts, setDrafts] = useState<Record<string, Draft>>({});
  const draftKey = shown ? shown.id : NEW_DRAFT;
  const draft = drafts[draftKey] ?? NO_DRAFT;
  const editDraft = (key: string, change: (was: Draft) => Draft) =>
    setDrafts((all) => ({ ...all, [key]: change(all[key] ?? NO_DRAFT) }));
  const [refusal, setRefusal] = useState<string | null>(null);
  // The conversation the person's own first words opened, called by them until the history names it.
  const [spoken, setSpoken] = useState<{ session: string; title: string } | null>(null);
  // The words that open a conversation: opening one takes seconds — the room written, the
  // agent spawned — and they showed nowhere meanwhile, so they read as lost. Held here until the
  // driver's queue answers for the conversation they opened, which shows them from then on: let go
  // any sooner, they vanished while the session list caught up. One that ended at once has no queue
  // to answer, and its record says what became of them. `late` where the page stopped waiting on the start.
  const [firstWords, setFirstWords] = useState<FirstWords | null>(null);
  const handedOver = firstWords?.session !== undefined && shown?.id === firstWords.session
    && (!live || held[firstWords.session] !== undefined);
  useEffect(() => {
    if (handedOver) setFirstWords(null);
  }, [handedOver]);
  const waiting = firstWords && !handedOver ? firstWords.words : null;
  // Words on their way to a conversation: shown the moment they are sent, until it holds them.
  const [sent, setSent] = useState<Sent[]>([]);
  const sends = useRef(0);
  // The box shown now: a message given back after the list moved on goes where the person can see it. Words to a
  // conversation the list then shows ended were given back to a box no longer drawn, and read as lost.
  const box = useRef(draftKey);
  useEffect(() => {
    box.current = draftKey;
  }, [draftKey]);
  // 🔴 A message that did not arrive goes back into the box, its files with it, never lost (the composer's own rule).
  const putBack = (text: string, files: File[]) => editDraft(box.current, (was) => ({
    text: [text, was.text.trim()].filter(Boolean).join('\n\n'),
    carry: { ...was.carry, files: [...files, ...was.carry.files] },
  }));
  const giveBack = (text: string, files: File[], why: string) => {
    setRefusal(why);
    putBack(text, files);
  };
  const { onNew, closeHistory, openRow, beginFrom, setRound, onGoOnBrief, joining, onSend, choosing, setChoosing, openHistory } = useHelpSend({ where, sends, setSent, send, giveBack, t, setGoingOn, sessions, shown, sent, turns, live, helpSessions, help, setCleared, hereNow, setChosen, setFirstWords, setSpoken, firstWords, setRefusal, startedFrom, setStartedFrom, here, putBack, drafts, setDrafts, start, startIn, startFocus: () => startFocus.current(), takeFocus: (focus, into) => takeFocus.current(focus, into), helper, mine, readiedLive, readying, opening, editDraft, onOpened, newest, setHistoryOpen, setFocusBox, end, setLeft, setOpenedRow, shownListing, setFocusHead, setHistoryRefusal, setRestore, left, rowOf, startFrom, setFocusWhenListed });
  const history = (
    <AskHistory
      rows={shownListing?.conversations ?? []}
      total={shownListing?.total}
      // A first answer on its way; a newer one over the last rows; a list that could not be read, with why (UX §4).
      loading={!shownListing && (listing.isPending || listing.isFetching) && !listing.isError}
      refreshing={listing.isFetching && listing.data === undefined && shownListing !== undefined}
      error={listing.isError && !listing.isFetching && !listing.isFetchNextPageError ? sentence(listing.error) : null}
      onRetry={() => void listing.refetch()}
      // The next page at the driver's `next`, offered only under the rows it follows; a page that could not be
      // read is said beside its press, which asks for it again.
      onMore={listing.data && listing.hasNextPage ? () => void listing.fetchNextPage() : undefined}
      loadingMore={listing.isFetchingNextPage}
      moreError={listing.isFetchNextPageError && !listing.isFetching ? sentence(listing.error) : null}
      search={search}
      scope={historyScope}
      shown={shown?.id ?? null}
      busy={startFrom.isPending || pin.isPending || end.isPending}
      refusal={historyRefusal ?? (historyOpen ? refusal : null)}
      restore={restore}
      onSearch={setSearch}
      onNew={onNew}
      onClose={closeHistory}
      onOpen={openRow}
      onRename={(id, name, answered) => rename.mutate({ id, name }, {
        onSuccess: () => answered.done(),
        onError: (error) => answered.refused(sentence(error)),
      })}
      onPin={(id, pinned) => {
        setHistoryRefusal(null);
        pin.mutate({ id, pinned }, { onError: (error) => setHistoryRefusal(sentence(error)) });
      }}
      onStartFrom={beginFrom}
      onDelete={(id, answered) => remove.mutate(id, {
        onSuccess: () => {
          answered.done();
          if (chosen === id) setChosen(null);
          if (left?.row === id) setLeft(null);
          // Its draft goes with it.
          setDrafts(({ [id]: _gone, ...kept }) => kept);
        },
        onError: (error) => answered.refused(sentence(error)),
      })}
    />
  );
  const { access, whatItHas, offered, selectedMode } = useHelpMode({ shown, known, live, picked, setChosen, readied, readiedLive, setRound, t, onGo });
  const focusing = useHelpFocus({
    shown, known, shownIn, here, window: every ? null : scoped ?? here ?? null, readied: readiedLive ? readied : null, setRound, onGo: onGo ?? (() => {}), reveal: changeFocus,
  });
  startFocus.current = focusing.startFocus;
  takeFocus.current = focusing.take;

  // What each turn of the one shown looked at beyond its focus, drawn under that turn.
  const beyond = useHelpBeyond(shown?.id ?? null);
  // And the second check's line under the last turn each of its readings covers, with the check's chip beside the access chip.
  const readings = useHelpReadings(shown?.id ?? null);
  const readingPages = readings.data?.pages ?? [];
  const readingBeneath = readingsBeneath(shown?.id ?? null, readingPages.flatMap((page) => page.readings));
  const beyondBeneathTurn = beyondBeneath(beyond.data, { focus: focusing.focus, next: focusing.next, onFocus: focusing.choose });
  const earlierParts = (shownRow?.parts ?? []).filter((id) => id !== shown?.id);
  // A go link in Ask Daoris's words opens its place as the starters' doors open theirs.
  const conversation = shown ? (<GoOpener.Provider value={onGo ?? null}>
    {earlierParts.map((id) => (
      <SessionConversation key={id} session={id} adapter={helpSessions.find((part) => part.id === id)?.door ?? shown.door}
        mode="earlier" chat live={false} scroller={scroller} />
    ))}
    <SessionConversation
      key={shown.id}
      session={shown.id}
      adapter={shown.door}
      chat
      tree={shown.tree}
      live={live}
      turnRunning={live ? turns.taking : undefined}
      scroller={scroller}
      dividers={focusDividers(shownRow?.marks)}
      beneath={(ask, until) => <>{beyondBeneathTurn(ask, until)}{readingBeneath(ask)}</>}
      onTail={setTail}
      onGoOnBrief={live ? undefined : () => onGoOnBrief(shown.id)}
      onLimitWait={(session, held) => setLimitHeld((was) => (held ? { session, held } : was?.session === session ? null : was))}
    />
  </GoOpener.Provider>) : null;

  // The words on their way shown with the conversation they were said to, or with none while the list has not listed it yet.
  const going = sent.filter((each) => each.session === shown?.id || !helpSessions.some((session) => session.id === each.session));
  const unanswered = Boolean(firstWords?.late) || going.some((each) => each.late);

  const composer = (
    <Composer
      key={draftKey}
      // A message always reaches something: the one shown, or the one it starts.
      live
      placeholder={t(shown && !live ? 'help.placeholderGoOn' : 'help.placeholder')}
      sending={send.isPending || start.isPending || joining || end.isPending}
      refusal={refusal}
      // A conversation has no ending to choose: its one stop ends the reply.
      endings={false}
      draft={draft.text}
      onDraft={(text) => editDraft(draftKey, (was) => ({ ...was, text }))}
      carried={draft.carry}
      onCarry={(carry) => editDraft(draftKey, (was) => ({ ...was, carry }))}
      focus={focusBox}
      queued={[...(waiting ? [waiting] : []), ...going.map((each) => each.words), ...turns.queued]}
      // Words the machine has not answered for are said so; words waiting on the conversation opening, rather
      // than on a turn, are said as Ask Daoris opening; words on their way to the one running, as sending.
      queuedLabel={unanswered ? t('help.unanswered')
        : waiting ? t('help.opening')
          : going.length > 0 ? t('help.sending')
            : turns.opening || !turns.taking ? t('help.opening') : undefined}
      taking={turns.taking}
      // Stop ends the reply's whole process tree, its tools' servers with it, on every door; the conversation stays.
      stoppable
      stopping={stop.isPending}
      stopTurnLabel={t('help.stop')}
      stopTurnTip={t('help.stopTip')}
      onStopTurn={() => { if (shown) stop.mutate(shown.id); }}
      onRemove={editQueued.remove}
      onEdit={editQueued.reword}
      onSend={onSend}
      onFinish={() => {}}
    />
  );
  const { published } = useHelpFollow({ shown, onGo });

  // What the next words do, said right above the box: where one started from, or that one no reply runs in continues. A
  // title it names breaks inside a word too wide for the dock.
  const line = (key: string, values?: Record<string, string>) => (
    <Prose className="m-0 text-small text-ink-soft wrap-anywhere">{t(key, values)}</Prose>
  );
  const { limitLine, note } = useHelpSlotParts({ shown, limitHeld, roster, t, shownIn, accounts, i18n, startFrom, stop, stopWaiting, accountUse, limitRefusal, beginFrom, live, setLimitRefusal, startedFrom, line, choosing, setChoosing });

  // What the open conversation's head calls it: the history's title, else the person's first words, else a conversation.
  const title = !shown ? null
    : known?.title || (spoken?.session === shown.id ? spoken.title : t('help.head.untitled'));

  const slot: AskConversationSlot = {
    body: conversation, held: <AskHeld session={shown?.id} live={live} />, published, composer,
    note: end.isPending ? <p role="status" className="m-0 text-small text-ink-soft">{t('help.finishing')}</p>
      : limitLine ? <>{limitLine}{note}</> : note,
    // A new conversation from the head takes the shown one's applied focus.
    onNew: shown ? () => { focusing.inherit(); onNew(); } : undefined,
    newPending: end.isPending,
    tail: tail && !tail.atTail ? <BackToBottom onPress={tail.toTail} /> : undefined,
    title,
    focusHead,
    // Escape goes back to the list where the conversation shown is opened from it.
    escapeToList: shown !== null && left?.row === shown.id,
    history, historyOpen,
    onHistory: end.isPending ? undefined : openHistory,
    access: access ? <span className="flex flex-wrap items-center gap-2">{access}<SecondCheckChip check={readingPages[0]?.check ?? null} /></span> : access,
    wired: whatItHas, full: offered && selectedMode === 'full',
    // No chip where no words can go: one that cannot go on has no box, and its head still names the focus.
    focus: helper !== null && composer ? focusing.chip : undefined,
    crumb: focusing.crumb,
  };
  return slot;
}
