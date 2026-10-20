import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Ask } from '../data/api';
import { ago } from '../locales/format';
import { Button, Inline, SelectField } from '../design/ui';
import { shellWord } from '../lib/shellWord';
import { choiceWords } from '../work/gates/ReviewGate';
import { type Answered, Refused } from '../work/InlineConfirm';

/**
 * Where a press was made among an ask's presses, and the refusal said there (ACCESSASK1, the UI/UX design R3): a proposal's
 * *Apply* by its key, or the choice's own press.
 */
export type PressRefusal = { at: string; sentence: string } | null;

/**
 * The refusal said where its press was (the UI/UX design R3): a press clears the last one and is answered here, `done` saying
 * nothing, `refused` the driver's sentence under that press; an answer to a press made before the latest is no longer read.
 */
export function usePressRefusal() {
  const [refusal, setRefusal] = useState<PressRefusal>(null);
  const turn = useRef(0);
  return {
    at: (place: string) => (refusal?.at === place ? refusal.sentence : null),
    press: (place: string, landed?: () => void): Answered => {
      const mine = ++turn.current;
      setRefusal(null);
      return {
        done: () => {
          if (mine === turn.current) landed?.();
        },
        refused: (sentence) => {
          if (mine === turn.current) setRefusal({ at: place, sentence });
        },
      };
    },
  };
}

/** The choice's own press, among the proposals' keys. */
const SET = 'set';

/**
 * **An ask's review choice** (REVIEWENV1g, D154 point 3; the review environment design §1.5–§1.6): what the person chose for its
 * work, the latest standing, with their words; each proposal its intake made with its reason, which only their press applies;
 * and the choice changed, while the ask is open.
 *
 * @remarks
 * **The person's alone to set** (D74, REVIEWENV1b3): an intake proposes and never sets `off`, so a proposal is shown beside the
 * choice with *Apply*, and nothing applies it but that press. Nothing chosen says each repository's rule decides, which is how
 * an ask stood before.
 *
 * **A refusal is said where it was pressed** (ACCESSASK1, the UI/UX design R3): an *Apply* refused under its proposal, the
 * choice's *Set* under its press with the choice and the words still as typed, so it can be pressed again.
 *
 * A molecule: it is handed the ask and the press.
 */
export function AskReview({ ask, environments = [], busy = false, onChoose }: {
  ask: Ask;
  /** The review environments the ask's circle declares, by name; none where no shell says. */
  environments?: string[];
  busy?: boolean;
  /**
   * Set the choice, or apply a proposal, with the person's words where they give any, and how to answer the press. Absent where
   * nothing can set it.
   */
  onChoose?: (choice: string, words: string | undefined, answered: Answered) => void;
}) {
  const { t } = useTranslation();
  const [choice, setChoice] = useState('');
  const [words, setWords] = useState('');
  const refusal = usePressRefusal();
  const latest = ask.reviewChoices?.at(-1) ?? null;
  // A proposal stands until the person's choice says what it proposed (design §1.6): then it was applied, or chosen anyway.
  const proposals = (ask.reviewProposals ?? []).filter((proposal) => proposal.choice !== latest?.choice);
  const live = ask.state !== 'Closed';

  return (
    <div className="grid gap-2">
      <p className="m-0 text-body">
        {latest
          ? <Inline text={t('asks.record.reviewChosen', { choice: choiceWords(t, latest.choice), ago: ago(latest.at) })} />
          : <span className="text-ink-soft">{t('asks.record.reviewByRule')}</span>}
      </p>
      {latest?.words && <p className="m-0 text-small text-ink-soft">{t('asks.record.reviewWords', { words: latest.words })}</p>}

      {proposals.map((proposal) => {
        const key = `${proposal.choice}:${proposal.at}`;
        const refused = refusal.at(key);
        return (
          <div key={key} className="flex flex-wrap items-center gap-x-2 gap-y-1 border-l-[3px] border-line-strong pl-2 text-small text-ink-soft">
            <span className="min-w-0 flex-1 basis-64">
              <Inline text={t('review.proposal', { choice: choiceWords(t, proposal.choice), reason: proposal.reason })} />
            </span>
            {live && onChoose && (
              <Button disabled={busy} onClick={() => onChoose(proposal.choice, undefined, refusal.press(key))}>
                {t('asks.record.reviewApply')}
              </Button>
            )}
            {refused && <Refused sentence={refused} />}
          </div>
        );
      })}

      {live && onChoose && (
        <div className="flex flex-wrap items-center gap-2">
          <SelectField
            value={choice}
            onChange={setChoice}
            ariaLabel={t('asks.record.reviewChoose')}
            placeholder={t('asks.record.reviewChoose')}
            options={[
              { value: 'on', label: t('asks.compose.reviewOn') },
              ...environments.map((name) => ({ value: name, label: t('asks.compose.reviewIn', { environment: name }) })),
              { value: 'off', label: t('asks.compose.reviewOff') },
            ]}
          />
          {choice && (
            <input
              aria-label={t('asks.compose.reviewWords')}
              placeholder={t('asks.compose.reviewWords')}
              value={words}
              onChange={(event) => setWords(event.target.value)}
              className="min-h-[1.9rem] min-w-0 flex-1 basis-48 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
            />
          )}
          <Button
            disabled={busy || !choice}
            onClick={() => onChoose(choice, words.trim() || undefined, refusal.press(SET, () => {
              // Landed, the press starts again; refused, the choice and the words stay as typed.
              setChoice('');
              setWords('');
            }))}
          >
            {t('asks.record.reviewSet')}
          </Button>
          {refusal.at(SET) && <Refused sentence={refusal.at(SET)!} />}
        </div>
      )}
      {live && onChoose && (
        <p className="m-0 text-meta text-ink-faint [overflow-wrap:anywhere]">
          <Inline text={t('asks.record.reviewTwin', { id: shellWord(ask.id, '<id>') })} />
        </p>
      )}
    </div>
  );
}
