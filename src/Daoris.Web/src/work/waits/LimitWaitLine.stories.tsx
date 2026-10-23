import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { LimitWaitLine } from './LimitWaitLine';
import { type LimitWait, limitWaitSaid } from './limitWait';

// A line an account's limit cut off, waiting (LIMITRESUME1d, D206 §3.7): when it resumes by itself and on which account, which resume
// comes, a reset that did not lift the limit, and the person's ways out beside it. Its words are `limitWaitSaid`'s, in the story's
// language, at the main area's width and at 600 px, in both themes.

const nothing = () => {};
const UNTIL = '2026-10-24T11:02:00Z';

function Main({ width, children }: { width: number; children: ReactNode }) {
  return <div className="@container/main max-w-full bg-page p-4" style={{ width }}>{children}</div>;
}

const wide: Decorator = (Story) => <Main width={1180}><Story /></Main>;
const narrow: Decorator = (Story) => <Main width={600}><Story /></Main>;
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

/** A driven line's wait, in the story's language, with *Go on now with…*, *Try now* and *Pause quest…*. */
function Waiting({ own = true, stated = true, notLifted = null, refused = false }: {
  own?: boolean; stated?: boolean; notLifted?: string | null; refused?: boolean;
}) {
  const { t } = useTranslation();
  const wait: LimitWait = { until: UNTIL, account: own ? 'work' : 'home', own, stated, notLifted };
  return (
    <LimitWaitLine
      lines={limitWaitSaid(t, wait, (account) => account ?? t('agents.account.own'))}
      refusal={refused ? t('errors.DRIVER_NOT_READY') : null}
      onGoOnNow={nothing}
      onTryNow={nothing}
      onPause={nothing}
    />
  );
}

const meta: Meta<typeof Waiting> = {
  title: 'Work/A line waiting for its limit',
  component: Waiting,
  decorators: [wide],
};
export default meta;

type Story = StoryObj<typeof Waiting>;

/** Its own account is the first ready: it resumes in its own conversation. */
export const Own: Story = {};

/** The same in 中文. */
export const OwnChinese: Story = { decorators: [chinese] };

/** Another account is ready first: a new session handed Daoris's brief. */
export const Briefed: Story = { args: { own: false } };

/** The same in 中文. */
export const BriefedChinese: Story = { args: { own: false }, decorators: [chinese] };

/** No reset was said, and its last reset did not lift the limit, at 600 px. */
export const NoResetNotLiftedNarrow: Story = { args: { stated: false, notLifted: 'session' }, decorators: [narrow] };

/** The same in 中文 and dark. */
export const NoResetNotLiftedNarrowChineseDark: Story = { args: { stated: false, notLifted: 'session' }, decorators: [chinese, dark, narrow] };

/** *Try now* refused: the sentence said under the presses. */
export const Refused: Story = { args: { refused: true } };
