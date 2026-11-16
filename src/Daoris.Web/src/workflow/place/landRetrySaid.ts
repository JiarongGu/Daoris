import type { TFunction } from 'i18next';

// A run's `land` refused for a cause that passes: the driver keeps the wait on the step's words as a code
// (`LandRetries.Wait.Holds`), `busy` while it is tried again by itself, `climbed|<line>` once past its deadline the wait climbed the
// reporting line, the line as the driver says it. Anything else is no wait of this kind, and the caller says the step as before.

/** What an active `land`'s words say of its wait, or null where they are no retry's. */
export function landRetrySaid(t: TFunction, words: string): string | null {
  if (words === 'busy') return t('workflow.landRetry.busy');
  const climbed = /^climbed\|(.+)$/s.exec(words);
  return climbed ? t('workflow.landRetry.climbed', { line: climbed[1] }) : null;
}
