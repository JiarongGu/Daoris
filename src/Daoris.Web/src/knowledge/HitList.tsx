import { Fragment } from 'react';
import { useTranslation } from 'react-i18next';
import type { Hit } from '../api';
import { mark } from '../highlight';
import { Button, CheckField, EmptyState, Icon, Inline, SkeletonRows } from '../ui';
import { cn } from '../lib/cn';
import { isComposing } from '../lib/composing';
import { contextOffer } from '../menus/press';
import { ListRowDoor } from '../work/ListPane';

/**
 * Where a search stands, as its list says it:
 * - `idle`: nothing asked yet, fewer than two characters typed;
 * - `first`: the first answer on its way, which is skeleton rows;
 * - `unanswered`: a search that failed with no answer ever, said in place;
 * - `answered`: the hits, whether the service had more, the tier that made them, and whether a newer answer is on
 *   its way, while these are held.
 */
export type HitsAnswer =
  | { state: 'idle' }
  | { state: 'first' }
  | { state: 'unanswered'; sentence: string }
  | {
    state: 'answered';
    hits: Hit[];
    /** The service capped them: there are more than these. */
    more: boolean;
    /** The answer was made by meaning too (TIER1). */
    byMeaning: boolean;
    /** Neither half answered, which is not nothing matching. */
    nothing: boolean;
    /** A newer search is on its way, and these are the last answer, held. */
    searching: boolean;
  };

/**
 * **Search's list** (FRAME1f, D118 §2): the box, *each repository's own only*, and the hits. A hit is a row of the list,
 * and choosing one reads its entry in the main area, where it was a drawer (audit SR4).
 *
 * @remarks
 * **A molecule**: what was typed and what was answered arrive, and every keystroke and press goes out.
 *
 * - **The box is the list's search** (D118 §3a, §3e): ↓ goes from it into the hits, as VS Code's filter boxes hand it
 *   on, ↑ and ↓ move between them, Home and End go to either end, and Escape clears what was typed, as the session
 *   list's does. The clearing control is the platform's own: `type="search"` brings Chromium's native one, which
 *   WebView2 paints in Windows' accent, a blue ✕ in a palette with no blue; that one is hidden in `tokens.css`.
 * - **Loading is §4's** (audit SR11): a first answer is skeleton rows, with its words on the line the count takes;
 *   a newer search holds the last answer at reduced opacity, so the list never jumps between two answers as the
 *   person types.
 * - **How many, and whether that is all**: a capped list with nothing saying so is the one answer a search must
 *   never give quietly.
 * - **An empty answer is an empty state** in the tier's own words, with the way to Convergence (UX5 U41); nothing
 *   answering is said apart from nothing matching.
 */
export function HitList({
  query, onQuery, localOnly, onLocalOnly, answer, marked, semantic, chosen, onChoose, onConverge,
}: {
  query: string;
  onQuery: (query: string) => void;
  localOnly: boolean;
  onLocalOnly: (localOnly: boolean) => void;
  answer: HitsAnswer;
  /** The query whose terms are marked in each excerpt. */
  marked: string;
  /** This deployment matches meaning too (D24): a hit found by words alone is then said to be. */
  semantic: boolean;
  /** The list's chosen item: an entry's id. */
  chosen: string | null;
  onChoose: (id: string) => void;
  /** Where an empty answer points: the view that finds a conclusion reached in other words. */
  onConverge: () => void;
}) {
  const { t } = useTranslation();
  const line = 'm-0 px-3 py-1 text-small text-ink-faint';

  return (
    <div>
      {/* The box stays in reach above forty hits: the list scrolls under it. */}
      <div className="sticky top-0 z-10 grid gap-1.5 border-b border-line bg-page px-2 pb-2 pt-1.5">
        <div className="flex items-center gap-1.5 rounded-control border border-line-strong bg-raised py-0.5 pl-2 pr-0.5 text-ink-faint focus-within:border-accent">
          <Icon name="search" size={12} className="shrink-0" />
          <input
            type="search"
            value={query}
            // The view is for finding: arriving on it is arriving to type (audit SR8).
            autoFocus
            aria-label={t('search.box')}
            placeholder={t('search.placeholder')}
            onChange={(event) => onQuery(event.target.value)}
            // Escape clears a search, and one with nothing in it is left to the list laid over (D118 §3a).
            onKeyDown={(event) => { if (!isComposing(event) && event.key === 'Escape' && query) { event.preventDefault(); onQuery(''); } }}
            className="min-h-6 min-w-0 flex-1 bg-transparent text-small text-ink outline-none placeholder:text-ink-faint"
          />
          {query && (
            <Button variant="ghost" aria-label={t('search.clear')} onClick={() => onQuery('')} className="h-6 w-6 justify-center px-0">
              <Icon name="x" size={12} />
            </Button>
          )}
        </div>
        {/* Local-only by default: canonical content is byte-identical in every adopter, so including it returns a
            dozen copies of one rule and calls that a corpus. */}
        <CheckField checked={localOnly} onChange={onLocalOnly} label={t('search.localOnly')} className="px-1 text-small" />
      </div>

      {answer.state === 'first' && (
        <>
          <p className={line}>{t('search.searching')}</p>
          <div className="px-3"><SkeletonRows rows={4} /></div>
        </>
      )}

      {answer.state === 'unanswered' && <p className="m-0 px-3 py-3 text-small text-ink-soft"><Inline text={answer.sentence} /></p>}

      {answer.state === 'answered' && answer.hits.length === 0 && !answer.searching && (answer.nothing
        // Nothing answering is not nothing matching: an index that could not be read says so.
        ? <EmptyState icon="search" headline={t('search.nothingHeadline')} body={t('search.nothingBody')} />
        : (
          <EmptyState
            icon="search"
            headline={t('search.emptyHeadline')}
            body={answer.byMeaning ? t('search.emptySemantic') : t('search.emptyLexical')}
            action={<Button onClick={onConverge}>{t('search.toConvergence')}</Button>}
          />
        ))}

      {answer.state === 'answered' && (answer.hits.length > 0 || answer.searching) && (
        <>
          {/* The words while a newer answer is on its way, on the line the count takes, so nothing moves. */}
          <p className={line}>
            {answer.searching
              ? t('search.searching')
              : answer.more ? t('search.cappedAt', { count: answer.hits.length }) : t('search.count', { count: answer.hits.length })}
          </p>
          {/* Found, but by words alone on a deployment that matches meaning too: said beside the list, or it reads as
              complete when a conclusion reached in other words may be missing. */}
          {semantic && !answer.byMeaning && answer.hits.length > 0 && (
            <p className="mx-2 mb-1.5 mt-0.5 border-l-[3px] border-warn bg-raised px-2.5 py-1.5 text-small text-ink-soft">
              {t('search.meaningDown')}
            </p>
          )}
          <ul className={cn('m-0 list-none p-0', answer.searching && 'opacity-60 transition-opacity duration-(--speed)')}>
            {answer.hits.map((hit) => (
              <li
                key={hit.id}
                aria-label={hit.title}
                data-list-row=""
                // A hit is a document's link: a right-click opens it or copies its path (CTX1, D138 §4).
                {...contextOffer({
                  label: hit.title,
                  acts: [
                    { id: 'open', label: t('contextMenu.act.open'), onSelect: () => onChoose(hit.id) },
                    { id: 'copy', label: t('contextMenu.act.copyPath'), icon: 'copy', copy: hit.path },
                  ],
                })}
              >
                <ListRowDoor chosen={chosen === hit.id} onPress={() => onChoose(hit.id)}>
                  <span className="block truncate text-body text-ink">{hit.title}</span>
                  <span title={hit.path} className="block truncate font-mono text-meta text-ink-faint">
                    {hit.repository} · {t(`kind.${hit.kind}`)}
                  </span>
                  {/* The matched terms, MARKED: the service centres the excerpt on the first match, since a result
                      that cannot show its reasoning gets treated as an oracle. Plain text around each mark, never a
                      span, whose edge dropped a space from the row's accessible name (RAIL1). */}
                  {hit.excerpt && (
                    <span className="mt-0.5 line-clamp-3 block text-small text-ink-soft">
                      {mark(hit.excerpt, marked).map((run, index) => (run.hit
                        ? <mark key={index} className="-mx-0.5 rounded-[2px] bg-accent-soft px-0.5 text-ink">{run.text}</mark>
                        : <Fragment key={index}>{run.text}</Fragment>))}
                    </span>
                  )}
                </ListRowDoor>
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  );
}
