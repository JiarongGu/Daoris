import type { Meta, StoryObj } from '@storybook/react-vite';

// The token gallery (D330, the window design §7): the role set on the shipped variables. Components name these roles,
// never a base colour; `roles.test.ts` holds every text pair at 4.5:1 and every mark at 3:1 in both themes.

function Swatch({ name, note }: { name: string; note?: string }) {
  return (
    <div className="flex items-center gap-3 py-1.5">
      <span className="size-8 shrink-0 rounded-control border border-line" style={{ background: `var(--${name})` }} />
      <span className="w-32 font-mono text-mono">{name}</span>
      {note && <span className="text-small text-ink-soft">{note}</span>}
    </div>
  );
}

function Gallery() {
  return (
    <div className="grid gap-6">
      <section>
        <h2 className="mb-2 text-heading font-semibold">Surfaces, lines and ink</h2>
        <Swatch name="page" note="the window behind panes" />
        <Swatch name="raised" note="panes, cards, rows" />
        <Swatch name="sunken" note="code wells, the steer box, the owner's own lines" />
        <Swatch name="overlay" note="menus, popovers, sheets, toasts" />
        <Swatch name="hover" note="a row or quiet control under the pointer" />
        <Swatch name="line-subtle" note="row separators" />
        <Swatch name="line" note="card edges" />
        <Swatch name="line-strong" note="control edges, 3:1" />
        <Swatch name="ink" />
        <Swatch name="ink-soft" />
        <Swatch name="ink-faint" note="metadata, 4.5:1" />
        <Swatch name="ink-disabled" note="a disabled control's label only" />
      </section>
      <section>
        <h2 className="mb-2 text-heading font-semibold">The accent: what can be pressed, or moves</h2>
        <Swatch name="accent" note="primary button, focus ring, selected rail" />
        <Swatch name="accent-ink" note="links and words that act" />
        <Swatch name="accent-soft" note="a selected row" />
        <Swatch name="live" note="● moving: the accent's second name" />
        <Swatch name="live-soft" note="a live band" />
      </section>
      <section>
        <h2 className="mb-2 text-heading font-semibold">The owner's amber, and the outcomes</h2>
        <Swatch name="you" note="◆ only what waits on the owner" />
        <Swatch name="you-ink" />
        <Swatch name="you-soft" note="a wait card's field" />
        <Swatch name="fail" note="▲ failed; declined and stopped are neutral" />
        <Swatch name="fail-ink" />
        <Swatch name="fail-soft" />
        <Swatch name="ok" note="✓ an outcome, never a large fill" />
        <Swatch name="ok-ink" />
        <Swatch name="ok-soft" />
      </section>
      <section>
        <h2 className="mb-2 text-heading font-semibold">Type scale</h2>
        <p className="text-display font-semibold">20 display: the one title of a screen</p>
        <p className="text-heading font-semibold">16 heading: a section, an object's title</p>
        <p className="text-body">14 body: card text, the timeline, forms</p>
        <p className="text-small text-ink-soft">13 small: rows, menus, controls</p>
        <p className="text-caption text-ink-faint">12 caption: metadata, times, badges</p>
        <p className="font-mono text-mono text-ink-soft">13 mono: code, paths, hashes</p>
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
