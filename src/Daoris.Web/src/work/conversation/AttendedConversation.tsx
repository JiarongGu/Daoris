import type { ComponentProps, RefObject } from 'react';
import type { TFunction } from 'i18next';
import type { withCall } from '../../bridge/shell';
import { SESSION_ACTIVE } from '../../design/ui';
import { anchorOf } from '../waits/anchors';
import { opinionAsked } from '../gates/opinion';
import { rulesAsked } from '../waits/waits';
import type { useAttendedWaits } from '../waits/useAttendedWaits';
import type { useAttendedHead } from '../head/useAttendedHead';
import type { useAttendedSession } from '../head/useAttendedSession';
import { beganLine } from '../head/partsLife';
import type { PartRows } from '../head/partRows';
import { FileOpener, type FileOpen } from '../review/preview';
import type { coolingFor } from '../say';
import type { Roster } from '../start/agentRoster';
import { type EarlierPart, EarlierParts } from './EarlierParts';
import { ancestryOf } from './line';
import { SessionConversation } from './SessionConversation';
import type { useAttendedBox } from './useAttendedBox';
import type { useWords } from './useWords';

/**
 * The conversation (D76): below the record, in the same scroll, so the head is read once and the words are what the region follows.
 * Only where the session ran: a teammate's record came without its transcript, which stays on their machine (D47 §4). The parts it
 * continues stand above it, read-only (SWITCHCTX1c-i).
 */
export function AttendedConversation({
  t, session, boxState, words, waitsState, headState, grouping, coolsUntil, roster, rootOf, openPreview, centre, partRows, partBox, attend,
  channel,
}: {
  t: TFunction;
  /** The home of the channel messages its turns took (COMMS1h): who posted each, and the door that opens its channel's panel. */
  channel?: ComponentProps<typeof SessionConversation>['channel'];
  session: ReturnType<typeof useAttendedSession>;
  boxState: ReturnType<typeof useAttendedBox>;
  words: ReturnType<typeof useWords>;
  waitsState: ReturnType<typeof useAttendedWaits>;
  headState: ReturnType<typeof useAttendedHead>;
  grouping: ReturnType<typeof withCall>;
  coolsUntil: ReturnType<typeof coolingFor>;
  roster: Roster;
  rootOf: (repository: string) => string | null | undefined;
  openPreview: (open: FileOpen) => void;
  centre: ComponentProps<typeof SessionConversation>['scroller'];
  attend: (id: string) => void;
  partRows: PartRows;
  partBox: RefObject<HTMLDivElement | null>;
}) {
  const { attended, attendedEntry, sessions, harnesses, live, here } = session;
  const { talking, chatTurns, turns, onUsage, onWaiting } = boxState;
  const { onStartFrom, onGoOnBrief, onSayAgain, onGoOnNew, goOnNew, newSession } = words;
  const { waits, standing, setAnchored, helperWaits, goToWait, setTail } = waitsState;
  const { setChatHeld } = headState;
  if (!attended || !here) return null;

  const beganOf = (id: string) => {
    const part = attendedEntry?.parts.find((each) => each.session === id);
    return attendedEntry && part && part.began !== 'opened' ? beganLine(t, part, attendedEntry.parts, partRows) : undefined;
  };
  // The line the attended part continues: the parts it continues by `from` (a part running alongside is not among them), and how each began.
  const line: EarlierPart[] = ancestryOf(sessions.data ?? [], attended.id, attendedEntry ? new Set(attendedEntry.parts.map((part) => part.session)) : null)
    .flatMap((id) => {
      const row = (sessions.data ?? []).find((each) => each.id === id);
      return row
        ? [{ id, adapter: row.door, chat: row.kind === 'chat', tree: row.tree, live: SESSION_ACTIVE.has(row.state), began: beganOf(id) }]
        : [];
    });

  return (
    <div className="mt-2 min-w-0">
      {/* A file a tool card names opens in the side bar's preview (PREVIEW1, D111). */}
      <FileOpener.Provider value={openPreview}>
        {/* The parts it continues, above it and read-only (SWITCHCTX1c-i); keyed by the part, so attending another starts the count again. */}
        {line.length > 0 && <EarlierParts key={attended.id} line={line} scroller={centre} />}
        <div ref={partBox} data-part={attended.id} data-attended className="min-w-0 scroll-mt-12">
          <SessionConversation
            session={attended.id}
            began={beganOf(attended.id)}
            adapter={attended.door}
            chat={attended.kind === 'chat'}
            tree={attended.tree}
            live={live}
            // A conversation the driver answers for is working while a turn is in flight, and not
            // under words its agent said after the turn ended (found looking at CONSOLE2).
            turnRunning={talking && chatTurns[attended.id] ? turns.taking : undefined}
            // The driver's word that its call waits for the person's answer (AUTONOMY1b3e).
            callMark={grouping?.calling ?? undefined}
            scroller={centre}
            // *Back to bottom* is the waiting panel's to draw, above it (WAITPANEL1), not the transcript's foot.
            onTail={setTail}
            onUsage={onUsage}
            onWaiting={onWaiting}
            // Its limit's wait, read from its events (LIMITRESUME1d2): the head and the foot say it.
            onLimitWait={(id, held) => setChatHeld((was) => (held
              ? { session: id, held }
              : was?.session === id ? null : was))}
            // Each wait's raising event, and the way between them (WAITFOOT1c, D168 point 4).
            waits={waits ? {
              waits: standing, goAheads: waits.goAheads, opinion: opinionAsked(waits.landing)?.opinion ?? null,
              proposals: rulesAsked(waits.rules?.proposals, attended.id),
              helpers: waits.helpers?.asks,
              permissions: waits.permissions?.waits,
            } : undefined}
            onAnchors={(id, state) => setAnchored({ session: id, ...state })}
            // COMMS1h: each helper its agent asked for, a card at its call, the answer folded in, the helper's door on it.
            helpersAsked={helperWaits ? { asks: helperWaits.asks, nameOf: helperWaits.nameOf, onOpen: helperWaits.onOpen } : undefined}
            // COMMS1h: a channel message it took is a sender chip naming its poster, with the door to the channel where it is whole.
            channel={channel}
            onToWait={(wait) => {
              const anchor = anchorOf(wait);
              if (anchor) goToWait(anchor);
            }}
            // Where the person's words went, a door to that session; and words that cannot go on here, one press
            // that starts a conversation with them, where a chat can start here (MSG1f, D137 §3.1, §2.2).
            onSession={(id) => attend(id)}
            onStartFrom={rootOf(attended.repository) ? onStartFrom : undefined}
            // SESSCTX1c part B: and beside it, the same session going on in a new part handed Daoris's brief.
            onGoOnBrief={rootOf(attended.repository) ? onGoOnBrief : undefined}
            // *Say it again* (COWORK1c): the person's own words that may have reached the agent before a restart, said anew.
            onSayAgain={onSayAgain}
            // Words its cooling account holds, and *Go on in a new session* with what it came to (MSG1g2, D137 §2.2).
            cooling={coolsUntil ? {
              until: coolsUntil,
              quest: attended.quest,
              onGoOnNew,
              pending: goOnNew.isPending,
              answer: newSession?.session === attended.id ? newSession.answer : null,
            } : undefined}
            reasons={{
              from: attended.door,
              to: harnesses.data?.adapter,
              adapter: harnesses.data?.adapter,
              agent: roster.find((row) => row.door === attended.door)?.product || attended.door,
            }}
          />
        </div>
      </FileOpener.Provider>
    </div>
  );
}
