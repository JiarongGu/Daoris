import type { Meta, StoryObj } from '@storybook/react-vite';
import type { ReactElement } from 'react';
import { InTheme } from '../../plugins/storyIcons';
import { WiringFiles } from './WiringFiles';
import { WiringFolders } from './WiringFolders';
import { WiringHead, WiringOlder } from './WiringHead';
import { WiringKnowledge } from './WiringKnowledge';
import { WiringLogs } from './WiringLogs';
import { WiringLoop } from './WiringLoop';
import { WiringService } from './WiringService';
import { WiringTerminal } from './WiringTerminal';
import { WiringToolHome } from './WiringToolHome';
import { WiringVariablesCard } from './WiringVariables';
import { WiringVersions } from './WiringVersions';
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

const narrow = [(Story: () => ReactElement) => <div className="w-[22rem] max-w-full"><Story /></div>];
const none = () => {};

// The six cards after the loop (WIRING1c-ii, §3.4-3.9): each from the installed read and from a development run, in the other
// theme where it has a pair, and narrow.

const UPDATE = { state: 'none', staged: null, mode: null, driven: 0, turns: 0, problem: null, outcome: null, last: null } as const;
const STAGED = { ...UPDATE, state: 'waiting', staged: { id: 'b2', version: '0.22.0' }, last: { phase: 'installed', version: '0.21.0' } } as const;

/** Each settings file with what it holds, one that does not parse and one not there, and the records folded. */
export const FilesRead: Story = { render: () => <WiringFiles files={WIRED.files} onDoor={none} />, decorators: inTheme('light') };

/** The same, in dark. */
export const FilesReadDark: Story = { render: () => <WiringFiles files={WIRED.files} onDoor={none} />, decorators: inTheme('dark') };

/** A development run: a file that could not be read and one not there. */
export const FilesDevelopment: Story = { render: () => <WiringFiles files={DEVELOPMENT.files} onDoor={none} /> };

/** A narrow main area. */
export const FilesNarrow: Story = { render: () => <WiringFiles files={WIRED.files} onDoor={none} />, decorators: narrow };

/** Variables set, with the account's value where it differs and a key as its prefix; the rest folded. */
export const VariablesSet: Story = { render: () => <WiringVariablesCard variables={WIRED.variables} />, decorators: inTheme('light') };

/** The same, in dark. */
export const VariablesSetDark: Story = { render: () => <WiringVariablesCard variables={WIRED.variables} />, decorators: inTheme('dark') };

/** An adopted service's variables are its own. */
export const VariablesAdopted: Story = { render: () => <WiringVariablesCard variables={DEVELOPMENT.variables} /> };

/** A narrow main area. */
export const VariablesNarrow: Story = { render: () => <WiringVariablesCard variables={WIRED.variables} />, decorators: narrow };

/** An install's versions, Node's said by Tools, and nothing staged. */
export const VersionsInstalled: Story = {
  render: () => <WiringVersions versions={WIRED.versions} update={UPDATE} onTools={none} onDriver={none} />, decorators: inTheme('light'),
};

/** The same, in dark. */
export const VersionsInstalledDark: Story = {
  render: () => <WiringVersions versions={WIRED.versions} update={UPDATE} onTools={none} onDriver={none} />, decorators: inTheme('dark'),
};

/** A build staged, and the last update installed. */
export const VersionsStaged: Story = { render: () => <WiringVersions versions={WIRED.versions} update={STAGED} onTools={none} onDriver={none} /> };

/** A development run carries no doctrine tool or driver verbs, and is not updated. */
export const VersionsDevelopment: Story = {
  render: () => <WiringVersions versions={DEVELOPMENT.versions} update={UPDATE} development onTools={none} onDriver={none} />,
};

/** A narrow main area. */
export const VersionsNarrow: Story = {
  render: () => <WiringVersions versions={WIRED.versions} update={UPDATE} onTools={none} onDriver={none} />, decorators: narrow,
};

/** Launchers there, on the account's PATH, another driver found first, and the address file taken. */
export const TerminalInstalled: Story = {
  render: () => <WiringTerminal terminal={WIRED.terminal} variables={WIRED.variables} />, decorators: inTheme('light'),
};

/** The same, in dark. */
export const TerminalInstalledDark: Story = {
  render: () => <WiringTerminal terminal={WIRED.terminal} variables={WIRED.variables} />, decorators: inTheme('dark'),
};

/** A development run has no launchers; the address file's process has ended. */
export const TerminalDevelopment: Story = { render: () => <WiringTerminal terminal={DEVELOPMENT.terminal} variables={DEVELOPMENT.variables} /> };

/** A narrow main area. */
export const TerminalNarrow: Story = { render: () => <WiringTerminal terminal={WIRED.terminal} variables={WIRED.variables} />, decorators: narrow };

/** The store's file, size, the family root and the tier. */
export const KnowledgeRead: Story = {
  render: () => <WiringKnowledge knowledge={WIRED.knowledge} tier="semantic" tierWhy={null} onAi={none} />, decorators: inTheme('light'),
};

/** The same, in dark. */
export const KnowledgeReadDark: Story = {
  render: () => <WiringKnowledge knowledge={WIRED.knowledge} tier="semantic" tierWhy={null} onAi={none} />, decorators: inTheme('dark'),
};

/** A file named by a variable that is not there, and an adopted service's root. */
export const KnowledgeAdopted: Story = {
  render: () => <WiringKnowledge knowledge={DEVELOPMENT.knowledge} tier={null} tierWhy="not-answering" onAi={none} />,
};

/** A narrow main area. */
export const KnowledgeNarrow: Story = {
  render: () => <WiringKnowledge knowledge={WIRED.knowledge} tier="semantic" tierWhy={null} onAi={none} />, decorators: narrow,
};

/** The machine log's folder, size, days kept and newest day, and the engine's log. */
export const LogsRead: Story = { render: () => <WiringLogs logs={WIRED.logs} onOpenFolder={none} onLog={none} />, decorators: inTheme('light') };

/** The same, in dark. */
export const LogsReadDark: Story = { render: () => <WiringLogs logs={WIRED.logs} onOpenFolder={none} onLog={none} />, decorators: inTheme('dark') };

/** A size not measured, no newest day and no engine log. */
export const LogsUnmeasured: Story = { render: () => <WiringLogs logs={DEVELOPMENT.logs} onOpenFolder={none} onLog={none} /> };

/** A narrow main area. */
export const LogsNarrow: Story = { render: () => <WiringLogs logs={WIRED.logs} onOpenFolder={none} onLog={none} />, decorators: narrow };

/** What Daoris's sessions left in the person's own tool home (OWNSPACE2): a read-only list, the records that name each, what keeps it. */
export const ToolHomeListed: Story = {
  render: () => <WiringToolHome toolHome={WIRED.toolHome!} />, decorators: inTheme('light'),
};

/** The same, in dark. */
export const ToolHomeListedDark: Story = {
  render: () => <WiringToolHome toolHome={WIRED.toolHome!} />, decorators: inTheme('dark'),
};

/** A walk under way: each size says Measuring… */
export const ToolHomeMeasuring: Story = { render: () => <WiringToolHome toolHome={MEASURING.toolHome!} measuring /> };

/** Nothing of Daoris's is left there. */
export const ToolHomeNothing: Story = { render: () => <WiringToolHome toolHome={DEVELOPMENT.toolHome!} /> };

/** Daoris's session records could not be read: with no record of a session there, nothing is claimed. */
export const ToolHomeRecordsUnread: Story = {
  render: () => <WiringToolHome toolHome={{ ...WIRED.toolHome!, why: 'records-unread', folders: [] }} />,
};

/** A narrow main area: a long folder name breaks anywhere. */
export const ToolHomeNarrow: Story = { render: () => <WiringToolHome toolHome={WIRED.toolHome!} />, decorators: narrow };

/** A shell older than the page: one line, and Driver's home row is its fallback. */
export const Older: Story = { render: () => <WiringOlder /> };
