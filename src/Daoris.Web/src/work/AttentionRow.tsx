import { useTranslation } from 'react-i18next';
import { elapsed } from '../format';
import { cn } from '../lib/cn';
import type { TrustHold } from '../signals';
import { Dot, Inline } from '../ui';
import { Note } from './Note';
import { hasNote, type NoteRecord } from './noteLines';

/**
 * One thing that is waiting on a person, as Overview's band shows it.
 *
 * @remarks
 * **Every kind is a different question.** A `parked` session is stopped at a checkpoint
 * only a person can clear (D46). An ask is a `proposal` while nothing serves it: the declarations
 * proposed, and only a person publishes (INT4a). An ask is `intake` when its intake session parked
 * asking the person (D65 §1b). That session is counted as the ask, never twice. An `unanswerable`
 * quest is addressed to a repository this deployment has no registration for. It will sit forever,
 * and nothing else says so. A `trust` row is a folder the agent has not been trusted in, where the
 * driver is holding a start (D73): only the person can give that grant. A `rule` row is an agent's
 * proposal to widen what agents may do (PERM2, D74), which never applies without the person. A
 * `parked-quest` row is a quest parked on its failed sessions here (SESSUX1i, D126 §4.6), which only the
 * person's *Try again* starts again; its door is the quest's page, where that press is.
 *
 * A fifth kind belongs here by design §4 — **finished work nobody has looked at** — and is not
 * buildable yet: nothing records that anybody looked. SURF6's *viewed* mark is the person's own and
 * is not kept, so the row arrives when looking is recorded, and the band says as much rather than
 * leaving a silent gap.
 */
export type Attention = {
  /** The session, ask or quest id — what the door opens; for a `trust` row, the folder; for a `rule` row, the proposal. */
  id: string;
  kind: 'parked' | 'parked-quest' | 'proposal' | 'intake' | 'unanswerable' | 'trust' | 'rule';
  /** A `trust` row's hold: the folder and the agent's own file — exactly what a grant writes. */
  trust?: TrustHold;
  /** What it is, derived: a session's identity, an ask's first line, or a quest's title. */
  title: string;
  /**
   * Where it waits: the repository, or for an ask its circle, because an ask has no repository yet.
   * For a `rule` row, who proposed it: the rules are the machine's, so the proposer is the place.
   */
  where: string;
  /** When it started waiting — a park's last move, a parked quest's last session's end, an ask's asking, a quest's filing. */
  since: string;
  /** The sentence explaining what the person is being asked to settle. */
  detail?: string | null;
  /**
   * A parked session's or a parked intake's note, its English and its parts (LANG1b): worded by the row in the reader's
   * language, the agent's question as written, in `detail`'s place.
   */
  note?: NoteRecord;
};

/** The kinds that wait in a circle rather than in a repository. */
const IN_A_CIRCLE: ReadonlySet<Attention['kind']> = new Set(['proposal', 'intake']);

/**
 * A row in *what needs you* — and a **door**, because a band that only counted would send people
 * looking for the thing it just told them about.
 *
 * **A door opens something, or it is not a door** (platform language §4). Handed no `onOpen`, the
 * row has nowhere to go, as a parked session in a browser has no Sessions. So it is text rather than
 * a button: it still says what is waiting, because knowing is the half that travels.
 *
 * A molecule: it is handed the item and reports a click.
 */
export function AttentionRow({ item, onOpen }: {
  item: Attention;
  onOpen?: (item: Attention) => void;
}) {
  const { t } = useTranslation();

  const body = (
    <>
      <span className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-0.5">
        <span className="min-w-0 truncate text-body font-semibold text-ink">{item.title}</span>
        <span className="shrink-0 font-mono text-meta text-ink-faint">
          {t('work.attention.since', { span: elapsed(item.since) })}
        </span>
      </span>
      <span className="mt-0.5 flex flex-wrap items-baseline gap-x-2 text-small">
        <Dot tone="parked" label={t(`work.attention.${item.kind}`)} />
        <span className="text-accent">
          {IN_A_CIRCLE.has(item.kind) ? t('work.attention.circle', { circle: item.where }) : item.where}
        </span>
      </span>
      {/* 🔴 No display utility beside the clamp: `block` overrode the box it needs, and a parked
          session's whole analysis filled the band (2026-09-29). */}
      {item.note && hasNote(item.note)
        ? (
          <span className="mt-0.5 line-clamp-2 text-small text-ink-soft">
            <Note note={item.note.note} parts={item.note.parts} compact />
          </span>
        )
        : item.detail && (
          <span className="mt-0.5 line-clamp-2 text-small text-ink-soft"><Inline text={item.detail} /></span>
        )}
      {/* Where the door goes, said on it: a row that only read as a notice left the owner with
          nowhere to answer a parked session (2026-09-29). */}
      {onOpen && (
        <span className="mt-1 block text-small font-medium text-accent">{t(`work.attention.open.${item.kind}`)}</span>
      )}
    </>
  );
  const shape = 'block w-full border-l-[3px] border-l-st-open px-3 py-2 text-left';

  return (
    <li>
      {onOpen
        ? (
          <button
            type="button"
            onClick={() => onOpen(item)}
            className={cn(shape, 'transition-colors duration-(--speed) hover:bg-accent-soft/50')}
          >
            {body}
          </button>
        )
        : <div className={shape}>{body}</div>}
    </li>
  );
}
