import { type Quest, type Session } from '../../data/api';
import { ago, sessionTool } from '../../locales/format';
import { type ChainStep } from '../../map/chain';
import { ChainStrip } from '../../map/ChainStrip';
import { type AccountNamer } from '../../agents/tools';
import { Button, Pill, SectionTitle, SESSION_TONE, shownKey } from '../../design/ui';
import { joinLines, liveParts } from '../../work/noteLines';
import { HowItCameToBe, type TraceDoor } from '../../work/head/HowItCameToBe';
import { Note } from '../../work/Note';
import { QuestFlow, type QuestMovesDoor } from '../QuestFlow';
import { type QuestAct } from './questActs';
import { type TFunction } from 'i18next';
import { type ShownState } from '../../design/atoms/Pill';

export function QuestRecord({ chain, onOpenQuest, onAttend, nameOf, quest, moves, session, t, shown, waitsOn, limitWait, press, trace, onOpenAsk }: {
  chain: ChainStep[];
  onOpenQuest: (id: string) => void;
  onAttend: ((session: string) => void) | undefined;
  nameOf: AccountNamer | undefined;
  quest: Quest;
  moves: QuestMovesDoor | undefined;
  session: Session | null | undefined;
  t: TFunction<"translation", undefined>;
  shown: ShownState | null;
  waitsOn: { line: string; values?: Record<string, unknown> | undefined; tip: string; } | null;
  limitWait: readonly string[] | null | undefined;
  press: (act: QuestAct) => void;
  trace: TraceDoor | undefined;
  onOpenAsk: ((id: string) => void) | undefined;
}) {
  return (
    <>
{chain.length > 1 && (
        /* The chain this quest belongs to (MAP1): the ask, the steps before and after it, what its close will
           still publish (D65 §4, `{parent}` as the asker wrote it), and on what each step ran. */
        <div className="mt-4">
          <ChainStrip
            chain={chain}
            onQuest={(other) => onOpenQuest(other.id)}
            onSession={onAttend ? (ran) => onAttend(ran.id) : undefined}
            nameOf={nameOf}
          />
        </div>
      )}

      {/* Its statuses drawn as a flowchart (FLOWCHART1a1, D186): after the chain, folded until it is opened, and the way it went
          once its moves are read (FLOWCHART1a2). */}
      <QuestFlow quest={quest} moves={moves} className="mt-4" />

      {quest.note && (quest.noteParts?.length
        // Daoris's lines in the reader's language, the person's words as theirs; a note without parts as kept (QUESTCLOSE1b2).
        ? <Note parts={quest.noteParts} className="mt-4 rounded-control bg-accent-soft px-3 py-2.5 text-body italic" />
        : <p className="mt-4 rounded-control bg-accent-soft px-3 py-2.5 text-body italic">{quest.note}</p>)}

      {session && (
        /* The driven session's RECORD (D46 §4) — read-only here: a quest is decided on its page, and its sessions
           are managed on theirs (D126 §3.6), so the stop is the session's page header's, behind the door below. Its
           note's Daoris lines are worded in the reader's language and the agent's words shown as written (LANG1b);
           the evidence is the driver's observation and renders verbatim. */
        <section className="mt-5" aria-label={t('quests.session.title')}>
          <SectionTitle>{t('quests.session.title')}</SectionTitle>
          <div className="flex flex-wrap items-center gap-2">
            <Pill tone={SESSION_TONE[shown ?? session.state]}>{t(shownKey(shown ?? session.state))}</Pill>
            <span className="font-mono text-meta text-ink-faint">
              {session.id} · {sessionTool(session, false, nameOf)}
              {' · '}{t('quests.session.moved', { ago: ago(session.updated) })}
            </span>
          </div>
          {/* What it waits on, in the row's own words (D126 §2.4): its last end reads as finished beside a quest that is not. */}
          {waitsOn && <p className="m-0 mt-2 text-body text-ink-soft">{t(waitsOn.line, waitsOn.values)}</p>}
          {/* Its limit's wait, the head's own sentence (LIMITRESUME1d2): when it goes on by itself, and how. */}
          {limitWait && limitWait.length > 0 && <p className="m-0 mt-2 text-body text-ink-soft">{joinLines(t, limitWait)}</p>}
          {/* A working session's past ending is left out of the reading, never of the record (WAITSHOWN1, D137 §2.3). */}
          <Note
            parts={session.state === 'working' ? liveParts(session.noteParts) : session.noteParts}
            className="mt-2 text-body text-ink-soft"
          />
          {session.evidence && (
            <pre className="mt-2 mb-0 overflow-x-auto whitespace-pre-wrap rounded-control border border-line-strong bg-raised px-3 py-2.5 font-mono text-small">
              {session.evidence}
            </pre>
          )}
          {/* One home for the stream (design §3, D55): a session's console is the frame's panel. This keeps the
              record summary and becomes a DOOR — which is only offered where Sessions exists at all. */}
          {onAttend && <Button className="mt-2.5" onClick={() => press('session')}>{t('work.open')}</Button>}
          <p className="mt-2 mb-0 text-small text-ink-faint">{t('quests.session.hint')}</p>
        </section>
      )}

      {trace && (
        /* How it came to be (TRACE1b): folded, so nothing is read until the person opens it. Its sessions open in
           Sessions, its other quests here and its ask on its page, each where this page already has the door. */
        <HowItCameToBe
          className="mt-5"
          kind="quest"
          id={quest.id}
          {...trace}
          onSession={onAttend}
          onQuest={onOpenQuest}
          onAsk={onOpenAsk}
          nameOf={nameOf}
        />
      )}
    </>
  );
}
