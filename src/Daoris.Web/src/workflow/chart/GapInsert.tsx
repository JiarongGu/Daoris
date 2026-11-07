import { cn } from '../../lib/cn';

// The ＋ on a way and in a ◎: a small press that opens the step picker for that way. It is no Tab
// stop of its own, so the chart keeps its one: the keyboard adds a step from the step (+) or from the inspector's row.

/** What a ＋ says and does: its name, which says between which steps or at which outcome, and its press. */
export type InsertOffer = { label: string; onPress: () => void };

/**
 * The press is 1.5rem on screen at every scale a canvas draws it at: it grows as the world shrinks (`--canvas-k`), and its
 * negative margin keeps the 1.25rem footprint the ink places it by, so a zoom moves nothing. The circle it draws is the inner mark.
 */
const PRESS = 'size-[max(1.5rem,calc(1.5rem/var(--canvas-k,1)))] -m-[calc((max(1.5rem,calc(1.5rem/var(--canvas-k,1)))_-_1.25rem)/2)]';

export function GapInsert({ offer, className }: { offer: InsertOffer; className?: string }) {
  return (
    <button
      type="button"
      tabIndex={-1}
      aria-label={offer.label}
      title={offer.label}
      data-gap-insert=""
      onClick={(event) => {
        event.stopPropagation();
        offer.onPress();
      }}
      className={cn('group inline-flex shrink-0 cursor-pointer items-center justify-center rounded-full border-0 bg-transparent p-0', PRESS, className)}
    >
      <span
        aria-hidden
        className={cn(
          'inline-flex size-5 items-center justify-center rounded-full border border-line-strong bg-page',
          'text-small leading-none text-ink-soft group-hover:border-ink-soft group-hover:text-ink',
        )}
      >
        ＋
      </span>
    </button>
  );
}
