import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { PushRef } from './PushRef';

// Push a branch on a Yours row, or a tag just made (GITAPP2g, n; D241 point 2): the press, what will move on origin confirmed, a tag's
// Dangerous value said, and the door's refusal. At the main area's three widths (80, 39 and 25rem) in English and 中文, in both themes.

const pushed = async () => ({ act: 'p1', outcome: 'done', refused: false, said: '' });
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;
const COMMIT = '5d2e77a1c4f9b03e8d6a2b7c1f4e9d05a3b8c6e2';
const LONG = 'feature/a-name-long-enough-to-meet-the-floor-of-the-main-area';
const PUSH = { offered: true, address: 'ssh://git@example.com/org/engine.git', dangerous: 'deny', press: pushed };
const DENIED = 'Nothing was pushed: daoris: Daoris’s git access refuses a push of a tag from here.';

const meta: Meta<typeof PushRef> = {
  title: 'Repositories/PushRef',
  component: PushRef,
  args: { kind: 'branch', name: 'spike/renderer', commit: COMMIT, push: PUSH },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof PushRef>;

/** The press on a row, before anything is asked. */
export const Idle: Story = {};

/** At 80rem: what moves on origin, before the press that sends it. */
export const Confirm: Story = { args: { initial: { step: 'confirm' } } };
export const ConfirmChinese: Story = { args: { initial: { step: 'confirm' } }, decorators: [chinese] };
export const ConfirmDark: Story = { args: { initial: { step: 'confirm' } }, decorators: [dark] };
export const ConfirmChineseDark: Story = { args: { initial: { step: 'confirm' } }, decorators: [chinese, dark] };

/** A tag: the place's Dangerous value said, deny and allow. */
export const ConfirmTagDenied: Story = { args: { kind: 'tag', name: 'v1.4.0', initial: { step: 'confirm' } } };
export const ConfirmTagDeniedChinese: Story = { args: { kind: 'tag', name: 'v1.4.0', initial: { step: 'confirm' } }, decorators: [chinese] };
export const ConfirmTagAllowed: Story = { args: { kind: 'tag', name: 'v1.4.0', push: { ...PUSH, dangerous: 'allow' }, initial: { step: 'confirm' } } };

/** At 39rem, a 680 px window. */
export const ConfirmHalf: Story = { args: { initial: { step: 'confirm' } }, decorators: [storyWidths.half] };
export const ConfirmHalfChineseDark: Story = { args: { initial: { step: 'confirm' } }, decorators: [storyWidths.half, chinese, dark] };

/** At 25rem, the main area's floor: a long name and the address wrap anywhere, and the presses wrap under them. */
export const ConfirmNarrow: Story = { args: { name: LONG, initial: { step: 'confirm' } }, decorators: [storyWidths.narrow] };
export const ConfirmNarrowChineseDark: Story = { args: { name: LONG, initial: { step: 'confirm' } }, decorators: [storyWidths.narrow, chinese, dark] };

/** The door's refusal of a tag, in its own line. */
export const Refused: Story = { args: { kind: 'tag', name: 'v1.4.0', initial: { step: 'said', sentence: DENIED } } };
export const RefusedNarrowChineseDark: Story = {
  args: { kind: 'tag', name: LONG, initial: { step: 'said', sentence: DENIED } },
  decorators: [storyWidths.narrow, chinese, dark],
};
