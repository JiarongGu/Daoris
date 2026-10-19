import type { Meta, StoryObj } from '@storybook/react-vite';
import { PatchView } from './PatchView';

// A file's patch drawn (REVIEW2): numbered, highlighted in the file's language, unified or side by
// side. Each state is a patch passed in.

const RUST = [
  'diff --git a/src/world/chunk.rs b/src/world/chunk.rs',
  '--- a/src/world/chunk.rs',
  '+++ b/src/world/chunk.rs',
  '@@ -10,7 +10,11 @@ impl Streamer {',
  '     /// Hydrate every tile in the region.',
  '-    pub fn hydrate(region: &Region) {',
  '+    /* One region at a time, and never more than',
  '+       the frame\'s budget allows. */',
  '+    pub fn hydrate(region: &Region, budget: &mut Budget) {',
  '         for tile in &region.tiles {',
  '+            if budget.spent() {',
  '+                break;',
  '+            }',
  '             load(tile);',
  '         }',
  '     }',
  '@@ -80,3 +84,3 @@ mod tests {',
  '-    const CAP: usize = 0;',
  '+    const CAP: usize = 4; // 每帧上限',
  '     #[test]',
  '\\ No newline at end of file',
].join('\n');

const meta: Meta<typeof PatchView> = {
  title: 'Work/PatchView',
  component: PatchView,
  args: { patch: RUST, path: 'src/world/chunk.rs', layout: 'unified' },
  decorators: [(Story) => <div className="max-w-3xl border border-line bg-raised"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof PatchView>;

/** One column: both sides numbered, each change signed, a comment that spans lines coloured on each. */
export const Unified: Story = {};

/** The old side beside the new: each removal across from what replaced it. */
export const SideBySide: Story = { args: { layout: 'split' } };

/** A file in a language the highlighter does not know: shown plain, never guessed. */
export const UnknownLanguage: Story = { args: { path: 'notes/budget.txt' } };

/** A rename with no change inside it: what git said is the whole of it. */
export const RenameOnly: Story = {
  args: { patch: 'diff --git a/a.md b/b.md\nsimilarity index 100%\nrename from docs/a.md\nrename to docs/b.md', path: 'docs/b.md' },
};

/** A line longer than the pane: it scrolls in its own box rather than widening the review. */
export const LongLine: Story = {
  args: { patch: `@@ -1 +1 @@\n-const x = 1;\n+const x = ${'"a very long string, "'.repeat(12)};`, path: 'src/long.ts' },
};
