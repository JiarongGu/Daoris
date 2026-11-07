import { useState } from 'react';
import type { Choice } from './choiceWords';

/**
 * **The choice's line**: under the drawing, one line names the choice and its first fact, and is the
 * Map's one polite live region. It is said once per choice: the words are taken when the choice changes and held while it stands, so a
 * count or a session changing under it is repainted by the detail and never read out again. With nothing chosen it reads the hint.
 */
export function ChoiceLine({ choice, hint }: { choice: Choice | null; hint: string }) {
  const key = choice?.key ?? '';
  const [said, setSaid] = useState({ key, words: choice?.words ?? hint });
  if (said.key !== key) setSaid({ key, words: choice?.words ?? hint });
  return <p role="status" aria-live="polite" data-choice-line="" className="m-0 mt-2 text-small text-ink-soft">{key === '' ? hint : said.words}</p>;
}
