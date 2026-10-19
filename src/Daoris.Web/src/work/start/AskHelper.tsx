import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { SelectField } from '../../design/ui';
import { type Answered, InlineConfirm } from '../InlineConfirm';

/** The longest question a helper is asked, in characters: the driver's `SessionsHelperCommand.MaxQuestion`, the service's too. */
export const MAX_QUESTION = 4000;

/** What the person asks: the question as they wrote it, trimmed, and the agent they named, or null for Daoris to choose. */
export type HelperQuestion = { question: string; agent: string | null };

/** The value the agent's choice holds while Daoris chooses: no agent's id is a word with a space in it. */
const ANY = 'any agent';

/**
 * ***Ask another agent…*'s ask** (MIXAGENT2b, D163 point 2; the mixed-agents design §3): the person's own door to a helper, with
 * the fields the agent's `helper_ask` has. It opens under the page header as the hand-over's does, on the primary's hue since
 * nothing goes: what a helper does (it reads this session's work, answers, and changes nothing; its answer comes back as this
 * session's next turn, marked as the helper's), the question, and which agent, Daoris's choice unless the person names one; then
 * *Ask* and *Never mind*. The person asking is their yes, so no rule holds it; a refusal is said inside it and keeps the question.
 *
 * @remarks
 * A molecule: the roster and the press arrive as props. The session's own agent is not offered, as the hand-over's ask leaves it
 * out: a helper is another agent's reading.
 */
export function AskHelper({ from, roster, busy = false, onAsk, onClose }: {
  /** The session's own agent, which is not offered. */
  from?: string | null;
  /** The agents present on this machine, and their names. */
  roster: { harnesses: readonly string[]; labels?: Readonly<Record<string, string>>; names?: Readonly<Record<string, string>> };
  /** An ask on its way: the presses wait for it. */
  busy?: boolean;
  /** The second press: the question and the agent, told how it ended. */
  onAsk: (asked: HelperQuestion, answered: Answered) => void;
  /** Put down, or the ask landed. */
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const [question, setQuestion] = useState('');
  const [agent, setAgent] = useState(ANY);
  const nameOf = (id: string) => roster.names?.[id] ?? roster.labels?.[id] ?? id;
  const others = roster.harnesses.filter((harness) => harness !== from);

  return (
    <InlineConfirm
      className="mt-2.5"
      tone="primary"
      block
      label={t('work.helper.title')}
      says={t('work.helper.says')}
      meanIt={t('work.helper.meanIt')}
      busy={busy}
      ready={question.trim().length > 0}
      onConfirm={(answered) => onAsk({ question: question.trim(), agent: agent === ANY ? null : agent }, answered)}
      onClose={onClose}
    >
      <textarea
        aria-label={t('work.helper.question')}
        placeholder={t('work.helper.question')}
        rows={3}
        maxLength={MAX_QUESTION}
        value={question}
        disabled={busy}
        onChange={(event) => setQuestion(event.target.value)}
        className="min-w-0 basis-full resize-y rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
      />
      {others.length > 0 && (
        <SelectField
          ariaLabel={t('work.helper.agent')}
          value={agent}
          onChange={setAgent}
          disabled={busy}
          options={[{ value: ANY, label: t('work.helper.anyAgent') }, ...others.map((id) => ({ value: id, label: nameOf(id) }))]}
        />
      )}
    </InlineConfirm>
  );
}
