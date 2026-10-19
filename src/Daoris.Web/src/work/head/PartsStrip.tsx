import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import type { Session } from '../../data/api';
import { ago } from '../../locales/format';
import { cn } from '../../lib/cn';
import { Dot, SectionTitle, SESSION_DOT, shownKey } from '../../design/ui';
import type { EntryPart, SessionEntry } from '../entries';
import { shownOf } from '../groups';
import { ownTree } from '../identity';
import type { SessionWhere } from '../rail/SessionRow';

/** What the strip reads of one part's record besides the driver's word: its record, its repository's checkout, where its work is. */
export type PartFacts = {
  session: Session;
  /** Its repository's registered checkout, to tell a tree of its own from the root (as the row does). */
  root?: string | null;
  /** Where its work is now (LOOK2b): the tree it opened, or where its landing put the work. */
  where?: SessionWhere | null;
};

/**
 * How a part began, in the reader's words (D167, SESSONE1b's `began`): started, retried after another part, resumed or carried
 * on from one, handed over from one, or asked by a repository. The earlier part is named by its number in the strip, and by its
 * id where the strip does not hold it. A word a newer host adds reads as a start rather than as nothing.
 */
export function beganLine(t: TFunction, part: EntryPart, parts: readonly EntryPart[]): string {
  const at = part.after ? parts.findIndex((other) => other.session === part.after) : -1;
  const after = at >= 0 ? t('work.parts.number', { n: at + 1 }) : part.after ?? '';
  switch (part.began) {
    case 'retried': return t('work.parts.began.retried', { after });
    case 'resumed': return t('work.parts.began.resumed', { after });
    case 'carried-on': return part.after ? t('work.parts.began.carriedOn', { after }) : t('work.parts.began.carriedOnAlone');
    case 'handed-over': return t('work.parts.began.handedOver', { after });
    case 'asked': return part.askedBy ? t('work.parts.began.asked', { repository: part.askedBy }) : t('work.parts.began.askedUnnamed');
    default: return t('work.parts.began.opened');
  }
}

/**
 * **A session's parts** (SESSONE1c, D167 point 5, the co-working design §1.6): under its head, one button per record of the
 * entry, oldest first, each with its word, how it began and which tree or branch it held. The part shown is marked and counted,
 * *Part 2 of 3*; a press shows another, and every act on the page stays the part shown's, since each part is still one
 * record with its own lifecycle (D167 point 1).
 *
 * @remarks
 * **A molecule**: the entry arrives as the driver's reader placed it and each part's record and place as props, so every
 * entry is reached without a bridge. Nothing is drawn for a line of work of one part, whose page needs no strip. A part the
 * page does not hold the record of (a teammate's, deleted) is still listed by its word and how it began, with no tree.
 */
export function PartsStrip({ entry, attended, rows, onShow }: {
  entry: SessionEntry;
  /** The part shown: the session the page attends. */
  attended: string;
  /** Each part's record and place, by its id. */
  rows: Readonly<Record<string, PartFacts | undefined>>;
  onShow: (session: string) => void;
}) {
  const { t } = useTranslation();
  const { parts } = entry;
  if (parts.length < 2) return null;
  const shown = Math.max(0, parts.findIndex((part) => part.session === attended));

  return (
    <nav aria-label={t('work.parts.title')}>
      <div className="flex items-baseline justify-between gap-2">
        <SectionTitle level={3}>{t('work.parts.title')}</SectionTitle>
        <span className="text-meta text-ink-faint">{t('work.parts.of', { n: shown + 1, total: parts.length })}</span>
      </div>
      <ol className="m-0 grid list-none gap-1 p-0">
        {parts.map((part, index) => {
          const facts = rows[part.session];
          const word = facts ? shownOf(facts.session, { shown: part.shown }, undefined) : null;
          const landed = facts?.where?.landed && (facts.where.treeGone || facts.where.landed.state === 'standing') ? facts.where.landed : null;
          const tree = facts && !landed && !facts.where?.treeGone ? ownTree(facts.session, facts.root) : null;
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
                <span>{beganLine(t, part, parts)}</span>
                {part.quest && <span className="font-mono text-meta text-ink-faint">#{part.quest}</span>}
                {landed
                  ? <span className="text-meta text-ink-faint">{t(landed.state === 'gone' ? 'work.rail.landedGone' : 'work.rail.landedOn', { branch: landed.branch })}</span>
                  : tree && <span className="text-meta text-ink-faint">{t('work.rail.inTree', { tree })}</span>}
                {facts && <span className="ml-auto shrink-0 text-meta text-ink-faint">{ago(facts.session.updated)}</span>}
              </button>
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
