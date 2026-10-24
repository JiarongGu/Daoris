import type { GoAhead } from '../../data/api';

/**
 * A parked session's question to the person (PARKVIEW1, the park's ask design §2): what only they can decide, as its agent asked it
 * through `question_ask` and the driver kept it for the park its last turn ended in. Every word is the agent's, shown as written in
 * every language; Daoris words only what surrounds it.
 */
export type ParkQuestion = {
  id: string;
  question: string;
  /** The answers it offers, each a press: its label and what the agent does on it. Empty for an answer in the person's words. */
  choices: { label: string; does: string }[];
  /** The answer it recommends, by its number from 1; null for none. */
  recommend: number | null;
  /** Why it recommends that answer, as it said (QUESTIONUI1a); null where it gave none or recommends none. */
  why: string | null;
  /** By when the work needs the answer, as the agent knows it; null where it named none. Its claim: Daoris keeps no deadline. */
  by: string | null;
  /**
   * The agent's mark that the question is the owner's alone (SUPERVISOR1b, the supervisor design §4.1): one of `OWNER_MARKS`, or a mark
   * a later build knows, as written; null or absent for none. The supervisor answers no marked question.
   */
  owner?: string | null;
  at: string;
};

/** The marks `question_ask` takes (`QuestionBox.OwnerMarks`), each with its own words on the card. */
export const OWNER_MARKS = ['sign-in', 'access', 'spending', 'delete', 'repository-asks', 'preference'] as const;
export type OwnerMark = (typeof OWNER_MARKS)[number];

/** Whether a mark is one this build has words for; any other is said as the owner's without naming why. */
export const knownMark = (mark: string | null | undefined): mark is OwnerMark => (OWNER_MARKS as readonly string[]).includes(mark ?? '');

const text = (value: unknown): string | null => (typeof value === 'string' && value.trim() ? value : null);

/**
 * The writer's bounds (`QuestionBox`), held on read too (Fable's read, Q6): the question, its answers, each label and what each does,
 * and by when; a question past any of them, or with two answers alike in any case, is none, as the connector never keeps one.
 */
export const QUESTION_BOUNDS = { question: 1000, choices: 6, label: 80, does: 300, by: 200, why: 300 } as const;

/**
 * `SESSION_QUESTION`'s answer read defensively: an older shell's none, a question with no id or words, one past a bound the writer keeps,
 * an answer with no label and a recommendation that names no answer are each nothing; a null the bridge left out is absent. The driver
 * reads the file by the same table (`Daoris.Desktop.Driver.Tests/fixtures/park-question.json`).
 */
export function readQuestion(answer: unknown): ParkQuestion | null {
  const asked = (answer as { question?: unknown } | null)?.question as Record<string, unknown> | null | undefined;
  if (!asked || typeof asked !== 'object') return null;
  const id = text(asked.id);
  const question = text(asked.question);
  if (!id || !question || question.length > QUESTION_BOUNDS.question) return null;
  const choices = Array.isArray(asked.choices)
    ? asked.choices.flatMap((choice) => {
      const label = text((choice as { label?: unknown } | null)?.label);
      const does = (choice as { does?: unknown } | null)?.does;
      return label ? [{ label, does: typeof does === 'string' ? does : '' }] : [];
    })
    : [];
  const by = text(asked.by);
  const why = text(asked.why);
  const labels = new Set(choices.map((choice) => choice.label.toLocaleUpperCase('en-US')));
  if (choices.length > QUESTION_BOUNDS.choices || labels.size < choices.length
    || choices.some((choice) => choice.label.length > QUESTION_BOUNDS.label || choice.does.length > QUESTION_BOUNDS.does)
    || (by !== null && by.length > QUESTION_BOUNDS.by) || (why !== null && why.length > QUESTION_BOUNDS.why)) {
    return null;
  }
  const recommend = typeof asked.recommend === 'number' && Number.isInteger(asked.recommend)
    && asked.recommend >= 1 && asked.recommend <= choices.length ? asked.recommend : null;
  // Its reason explains the recommendation (QUESTIONUI1a): with none recommended, there is nothing for it to explain.
  return {
    id, question, choices, recommend, why: recommend === null ? null : why, by, owner: text(asked.owner),
    at: typeof asked.at === 'string' ? asked.at : '',
  };
}

/**
 * The words a choice sends as the park's answer (the design §4): its label, as the agent wrote it, then the person's own words after
 * a blank line where they wrote some. The agent was told its answer comes back so, a label it offered first.
 */
export function choiceWords(label: string, words: string): string {
  const own = words.trim();
  return own ? `${label}\n\n${own}` : label;
}

/** The go-aheads the park still waits on, in their order: each asked and not yet answered. */
export const openGoAheads = (goAheads: readonly GoAhead[] | undefined): GoAhead[] =>
  (goAheads ?? []).filter((goAhead) => goAhead.state === 'asked');

/**
 * What the box's words become once a press kept them (the design §4): taken off only where the box still begins with the words the
 * press took, so words typed while it was on its way stay; a box changed under it is left as it is.
 */
export function afterPress(draft: string, taken: string): string {
  const said = taken.trim();
  if (!said) return draft;
  const now = draft.trimStart();
  return now.startsWith(said) ? now.slice(said.length).trimStart() : draft;
}
