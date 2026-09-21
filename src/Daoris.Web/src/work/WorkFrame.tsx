import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from '../format';
import { useQuests, useRegistry, useSessions } from '../queries';
import { useEndChat, useHarnesses, useSendMessage, useStartChat, useStopSession } from '../shell';
import { type Notify, SESSION_ACTIVE, useErrorNotify } from '../ui';
import { AttendedSession } from './AttendedSession';
import { Composer } from './Composer';
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
 * **The right dock is not here yet.** The reference frame's third column holds the timeline and
 * the diff (components plan §3a); the diff arrives with SURF6, and until then a dock would be a
 * pane with extra chrome — so the timeline stays in the attended column and moves when it has
 * company.
 */
export function WorkFrame({ selected, onSelect, notify }: {
  /**
   * The attended session, held by the application — because a door into Work from somewhere else
   * (a quest's record) has to be able to say WHICH session, and a selection this frame kept to
   * itself could not be told.
   */
  selected: string | null;
  onSelect: (id: string | null) => void;
  notify: Notify;
}) {
  const { t } = useTranslation();
  const [refusal, setRefusal] = useState<string | null>(null);
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
  const send = useSendMessage();
  const end = useEndChat();
  const stop = useStopSession();

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

  // Cleared only when the record it pointed at is gone entirely. A session that ENDED stays
  // attended, because the person is very likely reading exactly that.
  useEffect(() => {
    if (selected && sessions.data && !sessions.data.some((session) => session.id === selected)) {
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
        else attend(result.sessionId);
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

  const roster = Array.isArray(harnesses.data?.harnesses) ? harnesses.data.harnesses : [];
  const spawning = roster.find((row) => row.harness === (harnesses.data?.adapter ?? ''));

  return (
    <div className="flex min-h-0 flex-1">
      <aside className="flex w-[18rem] shrink-0 flex-col overflow-y-auto border-r border-line max-lg:w-[14rem]">
        <StartSession
          // A checkout on this machine is the whole question: there is nowhere else to talk, and a
          // teammate's mirrored registration has no tree here (D48 §3/§7).
          repositories={(registry.data ?? [])
            .filter((row) => Boolean(row.root))
            .map((row) => row.repository)
            .sort()}
          harnesses={roster.filter((row) => row.present).map((row) => row.harness)}
          profiles={Array.isArray(spawning?.profiles) ? spawning.profiles : []}
          pending={startChat.isPending}
          onStart={onStart}
        />
        <div className="min-h-0 flex-1 border-t border-line">
          <SessionRail selected={selected} onSelect={attend} notify={notify} />
        </div>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-4">
          <AttendedSession session={attended} quest={quest} />
        </div>

        {conversation && attended && (
          <Composer
            live={live}
            sending={send.isPending}
            refusal={refusal}
            onSend={onSend}
            onFinish={() => end.mutate(attended.id, {
              onSuccess: () => notify(t('work.composer.ending', { id: attended.id })),
              onError: (error: unknown) => notify(sentence(error), 'error'),
            })}
            onStop={() => stop.mutate(attended.id, {
              onSuccess: () => notify(t('quests.session.stopped', { id: attended.id })),
              onError: (error: unknown) => notify(sentence(error), 'error'),
            })}
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
    </div>
  );
}
