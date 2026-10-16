import type { Meta, StoryObj } from '@storybook/react-vite';
import type { ReactElement } from 'react';
import { InTheme } from '../plugins/storyIcons';
import { WiringFolders } from './WiringFolders';
import { WiringHead, WiringOlder } from './WiringHead';
import { WiringLoop } from './WiringLoop';
import { WiringService } from './WiringService';
import { DEVELOPMENT, MEASURING, WIRED } from './wiringFixtures';

// Settings → This Daoris's cards (WIRING1c, the this-Daoris design §3.1-3.3, §7), in the shape `SYSTEM_WIRING` answers: an
// install with every size counted, a walk under way, a development run with the account naming another home and a service
// that does not answer, and a shell older than the page; light and dark, and a narrow main area where the rows stack.

const meta = {
  title: 'Settings/This Daoris',
  // A width between the side bar's two states, as the Driver domain's main area has.
  decorators: [(Story) => <div className="grid w-[46rem] max-w-full gap-3.5"><Story /></div>],
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

const inTheme = (theme: 'light' | 'dark') => [(Story: () => ReactElement) => <InTheme theme={theme}><Story /></InTheme>];

/** An install, every size counted, an entry still at its old place and a folder an earlier build left. */
export const FoldersCounted: Story = { render: () => <WiringFolders wiring={WIRED} onMeasure={() => {}} />, decorators: inTheme('light') };

/** The same, in dark. */
export const FoldersCountedDark: Story = { render: () => <WiringFolders wiring={WIRED} onMeasure={() => {}} />, decorators: inTheme('dark') };

/** A walk under way: each size says Measuring… and the button waits. */
export const FoldersMeasuring: Story = { render: () => <WiringFolders wiring={MEASURING} onMeasure={() => {}} /> };

/** A development run: the home named for this start, the account naming another, and the host's notice. */
export const FoldersDevelopment: Story = {
  render: () => <><WiringHead kind="development" /><WiringFolders wiring={DEVELOPMENT} onMeasure={() => {}} /></>,
};

/** A folder that could not be read, and one whose path is unknown. */
export const FoldersUnreadable: Story = {
  render: () => (
    <WiringFolders
      onMeasure={() => {}}
      wiring={{
        ...WIRED,
        folder: { ...WIRED.folder, path: null, pathWhy: 'no-home' },
        folders: [{ ...WIRED.folders[0]!, path: null, pathWhy: 'no-home', bytes: null, bytesWhy: 'unreadable' }],
        leftOver: [],
      }}
    />
  ),
};

/** A narrow main area: the row stacks its value under its label, and a path breaks at its separators. */
export const FoldersNarrow: Story = {
  render: () => <WiringFolders wiring={WIRED} onMeasure={() => {}} />,
  decorators: [(Story) => <div className="w-[22rem] max-w-full"><Story /></div>],
};

/** The service this window started, answering, with its programs and a workspace's remote. */
export const ServiceAnswering: Story = { render: () => <WiringService wiring={WIRED} />, decorators: inTheme('light') };

/** The same, in dark. */
export const ServiceAnsweringDark: Story = { render: () => <WiringService wiring={WIRED} />, decorators: inTheme('dark') };

/** An adopted service that does not answer: why leads, and what could not be read says so in its own row. */
export const ServiceNotAnswering: Story = { render: () => <WiringService wiring={DEVELOPMENT} /> };

/** A narrow main area. */
export const ServiceNarrow: Story = {
  render: () => <WiringService wiring={WIRED} />,
  decorators: [(Story) => <div className="w-[22rem] max-w-full"><Story /></div>],
};

/** This window holds the home, nothing is held, and the loop looks every thirty seconds. */
export const LoopHeld: Story = { render: () => <WiringLoop loop={WIRED.loop} onDriver={() => {}} />, decorators: inTheme('light') };

/** The same, in dark. */
export const LoopHeldDark: Story = { render: () => <WiringLoop loop={WIRED.loop} onDriver={() => {}} />, decorators: inTheme('dark') };

/** A terminal's drive holds the home, an update drains and two repositories are held. */
export const LoopDraining: Story = { render: () => <WiringLoop loop={DEVELOPMENT.loop} onDriver={() => {}} /> };

/** Nobody holds the home and the loop is still starting. */
export const LoopStarting: Story = {
  render: () => <WiringLoop loop={{ ...WIRED.loop, holder: null, looked: null, lookedWhy: 'starting' }} onDriver={() => {}} />,
};

/** A narrow main area. */
export const LoopNarrow: Story = {
  render: () => <WiringLoop loop={WIRED.loop} onDriver={() => {}} />,
  decorators: [(Story) => <div className="w-[22rem] max-w-full"><Story /></div>],
};

/** A shell older than the page: one line, and Driver's home row is its fallback. */
export const Older: Story = { render: () => <WiringOlder /> };
