/**
 * The mark: 道衍 as a seal.
 *
 * @remarks
 * **What it says.** The project's own sentence is that 道衍 is *propagation and unfolding, and it
 * runs in three directions* — doctrine outward into the repositories, refinements back, work
 * sideways as quests. So the figure is one source with three arms leaving it, set in a seal.
 *
 * **Why a seal.** A 印章 is square, solid, and designed to be read at the size of a thumbprint,
 * which is exactly the constraint a 18px strip mark and a 16px taskbar icon have. It also belongs
 * beside the 道衍 the wordmark already carries, where a free-floating glyph would not.
 *
 * 🔴 **The first version was three curved arms with no field, and it failed by being looked at.**
 * At 18px the curves read as a bird — thin strokes at that size have no silhouette, and a mark
 * whose meaning needs 64px is not a mark. Solid field, straight arms, one centre: the shape is
 * legible before any of its detail is.
 *
 * The field takes `currentColor`, so a caller places it in the accent and both themes follow; the
 * figure is knocked out in the page's own colour rather than painted, which is what makes it a seal
 * rather than a badge.
 */
export function Mark({ size = 20, className }: { size?: number; className?: string }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      aria-hidden
      focusable="false"
      className={className}
    >
      {/* 🔴 ONE masked rect. The first attempt drew the field, then drew it again through the mask —
          the same colour twice, so there was no hole and the seal rendered as a solid square. A
          knockout is the absence of the field, which means exactly one thing may paint it. */}
      <rect width="24" height="24" rx="5.5" fill="currentColor" mask="url(#daoris-mark)" />
      <mask id="daoris-mark">
        <rect width="24" height="24" fill="white" />
        <g stroke="black" strokeWidth="2.4" strokeLinecap="round">
          {/* One arm, rotated twice — the balance is a property of the construction. Straight and
              short: the whole figure has to survive being 18 pixels wide. */}
          <line x1="12" y1="12" x2="12" y2="5.6" />
          <line x1="12" y1="12" x2="12" y2="5.6" transform="rotate(120 12 12)" />
          <line x1="12" y1="12" x2="12" y2="5.6" transform="rotate(240 12 12)" />
        </g>
        {/* The source. Three arms leaving nothing is a pattern; three arms leaving a point is an
            origin, which is the whole claim. */}
        <circle cx="12" cy="12" r="2.6" fill="black" />
      </mask>
    </svg>
  );
}
