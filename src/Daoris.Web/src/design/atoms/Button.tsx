import { type ButtonHTMLAttributes } from 'react';
import { cn } from '../../lib/cn';

/* ---------------------------------------------------------------- buttons */

const BUTTON: Record<string, string> = {
  default:
    'border border-line bg-raised text-ink hover:enabled:border-accent',
  primary:
    'border border-accent bg-accent text-accent-ink hover:enabled:brightness-108',
  ghost:
    'border border-transparent bg-transparent text-ink-soft px-2 py-1 hover:enabled:text-ink hover:enabled:border-line',
  // Its label in the danger ink, its line and its hover field in declined's hue: the hue as text is 3.95:1 in dark.
  danger:
    'border border-st-declined bg-transparent text-ink-danger hover:enabled:bg-st-declined/10',
};

/**
 * 🔴 **A button, unless it says it submits.** HTML makes an untyped button inside a form its submit,
 * so a click runs its own handler AND the form's: the composer's send would send every message twice, and
 * a key form's cancel saves the key the person is cancelling. A form's submit says
 * `type="submit"`.
 */
export function Button({
  variant = 'default', type = 'button', className, ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: keyof typeof BUTTON }) {
  return (
    <button
      {...props}
      type={type}
      className={cn(
        'inline-flex min-h-[1.9rem] items-center gap-1.5 rounded-control px-3 py-1.5 text-body',
        'transition-colors duration-(--speed)',
        BUTTON[variant],
        className,
      )}
    />
  );
}
