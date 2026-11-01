import { cn } from '../../lib/cn';

// The ＋ on a way and in a ◎ (FLOWCHART2b; the flowchart design §2): a small press that opens the step picker for that way. It is no Tab
// stop of its own, so the chart keeps its one: the keyboard adds a step from the step (+) or from the inspector's row.

/** What a ＋ says and does: its name, which says between which steps or at which outcome, and its press. */
export type InsertOffer = { label: string; onPress: () => void };

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
      className={cn(
        'inline-flex size-5 shrink-0 cursor-pointer items-center justify-center rounded-full border border-line-strong bg-page p-0',
        'text-small leading-none text-ink-soft hover:border-ink-soft hover:text-ink',
        className,
      )}
    >
      <span aria-hidden>＋</span>
    </button>
  );
}
