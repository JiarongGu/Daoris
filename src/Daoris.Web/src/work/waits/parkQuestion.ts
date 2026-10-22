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
  /** By when the work needs the answer, as the agent knows it; null where it named none. Its claim: Daoris keeps no deadline. */
  by: string | null;
  at: string;
};

const text = (value: unknown): string | null => (typeof value === 'string' && value.trim() ? value : null);

/**
 * `SESSION_QUESTION`'s answer read defensively: an older shell's none, a question with no id or words, an answer with no label and a
 * recommendation that names no answer are each nothing; a null the bridge left out is absent.
 */
export function readQuestion(answer: unknown): ParkQuestion | null {
  const asked = (answer as { question?: unknown } | null)?.question as Record<string, unknown> | null | undefined;
  if (!asked || typeof asked !== 'object') return null;
  const id = text(asked.id);
  const question = text(asked.question);
  if (!id || !question) return null;
  const choices = Array.isArray(asked.choices)
    ? asked.choices.flatMap((choice) => {
      const label = text((choice as { label?: unknown } | null)?.label);
      const does = (choice as { does?: unknown } | null)?.does;
      return label ? [{ label, does: typeof does === 'string' ? does : '' }] : [];
    })
    : [];
  const recommend = typeof asked.recommend === 'number' && Number.isInteger(asked.recommend)
    && asked.recommend >= 1 && asked.recommend <= choices.length ? asked.recommend : null;
  return { id, question, choices, recommend, by: text(asked.by), at: typeof asked.at === 'string' ? asked.at : '' };
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
