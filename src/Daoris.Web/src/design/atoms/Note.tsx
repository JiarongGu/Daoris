import { type ElementType, type HTMLAttributes, type ReactNode } from 'react';
import { cn } from '../../lib/cn';
import { Icon } from './Icon';

/**
 * What a note says about itself in colour, now that no note wears a left edge (the window design §7.4: 3 px is a wait card's
 * or a failed card's alone): `fail` is a refusal or a failure, `you` is the person's own wait, `caution` is a warning that
 * neither refused nor waits, and `plain` is a remark. The words say it too; the tone and the mark are for the glance.
 */
export type NoteTone = 'fail' | 'you' | 'caution' | 'plain';

const TONE: Record<NoteTone, string> = {
  fail: 'text-fail-ink',
  you: 'text-you-ink',
  caution: 'text-ink',
  plain: 'text-ink-soft',
};

/** A tone's ink, for a line that is not a `Note` but says the same (a count, an age). */
export function noteInk(tone: NoteTone): string {
  return TONE[tone];
}

/**
 * One line the product says about a state, in its tone, with the warning mark ahead of it for every tone but `plain`.
 *
 * @remarks
 * Written as a flex line so the mark stands beside the first line and a long sentence wraps under its own words; the element
 * (`p` by default) and its role, id and data attributes are the caller's. The caller gives the box (margin, background,
 * padding) and the size; the tone's ink is the atom's.
 */
export function Note({ tone, as, mark, className, children, ...rest }: {
  tone: NoteTone;
  as?: ElementType;
  /** The leading mark; shown for every tone but `plain`, unless said otherwise. */
  mark?: boolean;
  className?: string;
  children: ReactNode;
} & Omit<HTMLAttributes<HTMLElement>, 'className' | 'children'>) {
  const Element = as ?? 'p';
  const shown = mark ?? tone !== 'plain';
  return (
    <Element {...rest} className={cn('m-0 flex min-w-0 items-start gap-1.5 text-small', TONE[tone], className)}>
      {shown && <span aria-hidden className="mt-0.5 shrink-0"><Icon name="conflict" size={13} /></span>}
      <span className="min-w-0">{children}</span>
    </Element>
  );
}
