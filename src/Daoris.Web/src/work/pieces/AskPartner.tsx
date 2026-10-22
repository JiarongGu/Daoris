import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { SelectField } from '../../design/ui';
import { type Answered, InlineConfirm } from '../InlineConfirm';

/** The longest piece a partner is asked, in characters: the driver's `PartnerAsk.PieceLimit`. */
export const MAX_PIECE = 4000;

/** What the person asks: the piece as they wrote it, trimmed, the agent they chose, and the paths it is about. */
export type PieceAsk = { piece: string; agent: string; paths: string[] };

/**
 * ***Ask another agent to work on a piece…*'s ask** (COAGENT1b, D199 point 2; the co-agent design §2.3 step 1, §6.1): the person's own
 * door to a partner, on the primary's hue since nothing here goes. What a piece is (another agent writes it in a clone of its own from
 * the commit this session stands at, and Daoris keeps it as one commit to merge), the piece, the paths it is about, and which agent:
 * the person names one, since a piece is written by the agent named. Then *Ask* and *Never mind*; a refusal is said inside it and
 * keeps the words.
 *
 * @remarks
 * A molecule: the roster and the press arrive as props. The session's own agent is not offered, as the helper's ask leaves it out: a
 * partner is another agent. With no other agent on the machine, it says so and offers nothing to confirm.
 */
export function AskPartner({ from, roster, busy = false, onAsk, onClose }: {
  /** The session's own agent, which is not offered. */
  from?: string | null;
  /** The agents present on this machine, and their names. */
  roster: { harnesses: readonly string[]; labels?: Readonly<Record<string, string>>; names?: Readonly<Record<string, string>> };
  /** An ask on its way: the presses wait for it. */
  busy?: boolean;
  /** The second press: the piece, the agent and the paths, told how it ended. */
  onAsk: (asked: PieceAsk, answered: Answered) => void;
  /** Put down, or the ask landed. */
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const others = roster.harnesses.filter((harness) => harness !== from);
  const [piece, setPiece] = useState('');
  const [paths, setPaths] = useState('');
  const [agent, setAgent] = useState(others[0] ?? '');
  const nameOf = (id: string) => roster.names?.[id] ?? roster.labels?.[id] ?? id;

  return (
    <InlineConfirm
      className="mt-2.5"
      tone="primary"
      block
      label={t('work.pieces.title')}
      says={others.length > 0 ? t('work.pieces.says') : t('work.pieces.noAgent')}
      meanIt={others.length > 0 ? t('work.pieces.meanIt') : undefined}
      busy={busy}
      ready={piece.trim().length > 0 && agent.length > 0}
      onConfirm={(answered) => onAsk({ piece: piece.trim(), agent, paths: paths.split(/\s+/).filter(Boolean) }, answered)}
      onClose={onClose}
    >
      {others.length > 0 && (
        <>
          <textarea
            aria-label={t('work.pieces.piece')}
            placeholder={t('work.pieces.piece')}
            rows={3}
            maxLength={MAX_PIECE}
            value={piece}
            disabled={busy}
            onChange={(event) => setPiece(event.target.value)}
            className="min-w-0 basis-full resize-y rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
          />
          <input
            aria-label={t('work.pieces.paths')}
            placeholder={t('work.pieces.paths')}
            value={paths}
            disabled={busy}
            onChange={(event) => setPaths(event.target.value)}
            className="min-w-0 basis-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
          />
          <SelectField
            ariaLabel={t('work.pieces.agent')}
            value={agent}
            onChange={setAgent}
            disabled={busy}
            options={others.map((id) => ({ value: id, label: nameOf(id) }))}
          />
        </>
      )}
    </InlineConfirm>
  );
}
