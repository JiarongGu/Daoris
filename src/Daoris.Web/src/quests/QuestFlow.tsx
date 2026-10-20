import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { QuestMove } from '../data/api';
import { Flow } from '../design/Flow';
import { Icon } from '../design/ui';
import { cn } from '../lib/cn';
import { type QuestMoveLine, type QuestOfLife, questFlow } from './questLife';
import { shownStatus } from './records';

/**
 * What the page holds of a quest's moves (FLOWCHART1a2): the fold, which is the organism's so it reads the moves only once the
 * person opens it, and the read.
 */
export type QuestMovesDoor = {
  open: boolean;
  onToggle: () => void;
  /** The moves once read; `null` from a host that keeps none (an older host, trap 18); absent while not read. */
  moves?: QuestMove[] | null;
  /** A first read is on its way. */
  reading?: boolean;
  /** Why the read failed, in its own sentence. */
  refusal?: string | null;
};

/** At most this many of a status's lines show before its fold (the flowchart design §2.1). */
const SHOWN = 3;

/**
 * **A quest's life drawn as a flowchart** (FLOWCHART1a1-a2; D186, the flowchart design §2.1): a section of its page after the
 * chain strip and before *How this came to be*, folded to its line (its name and the status it stands at) until it is opened,
 * then the four statuses with the one it stands at lit and the moves out of each said, and, once its moves are read, the way it
 * went: its path drawn heavier and, inside each status, what happened there, oldest first, three before a fold.
 *
 * @remarks
 * - **Its moves are read only when it opens**, as the trace is (D143): the page's organism holds the fold and the read
 *   (`moves`), from the service's door, in a browser as in the window. Without a door it holds its own fold and draws the
 *   statuses alone, as FLOWCHART1a1 did.
 * - **A host that keeps no moves** (trap 18) draws the statuses and says the way it went is not kept there.
 */
export function QuestFlow({ quest, moves, className }: { quest: QuestOfLife; moves?: QuestMovesDoor; className?: string }) {
  const { t } = useTranslation();
  const [held, setHeld] = useState(false);
  const open = moves ? moves.open : held;
  const title = t('quests.flow.title');
  const read = moves?.moves;
  const said = moves?.refusal ? t('quests.flow.unread', { error: moves.refusal })
    : moves?.reading && read === undefined ? t('quests.flow.reading')
    : read === null ? t('quests.flow.notKept')
    : read && read.length === 0 ? t('quests.flow.noMoves')
    : read ? t('quests.flow.hintPath')
    : t('quests.flow.hint');
  return (
    <section aria-label={title} className={cn('min-w-0', className)}>
      <button
        type="button"
        aria-expanded={open}
        onClick={() => (moves ? moves.onToggle() : setHeld((was) => !was))}
        className="flex w-full min-w-0 cursor-pointer items-start gap-1.5 border-0 bg-transparent p-0 text-left text-small text-ink-faint hover:text-ink"
      >
        <Icon name={open ? 'chevronDown' : 'chevronRight'} size={12} className="mt-1 shrink-0" />
        <span className="min-w-0 [overflow-wrap:anywhere]">
          <span className="font-semibold">{title}</span>
          <span> · {t(`status.${shownStatus(quest)}`)}</span>
        </span>
      </button>
      {open && (
        <div className="mt-2 grid min-w-0 gap-2 pl-[18px]">
          <p className="m-0 text-small text-ink-faint">{said}</p>
          <QuestLifeFlow quest={quest} moves={read} />
        </div>
      )}
    </section>
  );
}

/** The flow a quest's section draws: its statuses, and with its moves the way it went and each status's lines. */
export function QuestLifeFlow({ quest, moves }: { quest: QuestOfLife; moves?: QuestMove[] | null }) {
  const { t } = useTranslation();
  const life = questFlow(t, quest, moves);
  const nodes = life.nodes.map((node) => {
    const lines = life.lines[node.id as keyof typeof life.lines];
    return lines.length > 0 ? { ...node, lines: <MoveLines lines={lines} /> } : node;
  });
  return <Flow label={life.label} nodes={nodes} edges={life.edges} entries={life.entries} />;
}

/**
 * A status's lines, oldest first, three before a fold that shows the rest. A conflict is marked ✕, as its refused edge ends; the
 * mark is the drawing's, and its words say it to a reader. Lines, not a list: the flow's nodes are its one list (§3.7), and the
 * fold is a control inside a node, reached as its foot's are, never a Tab stop of its own.
 */
function MoveLines({ lines }: { lines: QuestMoveLine[] }) {
  const { t } = useTranslation();
  const [all, setAll] = useState(false);
  const shown = all ? lines : lines.slice(0, SHOWN);
  return (
    <div className="grid min-w-0 gap-0.5">
      {shown.map((line) => (
        <p key={line.id} className="m-0 min-w-0 [overflow-wrap:anywhere]">
          {line.refused && <span aria-hidden className="mr-1">✕</span>}
          {line.text}
        </p>
      ))}
      {!all && lines.length > SHOWN && (
        <button
          type="button"
          tabIndex={-1}
          onClick={() => setAll(true)}
          className="cursor-pointer justify-self-start border-0 bg-transparent p-0 text-left text-meta text-ink-soft underline-offset-2 hover:text-ink hover:underline"
        >
          {t('quests.flow.more', { count: lines.length - SHOWN })}
        </button>
      )}
    </div>
  );
}
