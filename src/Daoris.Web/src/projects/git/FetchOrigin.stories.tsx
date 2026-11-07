import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { FetchOrigin } from './FetchOrigin';

// Fetch origin on a repository's Branches: one press, what will move confirmed, what is
// made, moved and removed said, refused in the driver's sentence, and the line it says where origin's settings refuse it. At the main
// area's three widths (80, 39 and 25rem) in English and 中文, in both themes.

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;
const ADDRESS = 'https://example.test/a-long-organisation-name/a-long-repository-name-that-meets-the-floor.git';
const done = async () => ({ act: 'f1', outcome: 'done', refused: false, said: '', notes: 'Made `origin/feature/a-long-branch-name`. Moved `origin/main`. Removed `origin/old`, which origin no longer has.' });

const meta: Meta<typeof FetchOrigin> = {
  title: 'Repositories/FetchOrigin',
  component: FetchOrigin,
  args: { fetch: { offered: true, address: ADDRESS, press: done } },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof FetchOrigin>;

/** Closed: one press, in the Branches tab beside New branch or tag. */
export const Closed: Story = {};

/** What will move, before the press that sends it. At 80rem. */
export const Confirm: Story = { args: { initial: { step: 'confirm' } } };
export const ConfirmChinese: Story = { args: { initial: { step: 'confirm' } }, decorators: [chinese] };
export const ConfirmDark: Story = { args: { initial: { step: 'confirm' } }, decorators: [dark] };
export const ConfirmChineseDark: Story = { args: { initial: { step: 'confirm' } }, decorators: [chinese, dark] };

/** At 39rem, a 680 px window. */
export const ConfirmHalf: Story = { args: { initial: { step: 'confirm' } }, decorators: [storyWidths.half] };
export const ConfirmHalfChinese: Story = { args: { initial: { step: 'confirm' } }, decorators: [storyWidths.half, chinese] };
export const ConfirmHalfDark: Story = { args: { initial: { step: 'confirm' } }, decorators: [storyWidths.half, dark] };
export const ConfirmHalfChineseDark: Story = { args: { initial: { step: 'confirm' } }, decorators: [storyWidths.half, chinese, dark] };

/** At 25rem, the main area's floor: a long address wraps anywhere, and the presses wrap under it. */
export const ConfirmNarrow: Story = { args: { initial: { step: 'confirm' } }, decorators: [storyWidths.narrow] };
export const ConfirmNarrowChinese: Story = { args: { initial: { step: 'confirm' } }, decorators: [storyWidths.narrow, chinese] };
export const ConfirmNarrowDark: Story = { args: { initial: { step: 'confirm' } }, decorators: [storyWidths.narrow, dark] };
export const ConfirmNarrowChineseDark: Story = { args: { initial: { step: 'confirm' } }, decorators: [storyWidths.narrow, chinese, dark] };

const SAID = 'Fetched origin’s branches. Made `origin/feature/a-long-branch-name`. Moved `origin/main`. Removed `origin/old`, which origin no longer has.';

/** Said: what is made, moved and removed, wrapping anywhere at the floor. */
export const Fetched: Story = { args: { initial: { step: 'said', sentence: SAID } } };
export const FetchedNarrow: Story = { args: { initial: { step: 'said', sentence: SAID } }, decorators: [storyWidths.narrow] };
export const FetchedNarrowChineseDark: Story = {
  args: { initial: { step: 'said', sentence: '已获取 origin 的分支。已创建 `origin/feature/a-long-branch-name`。已移动 `origin/main`。已移除 `origin/old`，origin 已不再有它。' } },
  decorators: [storyWidths.narrow, chinese, dark],
};

/** Refused in the driver's own sentence. */
export const Refused: Story = { args: { initial: { step: 'said', sentence: 'A hold on origin: sign in again, so nothing was fetched.' } } };

/** Not offered where origin's settings would run another program: the line says why, and no press. */
export const SettingRefuses: Story = {
  args: { fetch: { offered: false, reason: 'widened', why: '`remote.origin.uploadpack` is set, which would run another program or mirror instead of fetching origin’s branches', press: done } },
};
export const SettingRefusesNarrowChinese: Story = {
  args: { fetch: { offered: false, reason: 'widened', why: '`remote.origin.uploadpack` is set, which would run another program or mirror instead of fetching origin’s branches', press: done } },
  decorators: [storyWidths.narrow, chinese],
};
