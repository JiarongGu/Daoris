import { useTranslation } from 'react-i18next';
import type { Convergence } from '../api';
import { Button, EmptyState, Inline, SkeletonRows } from '../ui';
import { cn } from '../lib/cn';
import { ListRowDoor } from '../work/ListPane';
import { atFloor, findingId, findingTitle, lower, SIMILARITY } from './records';

/**
 * Where Convergence's answer stands, as its list says it:
 * - `first`: the first answer on its way, which is skeleton rows;
 * - `unanswered`: a comparison that failed with no answer ever, said in place;
 * - `answered`: the findings at `at`, the similarity they answer, whether the service had more, and whether a newer
 *   answer is on its way, while these are held.
 */
export type FindingsAnswer =
  | { state: 'first' }
  | { state: 'unanswered'; sentence: string }
  | { state: 'answered'; findings: Convergence[]; at: number; more: boolean; comparing: boolean };

/**
 * **Convergence's list** (FRAME1f, D118 §2): the similarity, then the findings. A finding is a row of the list, and
 * choosing one reads it in the main area: the service's sentence, then its entries whole.
 *
 * @remarks
 * **A molecule**: the similarity and the answer arrive, and every move and press goes out.
 *
 * - **The similarity is a control, not a constant** (D30): the useful value depends on the embedder and the corpus,
 *   and the tier's note says what this deployment can find, beneath it.
 * - **A row is the finding's entries**, by their titles, then what kind of likeness it is and how alike, then the
 *   repositories that reached it. A likeness in different words, the finding no text comparison can make, wears the
 *   accent, as its card did.
 * - **Loading is §4's**: a first answer is skeleton rows, with the words in the line the count takes; a similarity
 *   moved holds the last findings at reduced opacity until the new ones land, so nothing jumps under the slider.
 * - **An empty answer is an empty state** whose act lowers the similarity, until its floor, where it says it is the
 *   floor (UX5 U42). The headline names the value the answer is for, never one still being moved.
 */
export function FindingList({ threshold, onThreshold, semantic, answer, chosen, onChoose }: {
  /** The similarity as the slider stands. */
  threshold: number;
  onThreshold: (value: number) => void;
  /** This deployment matches meaning too (D24), which the note under the similarity says. */
  semantic: boolean;
  answer: FindingsAnswer;
  /** The list's chosen item: a finding's name (`findingId`). */
  chosen: string | null;
  onChoose: (id: string) => void;
}) {
  const { t } = useTranslation();
  const line = 'm-0 px-3 py-1 text-small text-ink-faint';

  return (
    <div>
      {/* The similarity stays in reach above forty findings: the list scrolls under it. */}
      <div className="sticky top-0 z-10 grid gap-1 border-b border-line bg-page px-3 pb-2.5 pt-2">
        <label className="grid gap-1 text-small text-ink-soft">
          <span className="flex items-baseline gap-1.5">
            {t('convergence.threshold')} <strong className="font-mono tabular-nums text-ink">{threshold.toFixed(2)}</strong>
          </span>
          <input
            type="range"
            min={SIMILARITY.min}
            max={SIMILARITY.max}
            step={SIMILARITY.step}
            value={threshold}
            onChange={(event) => onThreshold(Number(event.target.value))}
            className="w-full accent-accent"
          />
        </label>
        <p className="m-0 text-meta text-ink-faint">{semantic ? t('convergence.hintSemantic') : t('convergence.hintLexical')}</p>
      </div>

      {/* A first load is skeleton rows (D41 §4), with the words in the line the count takes, so nothing moves when the
          answer lands. On the first real index this was seconds of a bare "comparing…" on an empty page (POLISH3). */}
      {answer.state === 'first' && (
        <>
          <p className={line}>{t('convergence.comparing')}</p>
          <div className="px-3"><SkeletonRows rows={4} /></div>
        </>
      )}

      {answer.state === 'unanswered' && <p className="m-0 px-3 py-3 text-small text-ink-soft"><Inline text={answer.sentence} /></p>}

      {answer.state === 'answered' && answer.findings.length === 0 && !answer.comparing && (
        <EmptyState
          icon="convergence"
          headline={t('convergence.empty', { value: answer.at.toFixed(2) })}
          body={atFloor(answer.at) ? t('convergence.emptyFloor', { value: SIMILARITY.min.toFixed(2) }) : t('convergence.emptyBody')}
          action={!atFloor(answer.at) && (
            <Button onClick={() => onThreshold(lower(answer.at))}>
              {t('convergence.lower', { value: lower(answer.at).toFixed(2) })}
            </Button>
          )}
        />
      )}

      {answer.state === 'answered' && (answer.findings.length > 0 || answer.comparing) && (
        <>
          {/* How many, and whether that is all of them: the promise the search makes, for the same reason. */}
          <p className={line}>
            {answer.comparing
              ? t('convergence.comparing')
              : answer.more
                ? t('convergence.cappedAt', { count: answer.findings.length })
                : t('convergence.count', { count: answer.findings.length })}
          </p>
          <ul className={cn('m-0 list-none p-0', answer.comparing && 'opacity-60 transition-opacity duration-(--speed)')}>
            {answer.findings.map((finding) => {
              const id = findingId(finding);
              const title = findingTitle(finding);
              return (
                <li key={id} aria-label={title} data-list-row="">
                  <ListRowDoor chosen={chosen === id} onPress={() => onChoose(id)}>
                    <span title={title} className="block truncate text-body text-ink">{title}</span>
                    <span className="flex items-baseline gap-2 text-small">
                      <span className={cn('min-w-0 truncate', finding.method === 'Convergent' ? 'text-accent' : 'text-ink-soft')}>
                        {t(`convergence.methods.${finding.method}.label`)}
                      </span>
                      <span className="ml-auto shrink-0 font-mono tabular-nums text-ink-faint">{finding.similarity.toFixed(3)}</span>
                    </span>
                    <span className="block truncate text-meta text-ink-faint">{finding.repositories.join(' ↔ ')}</span>
                  </ListRowDoor>
                </li>
              );
            })}
          </ul>
        </>
      )}
    </div>
  );
}
