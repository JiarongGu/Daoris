import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { GoAhead, Session } from '../../data/api';
import { chinese } from '../../design/storyLanguage';
import { MAIN_WIDTHS } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import { BackToBottom, ToTheTop } from '../conversation/SessionConversation';
import { SessionBox } from '../conversation/SessionBox';
import type { ParkQuestion } from './parkQuestion';
import { WaitPanel } from './WaitPanel';
import { useWaitPanelHeight } from './waitPanelHeight';
import { WaitStrip } from './WaitStrip';

// The waiting panel (WAITPANEL1): a session's column as the frame lays it out, its conversation scrolled up from the tail above
// the panel, and the panel holding the strip and a park's box. Its top edge sizes it, kept per viewer; *To the top* and *Back to
// bottom* lie above that edge over the conversation's foot. In both languages and both themes, at the floor and kept tall.

const nothing = () => {};
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const PARKED: Session = {
  id: 's1a2b3c4', quest: '7a82cc', repository: 'engine', adapter: 'claude-code', state: 'awaiting-person', kind: 'driven',
  created: '2026-10-22T08:46:00Z', updated: '2026-10-22T09:05:00Z',
};

const ASKED: ParkQuestion = {
  id: 'q5t10n00',
  question: 'Which quarterly target should the dashboard tile show? The ticket says "the new quarterly target" without a number.',
  choices: [
    { label: '1,200', does: 'Write 1,200, the planning sheet\'s value, which the ticket links; development already holds it.' },
    { label: '1,350', does: 'Write 1,350, the value in the last stand-up note.' },
    { label: 'Leave it for now', does: 'Ship the tile reading the configuration, and leave the value as it is.' },
  ],
  recommend: 1,
  by: 'before Friday\'s release window',
  at: '2026-10-22T09:05:00Z',
};

const PRODUCTION: GoAhead = {
  number: 1, kind: 'write', on: 'production', act: 'dashboard configuration', state: 'asked',
  asked: [{ session: 's1a2b3c4', quest: '7a82cc', at: '2026-10-22T09:04:00Z', why: 'The tile reads its target from it.' }],
};

const LANDS = { branch: 'daoris/s-1a2b3c4', tree: 's-1a2b3c4', commits: 2, uncommitted: 0 };

/** What the panel holds: a park's box alone, the strip over it, or a running box with nothing waiting. */
type Holds = 'park' | 'stripAndPark' | 'nothing';

/** The column: the conversation scrolling in the main area, with a sticky line of its own, and the panel under it. */
function Column({ height, onResize, onReset, holds }: {
  height: number;
  onResize: (height: number) => void;
  onReset?: () => void;
  holds: Holds;
}) {
  return (
    <Tooltip.Provider>
      <div data-story-column="" style={{ width: MAIN_WIDTHS.half }} className="isolate flex h-[40rem] max-w-full flex-col bg-page text-ink">
        <main data-region="main" className="@container/main relative min-h-0 flex-1 overflow-y-auto px-4 py-3">
          <div className="sticky top-0 z-[9] border-b border-line bg-page py-1 text-small text-ink-faint">(the conversation's find line)</div>
          {Array.from({ length: 40 }, (_, index) => (
            <p key={index} className="my-2 text-body">Line {index + 1} of what the agent said while it worked.</p>
          ))}
        </main>
        <WaitPanel
          height={height}
          onResize={onResize}
          onReset={onReset}
          controls={(
            <>
              <ToTheTop onPress={nothing} className="bg-overlay shadow-sm" />
              <BackToBottom onPress={nothing} className="bg-overlay shadow-sm" />
            </>
          )}
        >
          {holds === 'stripAndPark' && (
            <WaitStrip session={PARKED} answerBelow parkBox lands={LANDS} landing={{ session: 's1a2b3c4', form: 'branch', target: 'main' }} onLand={nothing} />
          )}
          {holds === 'nothing'
            ? <SessionBox box={{ kind: 'steer' }} held={{ queued: [], taking: true }} onSend={nothing} />
            : (
              <SessionBox
                box={{ kind: 'say', mode: 'answer' }}
                onSend={nothing}
                park={{
                  quest: '7a82cc', tree: 's-1a2b3c4', since: new Date(Date.now() - 12 * 60_000).toISOString(),
                  question: ASKED, goAheads: [PRODUCTION], onChoice: nothing, onGoAhead: nothing,
                }}
              />
            )}
        </WaitPanel>
      </div>
    </Tooltip.Provider>
  );
}

/** The column with the height kept per viewer: a drag or an arrow keeps it, a double-click forgets it. */
function Kept({ holds }: { holds: Holds }) {
  const panel = useWaitPanelHeight();
  return <Column height={panel.height} onResize={panel.resize} onReset={panel.reset} holds={holds} />;
}

const meta: Meta<typeof Kept> = {
  title: 'Work/Waiting panel',
  component: Kept,
  args: { holds: 'park' },
};
export default meta;

type Story = StoryObj<typeof Kept>;

/** A park's ask in its box, at the height this viewer kept (240 px where none is kept): drag its top edge, or focus it and ↑ ↓. */
export const Parked: Story = {};

/** The same in 中文. */
export const ParkedChinese: Story = { decorators: [chinese] };

/** The same in dark. */
export const ParkedDark: Story = { decorators: [dark] };

/** The same in dark and 中文. */
export const ParkedDarkChinese: Story = { decorators: [chinese, dark] };

/** The strip over the park's box: each takes half the height, and scrolls within it. */
export const StripAndPark: Story = { args: { holds: 'stripAndPark' } };

/** The same in dark and 中文. */
export const StripAndParkDarkChinese: Story = { args: { holds: 'stripAndPark' }, decorators: [chinese, dark] };

/** Nothing waits on the person: no handle, and the conversation's presses still above the box. */
export const NothingWaiting: Story = { args: { holds: 'nothing' } };

/** At the floor: a line of what waits, the rest scrolled, the conversation given the column. */
export const AtTheFloor: Story = { render: () => <Column height={72} onResize={nothing} holds="park" /> };

/** Kept tall, past half the window: held at half, so the conversation above stays readable. */
export const KeptTall: Story = { render: () => <Column height={2000} onResize={nothing} holds="stripAndPark" /> };
