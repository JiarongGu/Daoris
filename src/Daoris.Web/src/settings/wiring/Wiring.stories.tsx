import type { Meta, StoryObj } from '@storybook/react-vite';
import type { ReactElement } from 'react';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { CardStack } from '../../design/ui';
import { InTheme } from '../../plugins/storyIcons';
import { WiringCards } from './WiringCards';
import { WiringDownloads } from './WiringDownloads';
import { WiringFiles } from './WiringFiles';
import { WiringFolders } from './WiringFolders';
import { WiringHead, WiringOlder } from './WiringHead';
import { WiringKnowledge } from './WiringKnowledge';
import { WiringLogs } from './WiringLogs';
import { WiringLoop } from './WiringLoop';
import { WiringService } from './WiringService';
import { WiringSpace } from './WiringSpace';
import { WiringTerminal } from './WiringTerminal';
import { WiringToolHome } from './WiringToolHome';
import { WiringVariablesCard } from './WiringVariables';
import { WiringVersions } from './WiringVersions';
import {
  DEVELOPMENT, DOWNLOADS, MEASURING, SPACE, SPACE_COUNTING, SPACE_SET, SPACE_STOPPED, SPACE_UNCOUNTED, WIRED,
} from './wiringFixtures';

// Settings → This Daoris's cards (WIRING1c, the this-Daoris design §3.1-3.3, §7), in the shape `SYSTEM_WIRING` answers: an
// install with every size counted, a walk under way, a development run with the account naming another home and a service
// that does not answer, and a shell older than the page; light and dark, and a narrow main area where the rows stack. The
// whole domain at the frame's three widths, in English and 中文 (the UI/UX design R6, UIFIT1): every row's value on the
// card's track, the cards 16 px apart by the domain's own container.

const meta = {
  title: 'Settings/This Daoris',
  // The main area at its wide width, as the frame gives it (R6): no frame of the story's own.
  decorators: [storyWidths.wide],
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

const inTheme = (theme: 'light' | 'dark') => [(Story: () => ReactElement) => <InTheme theme={theme}><Story /></InTheme>];
/** The main area at its 400 px floor: the rows stack. */
const narrow = [storyWidths.narrow];

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
export const FoldersNarrow: Story = { render: () => <WiringFolders wiring={WIRED} onMeasure={() => {}} />, decorators: narrow };

// CACHECLEAR1: the Downloads row's clearing, open under its row: the list with what keeps each version, and the press's answer.
const clearing = (props: Partial<Parameters<typeof WiringDownloads>[0]> = {}) => (
  <WiringFolders
    wiring={WIRED}
    onMeasure={() => {}}
    downloads={<WiringDownloads listing={DOWNLOADS} onClear={() => {}} onClose={() => {}} {...props} />}
  />
);

/** The Downloads row offers its clearing. */
export const DownloadsOffered: Story = { render: () => <WiringFolders wiring={WIRED} onMeasure={() => {}} onDownloads={() => {}} /> };

/** The list: two versions nothing uses, and what keeps each of the rest. */
export const DownloadsListed: Story = { render: () => clearing(), decorators: inTheme('light') };

/** The same, in dark. */
export const DownloadsListedDark: Story = { render: () => clearing(), decorators: inTheme('dark') };

/** The list on its way. */
export const DownloadsListing: Story = { render: () => clearing({ listing: undefined }) };

/** Every version in use: nothing to remove. */
export const DownloadsAllKept: Story = {
  render: () => clearing({ listing: { why: null, versions: DOWNLOADS.versions.filter((row) => row.keeps) } }),
};

/** After the press: one removed, and one the driver kept because a program runs from it. */
export const DownloadsCleared: Story = {
  render: () => clearing({
    cleared: [
      { kind: 'agent', name: 'claude-code', version: '2.0.1', unfinished: false, outcome: 'removed', bytes: 120_000_000 },
      { kind: 'tool', name: 'gh', version: '2.61.0', unfinished: false, outcome: 'kept', bytes: 30_000_000, keeps: 'in-use', by: [] },
    ],
  }),
};

/** A narrow main area, in 中文. */
export const DownloadsNarrowChinese: Story = { render: () => clearing(), decorators: [...narrow, chinese] };

/** The service this window started, answering, with its programs and a workspace's remote. */
export const ServiceAnswering: Story = { render: () => <WiringService wiring={WIRED} />, decorators: inTheme('light') };

/** The same, in dark. */
export const ServiceAnsweringDark: Story = { render: () => <WiringService wiring={WIRED} />, decorators: inTheme('dark') };

/** An adopted service that does not answer: why leads, and what could not be read says so in its own row. */
export const ServiceNotAnswering: Story = { render: () => <WiringService wiring={DEVELOPMENT} /> };

/** A narrow main area. */
export const ServiceNarrow: Story = { render: () => <WiringService wiring={WIRED} />, decorators: narrow };

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
export const LoopNarrow: Story = { render: () => <WiringLoop loop={WIRED.loop} onDriver={() => {}} />, decorators: narrow };

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

// KEEP1c (the cleanup design §4.1, §4.2): the Disk space card, each kind with its size, rule, next and last clean, in every state
// but the editors and the floor's (KEEP1d, KEEP1f), at the three widths in English and 中文.
const space = (props: Partial<Parameters<typeof WiringSpace>[0]> = {}) => (
  <WiringSpace space={SPACE} onCount={() => {}} onDownloads={() => {}} {...props} />
);

/** Every kind counted, each rule that runs with its next and last; light. */
export const SpaceCounted: Story = { render: () => space(), decorators: inTheme('light') };

/** The same, in dark. */
export const SpaceCountedDark: Story = { render: () => space(), decorators: inTheme('dark') };

/** The same at the half width. */
export const SpaceHalf: Story = { render: () => space(), decorators: [storyWidths.half] };

/** The same at the 400 px floor: each value under its words. */
export const SpaceNarrow: Story = { render: () => space(), decorators: narrow };

/** In 中文, wide. */
export const SpaceChinese: Story = { render: () => space(), decorators: [chinese] };

/** In 中文 at the half width. */
export const SpaceChineseHalf: Story = { render: () => space(), decorators: [storyWidths.half, chinese] };

/** In 中文 at the floor. */
export const SpaceChineseNarrow: Story = { render: () => space(), decorators: [...narrow, chinese] };

/** After a start: no kind counted yet, each with its Count. */
export const SpaceUncounted: Story = { render: () => space({ space: SPACE_UNCOUNTED }) };

/** A count under way and one that stopped short, a record that does not read, and a terminal's run under way. */
export const SpaceCounting: Story = { render: () => space({ space: SPACE_COUNTING }) };

/** The same in 中文 at the floor. */
export const SpaceCountingChineseNarrow: Story = { render: () => space({ space: SPACE_COUNTING }), decorators: [...narrow, chinese] };

/** A run a crash cut short, said until the next run ends. */
export const SpaceStopped: Story = { render: () => space({ space: SPACE_STOPPED }) };

/** The Downloads row's list, opened from this card's row. */
export const SpaceDownloads: Story = {
  render: () => space({ downloads: <WiringDownloads listing={DOWNLOADS} onClear={() => {}} onClose={() => {}} /> }),
};

// KEEP1d (the cleanup design §4.1, §4.2): the rules the person set, and a rule's editor open under its row, at the three widths in
// English and 中文.
const keep = () => {};

/** The rules as the person set them, each with its Change…. */
export const SpaceRulesSet: Story = { render: () => space({ space: SPACE_SET, onKeep: keep }) };

/** The set-aside folders' editor open: its days, its bounds, keep each folder whole; Save, The default, Never mind. */
export const SpaceEditing: Story = { render: () => space({ onKeep: keep, editing: 'set-aside' }) };

/** The same at the half width. */
export const SpaceEditingHalf: Story = { render: () => space({ onKeep: keep, editing: 'set-aside' }), decorators: [storyWidths.half] };

/** The downloads' editor at the 400 px floor, its own choice *until I remove it* set. */
export const SpaceEditingNarrow: Story = { render: () => space({ space: SPACE_SET, onKeep: keep, editing: 'downloads' }), decorators: narrow };

/** The set-aside folders' editor in 中文, wide. */
export const SpaceEditingChinese: Story = { render: () => space({ onKeep: keep, editing: 'set-aside' }), decorators: [chinese] };

/** The same in 中文 at the half width. */
export const SpaceEditingChineseHalf: Story = {
  render: () => space({ onKeep: keep, editing: 'set-aside' }), decorators: [storyWidths.half, chinese],
};

/** The logs' editor in 中文 at the floor. */
export const SpaceEditingChineseNarrow: Story = {
  render: () => space({ space: SPACE_SET, onKeep: keep, editing: 'logs' }), decorators: [...narrow, chinese],
};

/** The first load: skeleton rows in the card's place. */
export const SpaceLoading: Story = { render: () => space({ space: undefined }) };

/** A shell older than the page: the card says it answers no disk space. */
export const SpaceOlder: Story = { render: () => space({ space: undefined, older: true }) };

/** A shell older than the page, in 中文. */
export const SpaceOlderChinese: Story = { render: () => space({ space: undefined, older: true }), decorators: [chinese] };

// The whole domain, as `WiringDomain` composes it, in the domain's own container (R1, R6): at the three widths the frame
// gives, in English and 中文, light and dark. `measured/settings.spec.ts` reads each row's value box against the track.

const DOORS = { driver: none, tools: none, ai: none, logs: none, file: none };
const thisDaoris = () => (
  <CardStack>
    <WiringCards
      wiring={WIRED}
      update={UPDATE}
      onMeasure={none}
      onOpenLogFolder={none}
      doors={DOORS}
      space={<WiringSpace space={SPACE} onCount={none} onDownloads={none} />}
    />
  </CardStack>
);

/** The whole domain at the wide main area: one label track a card, values beside their words, cards 16 px apart. */
export const ThisDaoris: Story = { render: thisDaoris };

/** The same at the half width, 630 px: the track still beside. */
export const ThisDaorisHalf: Story = { render: thisDaoris, decorators: [storyWidths.half] };

/** The same at the 400 px floor: the track goes, each value under its words. */
export const ThisDaorisNarrow: Story = { render: thisDaoris, decorators: [storyWidths.narrow] };

/** The whole domain in 中文, wide: the shorter labels set the track no narrower than its floor. */
export const ThisDaorisChinese: Story = { render: thisDaoris, decorators: [chinese] };

/** The same in 中文 at the half width. */
export const ThisDaorisChineseHalf: Story = { render: thisDaoris, decorators: [storyWidths.half, chinese] };

/** The same in 中文 at the floor. */
export const ThisDaorisChineseNarrow: Story = { render: thisDaoris, decorators: [storyWidths.narrow, chinese] };

/** The whole domain in dark, wide. */
export const ThisDaorisDark: Story = { render: thisDaoris, decorators: inTheme('dark') };

/** The whole domain in dark at the floor. */
export const ThisDaorisDarkNarrow: Story = { render: thisDaoris, decorators: [storyWidths.narrow, ...inTheme('dark')] };
