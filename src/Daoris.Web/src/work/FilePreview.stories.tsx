import type { Meta, StoryObj } from '@storybook/react-vite';
import { FilePreview } from './FilePreview';
import type { TreeFile } from './preview';

// A file's preview (PREVIEW1, D111), in the side bar at its floor (300px) and at a roomier width. Every
// state is a file passed in: read, marked, bounded, binary, empty, refused, reading, with its changes.

const SOURCE = [
  'use crate::budget::Budget;',
  '',
  '/// Streams the world in, one region at a time.',
  'pub struct Streamer {',
  '    regions: Vec<Region>,',
  '}',
  '',
  'impl Streamer {',
  '    /* One region at a time, and never more than',
  '       the frame\'s budget allows. */',
  '    pub fn hydrate(region: &Region, budget: &mut Budget) {',
  '        for tile in &region.tiles {',
  '            if budget.spent() {',
  '                break;',
  '            }',
  '            load(tile); // 每帧上限',
  '        }',
  '    }',
  '}',
  '',
].join('\n');

const FILE: TreeFile = {
  session: 'c0ffee11', path: 'src/world/streaming/chunk.rs', size: SOURCE.length, binary: false, text: SOURCE, truncated: false,
};

const PATCH = [
  '@@ -9,7 +9,11 @@ impl Streamer {',
  '-    pub fn hydrate(region: &Region) {',
  '+    /* One region at a time, and never more than',
  '+       the frame\'s budget allows. */',
  '+    pub fn hydrate(region: &Region, budget: &mut Budget) {',
  '         for tile in &region.tiles {',
  '+            if budget.spent() {',
  '+                break;',
  '+            }',
  '             load(tile);',
].join('\n');

const meta: Meta<typeof FilePreview> = {
  title: 'Work/FilePreview',
  component: FilePreview,
  args: { path: FILE.path, file: FILE, onReload: () => {} },
  decorators: [(Story) => (
    <div className="flex h-96 w-[300px] flex-col border border-line bg-page">
      <Story />
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof FilePreview>;

/** Read whole: the path relative to the tree, numbered lines, highlighted in its language. At the floor. */
export const Read: Story = {};

/** The lines a read named (its offset and limit), marked by a bar and a wash, said in words, and scrolled to. */
export const MarkedLines: Story = { args: { lines: { from: 11, to: 16 } } };

/** Where the review holds a patch for the file: *File · Changes*, the switch wrapping under the size at the floor. */
export const WithItsChanges: Story = { args: { patch: PATCH } };

/** Past the bound: the first lines, and a sentence saying how much the file holds and where the rest is. */
export const Bounded: Story = { args: { file: { ...FILE, size: 3 * 1024 * 1024, truncated: true } } };

/** A binary file, said in a sentence with its size. */
export const Binary: Story = {
  args: { path: 'assets/logo.png', file: { ...FILE, path: 'assets/logo.png', binary: true, text: null, size: 48_213 } },
};

/** An empty file, said rather than drawn as nothing. */
export const Empty: Story = { args: { path: 'src/empty.rs', file: { ...FILE, path: 'src/empty.rs', text: '', size: 0 } } };

/** The host's refusal, as it said it: here a path through a link that leads out of the tree. */
export const Refused: Story = {
  args: {
    path: 'vendor/escape/secret.txt', file: null,
    refusal: '`vendor/escape/secret.txt` goes through a link that leads out of this session\'s tree, so the preview does not read it.',
  },
};

/** Named while it is read. */
export const Reading: Story = { args: { file: null, pending: true } };

/**
 * A 中文 path longer than the floor, which gives way at its front, and a long line that scrolls sideways
 * rather than wrapping, since a file's lines are the file's.
 */
export const LongAndChinese: Story = {
  args: {
    path: 'docs/设计/工作树里的一个很长的文件名.md',
    file: {
      ...FILE, path: 'docs/设计/工作树里的一个很长的文件名.md',
      text: '# 预览\n\n这一行很长，很长，很长，很长，很长，很长，很长，很长，很长，很长，很长，很长，很长，很长，很长，很长。\n',
    },
  },
};
