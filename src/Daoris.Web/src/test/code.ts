import type { MatcherFunction } from '@testing-library/react';

/**
 * A code span found by its whole text (LOOK5). A span of several words sets each in a box of its own (`CodeText`,
 * so a line never breaks inside a flag), which leaves its words its descendants and never its own text, and a plain
 * `getByText` reads only an element's own text. A string is the span's whole text; a pattern is tested against it.
 */
export const code = (text: string | RegExp): MatcherFunction => (_content, element) => {
  if (element?.tagName !== 'CODE') return false;
  const whole = element.textContent ?? '';
  return typeof text === 'string' ? whole === text : text.test(whole);
};
