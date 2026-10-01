import { Card, SkeletonRows } from '../ui';

/**
 * **A machine domain's card on its first load** (D118 §3h; FRAME1g, audit ST11 and PL11): skeleton rows in
 * the card's place until the shell's first answer, where the domain drew nothing and a first open read as a
 * blank page under its name. Busy for whoever reads the page, as the main area is while its item loads.
 *
 * @remarks
 * **A first load only**: a domain that has its answer keeps it while it asks again, and one whose question
 * failed says so in a toast, as every card does, rather than loading forever.
 */
export function DomainLoading({ rows = 4 }: { rows?: number }) {
  return (
    // The card's own margin, so a domain's first card loses it as the card it stands in for would.
    <div aria-busy="true" className="mt-3.5">
      <Card><SkeletonRows rows={rows} /></Card>
    </div>
  );
}
