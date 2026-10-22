import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { Button } from '../../design/ui';
import { type Answered, InlineConfirm } from '../InlineConfirm';
import { PARTNER_REASONS } from '../noteLines';
import { AskPartner, type PieceAsk } from './AskPartner';
import { type PieceRow, shownOf, stoppable } from './pieceRows';

/** What the frame hands the head for a session's pieces: the pieces, the agents' names, and the presses it can make. */
export type PieceSection = {
  pieces: readonly PieceRow[];
  nameOf: (agent: string) => string;
  /** The ask's press, with the agents it may name; absent, no ask is offered (a browser, a session that cannot lead one). */
  ask?: {
    from?: string | null;
    roster: { harnesses: readonly string[]; labels?: Readonly<Record<string, string>>; names?: Readonly<Record<string, string>> };
    busy?: boolean;
    onAsk: (asked: PieceAsk, answered: Answered) => void;
  };
  /** Attend a partner's own session. */
  onOpen?: (partner: string) => void;
  /** Stop a working piece's partner, its record's stop. */
  onStop?: (partner: string, answered: Answered) => void;
  stopping?: boolean;
};

/** Where one piece stands, in a line: its state in the person's words, its agent, and the facts Daoris read of its work. */
export function pieceLine(t: TFunction, row: PieceRow, nameOf: (agent: string) => string): string {
  const shown = shownOf(row);
  const agent = row.startedOn ?? row.agent;
  const parts: (string | null)[] = [t(`work.pieces.state.${shown}`), agent ? t('work.pieces.by', { agent: nameOf(agent) }) : null];
  if (shown === 'refused' || shown === 'uncertain' || shown === 'held') {
    const why = row.code && PARTNER_REASONS[row.code] ? t(PARTNER_REASONS[row.code].key) : null;
    // An uncertain piece's state already says why in the person's words; a refusal's and a hold's why follows it.
    if (why && shown !== 'uncertain') parts.push(why);
  }
  if (shown === 'stopped' || shown === 'outOfTime' || shown === 'limited') parts.push(t('work.pieces.kept'));
  if (row.commit && row.ref) {
    parts.push(t('work.pieces.tip', { commit: row.commit.slice(0, 7), ref: row.ref }));
    if (row.files) parts.push(t('work.pieces.files', { count: row.files.length }));
    if (row.beyond && row.beyond.length > 0) parts.push(t('work.pieces.beyond', { count: row.beyond.length }));
  }
  return parts.filter(Boolean).join(' · ');
}

/**
 * **The Pieces section** (分项; COAGENT1b, D199; the co-agent design §6.1): each piece this session asked another agent to write, its
 * words as the person wrote them (content, shown as they are), then where it stands in the person's words with the facts Daoris read
 * of its work: the one commit kept and the ref it is frozen at, its files and those beyond its piece. A piece whose partner works has
 * a door to the partner's own page and *Stop…*, its record's stop, which keeps what it wrote. Under them, *Ask another agent to work on
 * a piece…* opens the ask in place. The answer is the session's next turn, in the conversation, so it is not said again here.
 *
 * @remarks
 * **Props only** (components §2): the frame reads the pieces and holds the presses. Without `ask` no ask is offered, without `onOpen`
 * no door, and without `onStop` no stop: a browser, a story. The acts the design names for a piece's later states (*Discard…*, *Hand
 * over what it left*) wait for the driver's acts they name.
 */
export function Pieces({ section, headingId }: { section: PieceSection; headingId: string }) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState(false);
  const { pieces, ask } = section;

  return (
    <section aria-labelledby={headingId} className="mt-2.5 grid gap-1.5">
      <h3 id={headingId} className="m-0 text-small font-semibold">{t('work.pieces.heading')}</h3>
      {pieces.length > 0 && (
        <ul className="m-0 grid list-none gap-2 p-0">
          {pieces.map((row) => <PieceLine key={row.id} row={row} section={section} />)}
        </ul>
      )}
      {ask && !asking && (
        <div>
          <Button disabled={ask.busy} onClick={() => setAsking(true)}>{t('work.pieces.ask')}</Button>
        </div>
      )}
      {ask && asking && (
        <AskPartner
          from={ask.from}
          roster={ask.roster}
          busy={ask.busy}
          onAsk={ask.onAsk}
          onClose={() => setAsking(false)}
        />
      )}
    </section>
  );
}

function PieceLine({ row, section }: { row: PieceRow; section: PieceSection }) {
  const { t } = useTranslation();
  const [stopping, setStopping] = useState(false);
  const { nameOf, onOpen, onStop } = section;
  const door = onOpen && row.partner ? row.partner : null;
  const stop = onStop && stoppable(row) ? row.partner : null;

  return (
    <li data-piece={row.id} className="grid gap-1 rounded-control border border-line bg-raised px-3 py-2">
      <p className="m-0 min-w-0 whitespace-pre-wrap break-words text-body">{row.piece}</p>
      <p className="m-0 text-small text-ink-soft">{pieceLine(t, row, nameOf)}</p>
      {(door || stop) && !stopping && (
        <div className="mt-1 flex flex-wrap items-center gap-2">
          {door && <Button onClick={() => onOpen!(door)}>{t('work.pieces.open')}</Button>}
          {stop && <Button disabled={section.stopping} onClick={() => setStopping(true)}>{t('work.pieces.stop')}</Button>}
        </div>
      )}
      {stop && stopping && (
        <InlineConfirm
          label={t('work.pieces.stopTitle')}
          says={t('work.pieces.stopSays')}
          meanIt={t('work.pieces.stopMeanIt')}
          busy={section.stopping}
          onConfirm={(answered) => onStop!(stop, answered)}
          onClose={() => setStopping(false)}
        />
      )}
    </li>
  );
}
