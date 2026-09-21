import type { Meta, StoryObj } from '@storybook/react-vite';
import type { DiffFile } from './diff';
import { DiffFileRow } from './DiffFileRow';

// Every state of a review row (SURF6), on the shipped component — including the ones a real diff
// rarely produces. A molecule, so each is reached by passing props (components plan §2).

const PATCH = `diff --git a/src/chunk.ts b/src/chunk.ts
index 3f2a1b8..9c4d2e1 100644
--- a/src/chunk.ts
+++ b/src/chunk.ts
@@ -12,7 +12,9 @@ export function hydrate(region: Region): void {
-  for (const tile of region.tiles) load(tile);
+  const budget = streamingBudget();
+  for (const tile of region.tiles) {
+    if (budget.spent()) break;
+    load(tile);
+  }
 }`;

const FILE: DiffFile = {
  path: 'src/world/streaming/chunk.ts',
  status: 'modified',
  added: 4,
  removed: 1,
  patch: PATCH,
};

const meta: Meta<typeof DiffFileRow> = {
  title: 'Work/DiffFileRow',
  component: DiffFileRow,
  args: { file: FILE, open: false, viewed: false, onToggle: () => {}, onViewed: () => {} },
  decorators: [(Story) => (
    <ul className="m-0 w-[26rem] list-none border border-line bg-raised p-0"><Story /></ul>
  )],
};
export default meta;

type Story = StoryObj<typeof DiffFileRow>;

export const Collapsed: Story = {};

export const Open: Story = { args: { open: true } };

/** Ticked off. It dims rather than disappearing — the count still has to add up. */
export const Viewed: Story = { args: { viewed: true } };

export const Added: Story = {
  args: { file: { ...FILE, path: 'src/world/streaming/budget.ts', status: 'added', added: 61, removed: 0 } },
};

export const Deleted: Story = {
  args: { file: { ...FILE, path: 'src/world/legacy/loader.ts', status: 'deleted', added: 0, removed: 214 } },
};

export const Renamed: Story = {
  args: { file: { ...FILE, path: 'src/world/streaming/hydrate.ts', status: 'renamed', added: 2, removed: 2 } },
};

/** A binary file was never counted — and "not counted" must not render as "changed nothing". */
export const Binary: Story = {
  args: { file: { path: 'assets/logo.png', status: 'modified', added: null, removed: null, patch: null }, open: true },
};

/** The bound dropped this patch. The file is still listed, and it says where the rest is. */
export const PatchDropped: Story = {
  args: { file: { ...FILE, path: 'src/generated/schema.ts', added: 18_402, removed: 17_991, patch: null }, open: true },
};

/** A deep path truncates at the FRONT, because the filename is the half a person is looking for. */
export const ALongPath: Story = {
  args: {
    open: false,
    file: {
      ...FILE,
      path: 'src/world/streaming/regions/hydration/internal/chunk-budget-resolver.ts',
    },
  },
};

/** 中文 in a path and in a patch — the console is bilingual and a diff is content, never translated. */
export const InChinese: Story = {
  args: {
    open: true,
    file: {
      ...FILE,
      path: 'docs/世界流式加载.md',
      patch: '@@ -1,3 +1,4 @@\n 世界流式加载引擎\n-每帧不设上限\n+每帧的水化工作有上限\n+超出预算即停止',
    },
  },
};
