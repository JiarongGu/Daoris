import { clsx, type ClassValue } from 'clsx';
import { extendTailwindMerge } from 'tailwind-merge';

/**
 * The type steps `tokens.css` declares (D41 §3 as amended by D56).
 *
 * @remarks
 * 🔴 **tailwind-merge has to be told these are SIZES, or it treats them as colours and deletes
 * them.** `text-small` and `text-ink-soft` are both `text-…`, and a stock merge knows only the
 * built-in scale — so it read a custom step as a colour, put both in one group, and kept the last:
 * every `cn('text-small', …, 'text-ink-soft')` in this codebase rendered **without a size**.
 *
 * It is exactly the failure this project keeps finding: valid Tailwind, a perfectly rendering page,
 * one step off the intended scale, and nothing to notice. `tokens.test.ts` could not catch it —
 * the class is right there in the source, which is all a source scan can see.
 */
const STEPS = ['meta', 'small', 'body', 'title', 'view', 'wordmark', 'value'] as const;

const merge = extendTailwindMerge({
  extend: { classGroups: { 'font-size': [{ text: [...STEPS] }] } },
});

/** The standard composition idiom: conditional classes, with Tailwind conflicts resolved last-wins. */
export function cn(...inputs: ClassValue[]): string {
  return merge(clsx(inputs));
}
