import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from '../format';
import { buildChain } from '../map/chain';
import { useQuests, useRegistry, useSessions } from '../queries';
import {
  stopNotice, type TurnStop, useCancelTurn, useEndChat, useHarnesses, useResolveSession, useSendMessage,
  useSessionTurns, useStartChat, useStopSession,
} from '../shell';
import { Button, Drawer, Icon, type Notify, SESSION_ACTIVE, Tip, useErrorNotify } from '../ui';
import { AttendedSession } from './AttendedSession';
import { SessionConversation } from './SessionConversation';
import { isIntake, sessionOrigin } from './identity';
import type { Resolution } from './AwaitingPerson';
import { Composer } from './Composer';
import { DiffPane } from './DiffPane';
import { useDraft } from './drafts';
import { type DockTab, RightDock } from './RightDock';
import { SessionTimeline } from './SessionTimeline';
import { SessionRail } from './SessionRail';
import { StartSession, type StartChoice } from './StartSession';
import { OutputPanel, PANEL_MIN } from './frame';

// Per-viewer conveniences, like the language and the workspace scope (D42): a remembered layout is
// a preference, never machine wiring and never a tracked file.
const PANEL_HEIGHT = 'daoris.panelHeight';
const PANEL_CLOSED = 'daoris.panelClosed';

function remembered(key: string, fallback: number): number {
  try {
    const held = Number(window.localStorage.getItem(key));
    return Number.isFinite(held) && held > 0 ? held : fallback;
  } catch {
    // Storage can be absent or refused; a layout that is not remembered still works.
    return fallback;
  }
}

function remember(key: string, value: string): void {
  try {
    window.localStorage.setItem(key, value);
  } catch {
    // Not remembering is a lesser failure than not working.
  }
}

/**
 * The **Work frame** (D55): the rail, the attended session, the composer and the output panel.
 *
 * @remarks
 * **One selection binds every region** (IDE study §3), which is why it is held here and passed
 * down rather than kept by the rail. It is also why the composer and the panel need no id of their
 * own: they are looking at whatever the person is.
 *
 * **It is a frame, not a view** — it fills the window rather than sitting in the management
 * shell's content column, and the mode switch and the status bar that come with D55 belong to the
 * application, because both frames want them.
 *
 * **Desktop-only, structurally.** Over a keyed remote none of this is rendered: the stream and a
 * tree path are machine-local (D47 §4), and a frame whose centre is a stream has nothing honest to
 * show a browser. `App` gates on the same "is a shell here" answer every control uses.
 *
 * **The centre is the record, then the conversation** (D76): the head and the session's
 * conversation scroll together, and the conversation follows its tail. The right dock holds the
 * timeline and the review (components plan §3a); the console is the panel below, the
 * conversation's raw view.
 */
export function WorkFrame({ selected, onSelect, notify, onSendBack, onAnswerAsk, intent, onIntentTaken }: {
  /**
   * The attended session, held by the application — because a door into Work from somewhere else
   * (a quest's record) has to be able to say WHICH session, and a selection this frame kept to
   * itself could not be told.
   */
  selected: string | null;
  onSelect: (id: string | null) => void;
  notify: Notify;
  /**
   * Send the reviewed work back as a quest (SURF6b) — a door into the platform's own composer, not
   * a second publish path. It is the one review move Daoris has that an editor does not, and it
   * goes through the channel `repository-owns-its-work` sanctions rather than around it.
   */
  onSendBack?: (repository: string) => void;
  /**
   * Open the ask a parked intake is waiting on (INT4g). Its answer is there — publish or close —
   * and the ask's record is the application's to open, in Quests, the way the attention band does.
   */
  onAnswerAsk?: (ask: string) => void;
  /**
   * What the command palette asked for (SURF9) — an event, consumed on arrival, because leaving it
   * set would reopen the drawer every time anything here re-rendered.
   */
  intent?: 'start' | 'review' | null;
  onIntentTaken?: () => void;
}) {
  const { t } = useTranslation();
  const [refusal, setRefusal] = useState<string | null>(null);
  const [starting, setStarting] = useState(false);
  // The centre scrolls the head and the conversation together; the conversation follows its tail.
  const centre = useRef<HTMLDivElement>(null);
  // Which dock surface is up. Not remembered across launches: unlike the mode and the attended
  // session, this one is answered by what the person is doing in the next ten seconds.
  const [dock, setDock] = useState<DockTab>('timeline');
  const [height, setHeight] = useState(() => remembered(PANEL_HEIGHT, 200));
  const [collapsed, setCollapsed] = useState(() => {
    try {
      return window.localStorage.getItem(PANEL_CLOSED) === '1';
    } catch {
      return false;
    }
  });

  const sessions = useSessions(null, true);
  const quests = useQuests(null, true);
  const registry = useRegistry();
  const harnesses = useHarnesses();

  const startChat = useStartChat();
  const resolve = useResolveSession();
  const send = useSendMessage();
  const end = useEndChat();
  const stop = useStopSession();
  const cancelTurn = useCancelTurn();

  useErrorNotify(sessions.error, notify);

  const attended = (sessions.data ?? []).find((session) => session.id === selected) ?? null;
  const quest = attended?.quest
    ? (quests.data ?? []).find((row) => row.id === attended.quest) ?? null
    : null;
  const live = attended ? SESSION_ACTIVE.has(attended.state) : false;

  // A conversation is the only thing there is anything to say to. A driven session also holds a
  // tree, but it was given its whole target at once and has no channel to speak into — an input
  // box nothing is listening to is worse than none (the reasoning Projects used to carry).
  const conversation = attended?.kind === 'chat';
  // A teammate's record came down with the sync (SYNC4) and its process is on THEIR machine: it is
  // read here, and nothing this window sends could reach it — so it gets no moves and no composer.
  const here = attended ? sessionOrigin(attended) === null : false;
  // An intake takes no messages at all (INT4b, INT4g, INT4h): it is one turn, framed as one prompt.
  // Parked, there is no process left to hear one, and its answer is on the ask. Running, the pipe
  // door gave it no stdin and on the protocol door its stdin is the driver's own frames — so a box
  // there sent a person's words into nothing, or into the middle of the JSON-RPC stream. The driver
  // refuses such a line too; the frame offers no box, and the head carries the stop.
  const intake = attended ? isIntake(attended) : false;
  const talking = Boolean(attended && conversation && here && !intake);

  // What the person was typing to this conversation, kept per session and across a reload (CONV4b).
  const [draft, setDraft] = useDraft(talking ? attended!.id : null);
  // Where its turns stand, as the driver holds them: the stop and the queue follow this, not the
  // record, which learns a turn began only when its first event lands (CONV4a).
  const turns = useSessionTurns(talking ? attended!.id : null);

  // Cleared only when the record it pointed at is gone entirely. A session that ENDED stays
  // attended, because the person is very likely reading exactly that. 🔴 And only a record this
  // frame has SEEN can be gone: a session just started is attended before the list has caught up
  // with it, and clearing on that first answer left "Nothing attended" beside a running chat (CONV3).
  const seen = useRef(new Set<string>());
  useEffect(() => {
    if (!sessions.data) return;
    for (const session of sessions.data) seen.current.add(session.id);
    if (selected && seen.current.has(selected) && !sessions.data.some((session) => session.id === selected)) {
      onSelect(null);
    }
  }, [selected, sessions.data, onSelect]);

  const attend = (id: string) => {
    onSelect(id);
    setRefusal(null);
  };

  const resize = (next: number) => {
    setHeight(next);
    remember(PANEL_HEIGHT, String(next));
  };

  const toggle = () => setCollapsed((was) => {
    remember(PANEL_CLOSED, was ? '0' : '1');
    return !was;
  });

  const onStart = (choice: StartChoice) => {
    setRefusal(null);
    startChat.mutate(choice, {
      onSuccess: (result) => {
        // The ledger's own sentence: the tree is busy, the repository has no checkout here, the
        // harness is missing, the chosen account is logged out. Each names the action that fixes
        // it, which is why it is shown rather than summarised.
        if (!result.sessionId) notify(result.message, 'error');
        else { setStarting(false); attend(result.sessionId); }
      },
      onError: (error: unknown) => notify(sentence(error), 'error'),
    });
  };

  const onSend = (text: string) => {
    if (!attended) return;
    send.mutate({ id: attended.id, text }, {
      // False is an answer: the session ended while they were typing. It lands on the composer
      // rather than in a toast, because that is where the person is looking.
      onSuccess: (result) => setRefusal(result.sent ? null : t('work.composer.notListening')),
      onError: (error: unknown) => notify(sentence(error), 'error'),
    });
  };

  // Stopping the turn and keeping the conversation (CONV4a). What was waiting comes back into the box,
  // ahead of whatever is being typed now, since it was written first; and the notice claims only the
  // asking — a stop that lands after the words and before the turn's end leaves it ending normally.
  const onStopTurn = () => {
    if (!attended) return;
    cancelTurn.mutate(attended.id, {
      onSuccess: ({ cancelled, withdrawn }: TurnStop) => {
        if (withdrawn.length > 0) setDraft((was) => [...withdrawn, was.trim()].filter(Boolean).join('\n\n'));
        const stopping = cancelled ? t('work.composer.turnStopping') : null;
        const back = withdrawn.length > 0 ? t('work.composer.withdrawn', { count: withdrawn.length }) : null;
        // Two sentences are joined the catalogue's way: English puts a space after a full stop, and
        // Chinese does not (seen on the window, CONV4b).
        notify(stopping && back
          ? t('work.composer.twoSentences', { first: stopping, second: back })
          : stopping ?? back ?? t('work.composer.noTurn'));
      },
      onError: (error: unknown) => notify(sentence(error), 'error'),
    });
  };

  // Cutting a live session off — a conversation's, from its composer, or a running intake's, from
  // the head, since an intake has no composer to carry it (INT4h).
  const onStop = () => {
    if (!attended) return;
    stop.mutate(attended.id, {
      onSuccess: (answer) => notify(t(stopNotice(answer), { id: attended.id })),
      onError: (error: unknown) => notify(sentence(error), 'error'),
    });
  };

  // The person's answer to a parked session (design §4). The refusal — a move that is not theirs,
  // a decline with nothing in it — is the host's own sentence and reaches them word for word.
  const onResolve = (state: Resolution, note: string | null) => {
    if (!attended) return;
    resolve.mutate({ id: attended.id, state, note: note ?? undefined }, {
      onSuccess: () => notify(t('work.awaiting.resolved', {
        id: attended.id, state: t(`sessionState.${state}`),
      })),
      onError: (error: unknown) => notify(sentence(error), 'error'),
    });
  };

  // 🔴 Consumed by IDENTITY, the way the quest composer's opening draft is (FIX-LOG 2026-09-23):
  // "if (intent) set…" during render made React re-run this frame with the same prop on every pass,
  // so the palette's ask never landed. The parent is told from an effect, not during this render.
  const [taken, setTaken] = useState<typeof intent>(null);
  if (intent && intent !== taken) {
    setTaken(intent);
    if (intent === 'start') setStarting(true);
    else setDock('review');
  }
  if (!intent && taken) setTaken(null);
  useEffect(() => { if (intent) onIntentTaken?.(); }, [intent, onIntentTaken]);

  const roster = Array.isArray(harnesses.data?.harnesses) ? harnesses.data.harnesses : [];
  const spawning = roster.find((row) => row.harness === (harnesses.data?.adapter ?? ''));

  return (
    <div className="flex min-h-0 flex-1">
      <aside className="flex w-60 shrink-0 flex-col border-r border-line max-lg:w-52">
        {/* The rail is a list of sessions, and NEW is one control (D56). It used to be a permanent
            287×200 form above the list — 27% of the rail, always, for something a person does
            occasionally. Every reference in the study puts new behind a single affordance. */}
        <header className="flex h-8 shrink-0 items-center gap-2 border-b border-line pl-3 pr-1.5">
          <span className="text-meta uppercase tracking-[0.06em] text-ink-faint">
            {t('work.rail.label')}
          </span>
          <Tip content={t('work.start.title')}>
            <Button
              variant="ghost"
              aria-label={t('work.start.title')}
              onClick={() => setStarting(true)}
              className="ml-auto h-6 w-6 justify-center px-0"
            >
              <Icon name="plus" size={15} />
            </Button>
          </Tip>
        </header>

        <div className="min-h-0 flex-1 overflow-y-auto">
          <SessionRail selected={selected} onSelect={attend} notify={notify} />
        </div>
      </aside>

      {/* D41's single detail-and-form surface (§4), rather than a popover built for one form. */}
      {starting && (
        <Drawer title={t('work.start.title')} onClose={() => setStarting(false)}>
          <StartSession
            // A checkout on this machine is the whole question: there is nowhere else to talk, and
            // a teammate's mirrored registration has no tree here (D48 §3/§7).
            repositories={(registry.data ?? [])
              .filter((row) => Boolean(row.root))
              .map((row) => row.repository)
              .sort()}
            harnesses={roster.filter((row) => row.present).map((row) => row.harness)}
            profiles={Array.isArray(spawning?.profiles) ? spawning.profiles : []}
            pending={startChat.isPending}
            onStart={onStart}
          />
        </Drawer>
      )}

      <div className="flex min-w-0 flex-1 flex-col">
        <div ref={centre} className="flex min-h-0 flex-1 flex-col overflow-y-auto px-4 py-3">
          <AttendedSession
            session={attended}
            quest={quest}
            resolving={resolve.isPending}
            stopping={stop.isPending}
            onResolve={here ? onResolve : undefined}
            onAnswerAsk={here ? onAnswerAsk : undefined}
            onStop={here && intake ? onStop : undefined}
            chain={quest ? buildChain(quest.id, quests.data ?? [], sessions.data ?? []) : []}
            onSession={(session) => attend(session.id)}
          />
          {/* The conversation (D76): below the record, in the same scroll, so the head is read once
              and the words are what the region follows. Only where the session ran: a teammate's
              record came without its transcript, which stays on their machine (D47 §4). */}
          {attended && here && (
            <div className="mt-4 min-w-0">
              <SessionConversation
                session={attended.id}
                adapter={attended.adapter}
                chat={attended.kind === 'chat'}
                tree={attended.tree}
                live={live}
                scroller={centre}
              />
            </div>
          )}
        </div>

        {talking && attended && (
          <Composer
            live={live}
            sending={send.isPending}
            refusal={refusal}
            // One owner for the moves at a time (D56): while the session is parked the attention
            // band above holds finish, decline and stop, and this form keeps `send` alone.
            endings={attended.state !== 'awaiting-person'}
            draft={draft}
            onDraft={setDraft}
            queued={turns.queued}
            taking={turns.taking}
            // Only a door that can see a turn end can stop one; the roster says which (CONV3b).
            stoppable={roster.find((row) => row.harness === attended.adapter)?.structured === true}
            stopping={cancelTurn.isPending}
            onStopTurn={onStopTurn}
            onSend={onSend}
            onFinish={() => end.mutate(attended.id, {
              onSuccess: () => notify(t('work.composer.ending', { id: attended.id })),
              onError: (error: unknown) => notify(sentence(error), 'error'),
            })}
            onStop={onStop}
          />
        )}

        <OutputPanel
          sessionId={attended?.id ?? null}
          height={Math.max(PANEL_MIN, height)}
          collapsed={collapsed}
          onResize={resize}
          onToggle={toggle}
        />
      </div>

      {/* The third column, built now that it has a second occupant (components plan §3a). It is
          keyed to the attended session like every other region, and it is what gives the centre
          column its height back — the timeline used to share that space with the composer and the
          panel. */}
      <RightDock tab={dock} onTab={setDock}>
        {dock === 'review'
          ? (
            <DiffPane
              session={attended?.id ?? null}
              hasTree={Boolean(attended?.tree)}
              onSendBack={onSendBack && attended
                ? () => onSendBack(attended.repository)
                : undefined}
            />
          )
          : attended
            ? (
              <div className="p-3">
                <SessionTimeline
                  session={attended}
                  quest={quest}
                  hideCurrentNote={attended.state === 'awaiting-person'}
                />
              </div>
            )
            : <p className="m-0 p-3 text-small text-ink-faint">{t('work.attended.none.body')}</p>}
      </RightDock>
    </div>
  );
}
