import type { Meta, StoryObj } from '@storybook/react-vite';
import type { Session } from '../data/api';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import { InTheme } from '../plugins/storyIcons';
import { SessionHead } from './head/SessionHead';
import { SessionPageHead } from './head/SessionPageHead';

// SESSACCESS1 (D184 point 1; the UI/UX design §3.3 and §4's `AccessFact`, D185): a session's head says the access it started
// under, from its record's pause map. The page head wears *Full access* only where every pause was recorded open; the record's
// *Details*, opened, lists each pause as recorded. No pause open, some open, every one open, one the page does not know, and a
// record from before the field; at the frame's three widths, in English, 中文 and dark.

const TODAY = { permissions: 'auto', routing: 'person', landing: 'person', goAheads: 'named', questions: 'claude-code' };
const FULL = { permissions: 'unrestricted', routing: 'intake', landing: 'rule', goAheads: 'never', questions: 'claude-code' };

const SESSION: Session = {
  id: 's1a2b3c4', quest: 'abc123', repository: 'engine', adapter: 'claude-code', state: 'working', kind: 'driven',
  created: '2026-10-20T09:00:00Z', updated: '2026-10-20T09:12:00Z', profile: 'account-1',
};

/** The page head over the record's head, as Sessions' main area draws them. */
function Heads({ session }: { session: Session }) {
  return (
    <>
      <SessionPageHead
        session={session} title="Fix the FTS index's ranking" shown="working" facts={['engine', '12m']}
        acts={['stop', 'review', 'openFolder', 'copy']} onAct={() => {}}
      />
      <SessionHead session={session} headed />
    </>
  );
}

const meta: Meta<typeof Heads> = {
  title: 'Work/AccessFact',
  component: Heads,
  args: { session: { ...SESSION, access: FULL } },
  decorators: [storyWidths.wide],
  // *Details* opened, in either language, so each story shows the pauses as recorded.
  play: async ({ canvasElement }) => {
    [...canvasElement.querySelectorAll<HTMLButtonElement>('button')]
      .find((button) => /^(Details|详情)/.test(button.textContent ?? ''))?.click();
  },
};
export default meta;

type Story = StoryObj<typeof Heads>;

/** Every pause recorded open: the warning chip, last on the facts line; *Details* lists each pause open. */
export const FullAccess: Story = {};
export const FullAccessHalf: Story = { decorators: [storyWidths.half] };
/** At the main area's floor the facts line wraps before the chip, never cutting it. */
export const FullAccessNarrow: Story = { decorators: [storyWidths.narrow] };
export const FullAccessChinese: Story = { decorators: [storyWidths.half, chinese] };
export const FullAccessDark: Story = { decorators: [storyWidths.narrow, (Story) => <InTheme theme="dark"><Story /></InTheme>] };

/** Today's pauses, an untouched start: no chip, so it reads as it did; *Details* says each. */
export const NoPauseOpen: Story = { args: { session: { ...SESSION, access: TODAY } } };
export const NoPauseOpenChinese: Story = { args: { session: { ...SESSION, access: TODAY } }, decorators: [storyWidths.narrow, chinese] };

/** Some pauses open: no chip; *Details* says which, as recorded. */
export const SomeOpen: Story = { args: { session: { ...SESSION, access: { ...TODAY, permissions: 'unrestricted', landing: 'rule' } } } };

/** A pause the page does not know, recorded by a newer driver: listed by its code, and never counted as open, so no chip. */
export const UnknownPause: Story = { args: { session: { ...SESSION, access: { ...FULL, sandbox: 'off' } } } };

/** A record from before the field: no chip, and *Details* says *Not recorded*. */
export const NotRecorded: Story = { args: { session: { ...SESSION, access: null } } };
export const NotRecordedChinese: Story = { args: { session: { ...SESSION, access: null } }, decorators: [storyWidths.half, chinese] };
