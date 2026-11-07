import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { NewRef } from './NewRef';

// New branch or tag on a repository's Branches: closed, the name asked, what will be made
// confirmed, made with its Undo, refused in the driver's sentence, and not known. At the main area's three widths (80, 39 and 25rem)
// in English and 中文, in both themes.

const nothing = async () => ({ refused: false, said: '' });
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;
const AT = { label: 'main', commit: 'c4f1a2e0b7d94c3a8f56e1d20b9a7c4e3f8d1a65' };

const meta: Meta<typeof NewRef> = {
  title: 'Repositories/NewRef',
  component: NewRef,
  args: { at: AT, acts: { make: nothing, remove: nothing, rename: nothing, finish: nothing, undo: nothing } },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof NewRef>;

/** Closed: one press, in the Branches tab beside the line. */
export const Closed: Story = {};

/** At 80rem: the name asked, the branch chosen. */
export const Wide: Story = { args: { initial: { name: 'topic' } } };
export const WideChinese: Story = { args: { initial: { name: 'topic' } }, decorators: [chinese] };
export const WideDark: Story = { args: { initial: { name: 'topic' } }, decorators: [dark] };
export const WideChineseDark: Story = { args: { initial: { name: 'topic' } }, decorators: [chinese, dark] };

/** At 39rem, a 680 px window. */
export const Half: Story = { args: { initial: { name: 'topic' } }, decorators: [storyWidths.half] };
export const HalfChinese: Story = { args: { initial: { name: 'topic' } }, decorators: [storyWidths.half, chinese] };
export const HalfDark: Story = { args: { initial: { name: 'topic' } }, decorators: [storyWidths.half, dark] };
export const HalfChineseDark: Story = { args: { initial: { name: 'topic' } }, decorators: [storyWidths.half, chinese, dark] };

/** At 25rem, the main area's floor: a long name wraps anywhere, and the presses wrap under it. */
export const Narrow: Story = { args: { initial: { name: 'feature/a-name-long-enough-to-meet-the-floor-of-the-main-area' } }, decorators: [storyWidths.narrow] };
export const NarrowChinese: Story = { args: { initial: { name: 'feature/a-name-long-enough-to-meet-the-floor-of-the-main-area' } }, decorators: [storyWidths.narrow, chinese] };
export const NarrowDark: Story = { args: { initial: { name: 'feature/a-name-long-enough-to-meet-the-floor-of-the-main-area' } }, decorators: [storyWidths.narrow, dark] };
export const NarrowChineseDark: Story = { args: { initial: { name: 'feature/a-name-long-enough-to-meet-the-floor-of-the-main-area' } }, decorators: [storyWidths.narrow, chinese, dark] };

/** What will be made, before the press that sends it. */
export const ConfirmBranch: Story = { args: { initial: { stage: 'confirm', name: 'topic' } } };
export const ConfirmTag: Story = { args: { initial: { stage: 'confirm', kind: 'tag', name: 'v1' } } };
export const ConfirmNarrowChinese: Story = { args: { initial: { stage: 'confirm', kind: 'tag', name: 'v1' } }, decorators: [storyWidths.narrow, chinese] };

/** Made, with the Undo that deletes the ref only at the id it made. */
export const Made: Story = { args: { initial: { stage: 'made', name: 'topic' } } };
export const MadeNarrowDark: Story = { args: { initial: { stage: 'made', kind: 'tag', name: 'v1' } }, decorators: [storyWidths.narrow, dark] };

/** Refused in the driver's own sentence, the name kept to change. */
export const Refused: Story = {
  args: { initial: { stage: 'said', name: 'main', sentence: 'a branch named `main` already exists, so nothing was created' } },
};

/** Not known: said, and nothing retried. */
export const Unknown: Story = { args: { initial: { stage: 'unknown', name: 'topic' } } };
export const UnknownChineseDark: Story = { args: { initial: { stage: 'unknown', name: 'topic' } }, decorators: [chinese, dark] };
