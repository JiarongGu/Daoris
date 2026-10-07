import type { Meta, StoryObj } from '@storybook/react-vite';

// The token gallery — the design language's raw material, on the shipped variables. The status set
// is the COMPUTED palette (docs/2026-09-19-platform-ux.md): both themes pass all six checks of the
// visualization validator, and a status never ships without its text label.

function Swatch({ name, variable, note }: { name: string; variable: string; note?: string }) {
  return (
    <div className="flex items-center gap-3 py-1.5">
      <span
        className="size-8 shrink-0 rounded-control border border-line"
        style={{ background: `var(${variable})` }}
      />
      <span className="w-32 font-mono text-small">{name}</span>
      <span className="font-mono text-meta text-ink-faint">{variable}</span>
      {note && <span className="text-small text-ink-soft">— {note}</span>}
    </div>
  );
}

function Gallery() {
  return (
    <div className="grid max-w-2xl gap-6">
      <section>
        <h2 className="mb-2 text-body font-semibold">Surfaces & ink</h2>
        <Swatch name="page" variable="--page" />
        <Swatch name="sunken" variable="--sunken" note="the box a move asks once in" />
        <Swatch name="raised" variable="--raised" note="cards, controls" />
        <Swatch name="overlay" variable="--overlay" note="drawers, toasts" />
        <Swatch name="line" variable="--line" />
        <Swatch name="ink" variable="--ink" />
        <Swatch name="ink-soft" variable="--ink-soft" />
        <Swatch name="ink-faint" variable="--ink-faint" />
        <Swatch name="ink-danger" variable="--ink-danger" note="red drawn as words; a fill keeps st-declined" />
      </section>
      <section>
        <h2 className="mb-2 text-body font-semibold">Accent — the interactive identity, never a status</h2>
        <Swatch name="accent" variable="--accent" note="buttons, active nav, links, single-series bars" />
        <Swatch name="accent-soft" variable="--accent-soft" />
      </section>
      <section>
        <h2 className="mb-2 text-body font-semibold">Quest states — computed, not tasted</h2>
        <Swatch name="st-open" variable="--st-open" note="waiting" />
        <Swatch name="st-taken" variable="--st-taken" note="in progress" />
        <Swatch name="st-done" variable="--st-done" />
        <Swatch name="st-declined" variable="--st-declined" />
        <Swatch name="ink-open" variable="--ink-open" note="open's words; a fill keeps st-open" />
        <Swatch name="ink-taken" variable="--ink-taken" note="taken's words" />
        <Swatch name="ink-done" variable="--ink-done" note="done's words" />
        <p className="mt-2 max-w-xl text-small text-ink-soft">
          Light passes the six validator checks at worst adjacent deutan ΔE 13.3 (normal 21.0); dark is
          its own validated set at 9.2 / 17.5 — not a filter. The red/green pair is separated by
          lightness as well as hue.
        </p>
      </section>
      <section>
        <h2 className="mb-2 text-body font-semibold">Type scale</h2>
        <p className="text-value font-semibold leading-tight">2.0 — tile values</p>
        <p className="font-serif text-wordmark font-semibold">1.5 — the wordmark, the one serif</p>
        <p className="text-view font-[650]">1.25 — view titles</p>
        <p className="text-title font-[650]">1.05 — drawer titles</p>
        <p className="text-body">0.95 — body</p>
        <p className="text-body text-ink-soft">0.875 — secondary</p>
        <p className="font-mono text-meta text-ink-faint">0.72 mono — meta</p>
      </section>
    </div>
  );
}

const meta: Meta<typeof Gallery> = {
  title: 'Design/Tokens',
  component: Gallery,
};
export default meta;

export const Tokens: StoryObj<typeof Gallery> = {};
