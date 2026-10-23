import { useTranslation } from 'react-i18next';
import type { Session } from '../../data/api';
import { ago } from '../../locales/format';
import { cn } from '../../lib/cn';
import { Dot, SectionTitle, SESSION_DOT, shownKey } from '../../design/ui';
import type { PartSpent, SessionEntry } from '../entries';
import { shownOf } from '../groups';
import type { SessionWhere } from '../rail/SessionRow';
import { beganLine, partPlace, spentLine } from './partsLife';
import { ranLine } from '../actors';

/** What the strip reads of one part's record besides the driver's word: its record, its repository's checkout, where its work is. */
export type PartFacts = {
  session: Session;
  /** Its repository's registered checkout, to tell a tree of its own from the root (as the row does). */
  root?: string | null;
  /** Where its work is now (LOOK2b): the tree it opened, or where its landing put the work. */
  where?: SessionWhere | null;
  /** What it spent, from this machine's record (SESSCTX1d, `SESSION_SPENT`); absent where none answered or it ended no turn. */
  spent?: PartSpent | null;
};

/** What the strip and the flow beside it are handed: the entry, the part shown, each part's record and place, and how to show another. */
export type PartsProps = {
  entry: SessionEntry;
  /** The part shown: the session the page attends. */
  attended: string;
  /** Each part's record and place, by its id. */
  rows: Readonly<Record<string, PartFacts | undefined>>;
  onShow: (session: string) => void;
};

/** The strip's heading: its name, and which part the page is on, *Part 2 of 3*. */
export function PartsHeading({ entry, attended }: Pick<PartsProps, 'entry' | 'attended'>) {
  const { t } = useTranslation();
  const shown = Math.max(0, entry.parts.findIndex((part) => part.session === attended));
  return (
    <div className="flex items-baseline justify-between gap-2">
      <SectionTitle level={3}>{t('work.parts.title')}</SectionTitle>
      <span className="text-meta text-ink-faint">{t('work.parts.of', { n: shown + 1, total: entry.parts.length })}</span>
    </div>
  );
}

/**
 * The parts, one button each, oldest first: each with its word, how it began and which tree or branch it held. The strip's
 * body, and the rail of the parts' flowchart where the flow does not fit (FLOWCHART1b).
 */
export function PartsList({ entry, attended, rows, onShow }: PartsProps) {
  const { t } = useTranslation();
  const { parts } = entry;
  return (
    <ol className="m-0 grid list-none gap-1 p-0">
      {parts.map((part, index) => {
        const facts = rows[part.session];
        const word = facts ? shownOf(facts.session, { shown: part.shown }, undefined) : null;
        const place = partPlace(t, facts);
        const spent = spentLine(t, facts?.spent);
        const here = part.session === attended;
        return (
          <li key={part.session}>
            <button
              type="button"
              aria-current={here || undefined}
              onClick={() => onShow(part.session)}
              className={cn(
                'flex w-full min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5 rounded-control border-l-[3px] px-2.5 py-1 text-left text-small',
                'transition-colors duration-(--speed) hover:bg-accent-soft/50',
                here ? 'border-l-accent bg-accent-soft text-ink' : 'border-l-transparent text-ink-soft',
              )}
            >
              <span className="font-mono text-meta text-ink-faint">{index + 1}</span>
              {word
                ? <Dot tone={SESSION_DOT[word]} label={t(shownKey(word))} />
                : <span className="text-meta text-ink-faint">{part.shown}</span>}
              <span>{beganLine(t, part, parts, rows)}</span>
              <span className="text-meta text-ink-faint">{ranLine(t, facts?.session)}</span>
              {part.quest && <span className="font-mono text-meta text-ink-faint">#{part.quest}</span>}
              {place && <span className="text-meta text-ink-faint">{place}</span>}
              {spent && <span className="text-meta text-ink-faint">{spent}</span>}
              {facts && <span className="ml-auto shrink-0 text-meta text-ink-faint">{ago(facts.session.updated)}</span>}
            </button>
          </li>
        );
      })}
    </ol>
  );
}

/**
 * **A session's parts** (SESSONE1c, D167 point 5, the co-working design §1.6): under its head, one button per record of the
 * entry, oldest first, each with its word, how it began (a move or a hand-over from which agent, and why: SESSCTX1d), which tree
 * or branch it held, and what it spent, on its own row, so a moved session reads as one. The part shown is marked and counted,
 * *Part 2 of 3*; a press shows another, and every act on the page stays the part shown's, since each part is still one
 * record with its own lifecycle (D167 point 1).
 *
 * @remarks
 * **A molecule**: the entry arrives as the driver's reader placed it and each part's record and place as props, so every
 * entry is reached without a bridge. Nothing is drawn for a line of work of one part, whose page needs no strip. A part the
 * page does not hold the record of (a teammate's, deleted) is still listed by its word and how it began, with no tree.
 *
 * **The page mounts the flow in its place** (FLOWCHART1b, `PartsFlow`), and this strip is that flow's rail: where the flowchart
 * does not fit, or the person chose the list, the strip is what is shown.
 */
export function PartsStrip(props: PartsProps) {
  const { t } = useTranslation();
  if (props.entry.parts.length < 2) return null;
  return (
    <nav aria-label={t('work.parts.title')}>
      <PartsHeading entry={props.entry} attended={props.attended} />
      <PartsList {...props} />
    </nav>
  );
}
