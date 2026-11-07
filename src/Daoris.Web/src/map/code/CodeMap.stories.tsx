import { useState } from 'react';
import type { Meta, StoryObj } from '@storybook/react-vite';
import type { Provenance } from '../../data/api';
import { keptAtZoom } from '../../design/canvas/storyZoom';
import { inDark } from '../../design/flowFrames';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { type CodeFixture, CYCLE_ACROSS_FOLDERS, LONG_IDS, REPOSITORY, SKIPPING_LAYERS, syntheticMap, WITH_A_CYCLE } from './codeFixtures';
import { CodeMapDetail, CodeMapWorld } from './CodeMap';

// A repository's own code map on the canvas, in the shapes a producer writes: this
// repository's twenty modules, a map with a cycle, a cycle across folders, lines routed around the cards they would cross, one folder
// folded alone, ids too long for their cards, a teammate's, and synthetic ones of 500 and 5,000 that open on their closed folders. At the main area's 80, 39 and 25rem (below the 36rem floor the world is drawn where it fits and
// pans in its pane), in English and 中文, light and dark. Frames and cards are measured on these (`measured/code.spec.ts`).

const FED: Provenance = {
  commit: 'c0ffee1234567890', shortCommit: 'c0ffee12', committedAt: '2026-09-24T08:00:00Z', branch: 'main', origin: 'person@machine-b',
};

function Both({ map, fed, initial = null, inPlace = false }: { map: CodeFixture; fed?: Provenance; initial?: string | null; inPlace?: boolean }) {
  const [selected, setSelected] = useState<string | null>(initial);
  const detail = <CodeMapDetail modules={map.modules} dependencies={map.dependencies} selected={selected} file="docs/code-map.json" fed={fed} onSelect={setSelected} />;
  // Below the 36rem floor the detail opens in place under its row in the list, as MapView gives it.
  const here = inPlace && selected !== null;
  return (
    // As MapView lays them out: beside each other by the main area's own width, so the story
    // stands in for the main area as their container.
    <div className="@container/main">
      <div className="grid items-start gap-4 @4xl/main:grid-cols-[minmax(0,1fr)_20rem]">
        <CodeMapWorld
          repository="daoris" modules={map.modules} dependencies={map.dependencies} selected={selected} onSelect={setSelected} version={fed?.commit}
          detail={here ? detail : undefined}
        />
        {!here && detail}
      </div>
    </div>
  );
}

const meta: Meta<typeof Both> = {
  title: 'Map/CodeMap',
  component: Both,
  args: { map: REPOSITORY },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof Both>;

/** Narrow, the list is the reading, a folder to a group, a cycle a group under its folder, and the chosen module's detail under its row. */
export const ListNarrowDetailInPlace: Story = { args: { initial: 'Daoris.Desktop.Driver', inPlace: true }, decorators: [storyWidths.narrow] };
export const ListNarrowDetailInPlaceChinese: Story = { args: { initial: 'Daoris.Desktop.Driver', inPlace: true }, decorators: [storyWidths.narrow, chinese] };
export const ListNarrowACycle: Story = { args: { map: WITH_A_CYCLE, initial: 'planner', inPlace: true }, decorators: [storyWidths.narrow] };
export const ListNarrowACycleChinese: Story = { args: { map: WITH_A_CYCLE, initial: 'planner', inPlace: true }, decorators: [storyWidths.narrow, chinese] };

/** This repository's twenty modules, in their folders, opened whole, a module chosen and its neighbourhood forward. */
export const Layered: Story = { args: { initial: 'Daoris.Desktop.Driver' } };
export const LayeredHalf: Story = { ...Layered, decorators: [storyWidths.half] };
export const LayeredNarrow: Story = { ...Layered, decorators: [storyWidths.narrow] };
export const LayeredChinese: Story = { ...Layered, decorators: [storyWidths.wide, chinese] };
export const LayeredDark: Story = { ...Layered, decorators: [storyWidths.wide, inDark] };

/** A producer recorded a cycle: its modules are one labelled unit in the layers, whole in a frame of their own. */
export const WithACycle: Story = { args: { map: WITH_A_CYCLE } };
export const WithACycleChinese: Story = { ...WithACycle, decorators: [storyWidths.wide, chinese] };

/**
 * A teammate's map: no checkout here, so it came down with the sync, and the detail says
 * from which commit and whose key — before a module is chosen, where the file line sits.
 */
export const FromATeammate: Story = { args: { fed: FED } };
export const FromATeammateNarrow: Story = { ...FromATeammate, decorators: [storyWidths.narrow] };

/**
 * A cycle across three folders, condensed in the folder above them, labelled with the folders it crosses, and whole when opened
 * (opened here, as a map of seven opens whole; the second story chooses a module outside it).
 */
export const ACycleAcrossFolders: Story = { args: { map: CYCLE_ACROSS_FOLDERS } };
export const ACycleAcrossFoldersChosen: Story = { args: { map: CYCLE_ACROSS_FOLDERS, initial: 'board' }, decorators: [storyWidths.half] };
export const ACycleAcrossFoldersChineseDark: Story = { args: { map: CYCLE_ACROSS_FOLDERS }, decorators: [storyWidths.wide, chinese, inDark] };

/** *Fold* pressed in the header of the open frame a selector names: any folder's, chosen module or none. */
const foldFrame = (selector: string): Story['play'] => async ({ canvasElement }) => {
  canvasElement.querySelector<HTMLButtonElement>(selector)?.click();
  await new Promise((settled) => requestAnimationFrame(settled));
};
const foldWeb = foldFrame('[data-code-fold="folder:src/web"]');
const foldCycle = foldFrame('[data-code-fold^="cycle:"]');

/** One folder folded alone: its header's *Fold*, and the folder is a card again, the choice released, every other folder still open. */
export const OneFolderFolded: Story = { args: { map: CYCLE_ACROSS_FOLDERS, initial: 'board' }, play: foldWeb };
export const OneFolderFoldedNarrowChinese: Story = { args: { map: CYCLE_ACROSS_FOLDERS, initial: 'board' }, play: foldWeb, decorators: [storyWidths.narrow, chinese] };
/** A folder no module is chosen in, folded from its own header. */
export const AnotherFolderFolded: Story = { args: { map: CYCLE_ACROSS_FOLDERS, initial: 'board' }, play: foldFrame('[data-code-fold="folder:src/data"]') };
export const AnotherFolderFoldedChinese: Story = { ...AnotherFolderFolded, decorators: [storyWidths.wide, chinese] };
/** The cycle across folders folded alone: one card, labelled with the folders it crosses. */
export const ACycleAcrossFoldersFolded: Story = { args: { map: CYCLE_ACROSS_FOLDERS, initial: 'session' }, play: foldCycle };
export const ACycleAcrossFoldersFoldedDark: Story = { args: { map: CYCLE_ACROSS_FOLDERS, initial: 'session' }, play: foldCycle, decorators: [storyWidths.half, inDark] };

/**
 * Each zoom says less about the same cards: at 100% a module's id, a folder's name and ×n; at 50% the id's last segment; at
 * 25% a dot, and the chosen module's name beside it. The folders are the map's own, opened whole.
 */
const atZoom = (k: number) => keptAtZoom('code', 'daoris@local', k, 'Daoris.Desktop.Driver');
export const Zoom100: Story = { args: { initial: 'Daoris.Desktop.Driver' }, decorators: [atZoom(1), storyWidths.wide] };
export const Zoom50: Story = { args: { initial: 'Daoris.Desktop.Driver' }, decorators: [atZoom(0.5), storyWidths.wide] };
export const Zoom25: Story = { args: { initial: 'Daoris.Desktop.Driver' }, decorators: [atZoom(0.25), storyWidths.wide] };
export const Zoom100Chinese: Story = { args: { initial: 'Daoris.Desktop.Driver' }, decorators: [atZoom(1), storyWidths.wide, chinese] };
export const Zoom50Chinese: Story = { args: { initial: 'Daoris.Desktop.Driver' }, decorators: [atZoom(0.5), storyWidths.wide, chinese] };
export const Zoom25Chinese: Story = { args: { initial: 'Daoris.Desktop.Driver' }, decorators: [atZoom(0.25), storyWidths.wide, chinese] };
/** A map of 500: closed folders at 50% say their name and ×n; at 25% the chosen folder's frame keeps its title. */
export const HundredsZoom50: Story = { args: { map: syntheticMap(500) }, decorators: [keptAtZoom('code', 'daoris@local', 0.5, 'module-0000'), storyWidths.wide] };

/** Lines that skip a layer run around the cards between, through the gaps, leaving and entering their cards as every line does. */
export const LinesAroundCards: Story = { args: { map: SKIPPING_LAYERS, initial: 'app' } };
export const LinesAroundCardsHalf: Story = { ...LinesAroundCards, decorators: [storyWidths.half] };
export const LinesAroundCardsChineseDark: Story = { ...LinesAroundCards, decorators: [storyWidths.wide, chinese, inDark] };

/** Ids longer than a card, cut on it and whole in the detail. */
export const LongIds: Story = { args: { map: LONG_IDS, initial: 'Daoris.Desktop.Driver.Host.Integration' } };

/** Five hundred modules: folders closed, the lines between them counted, find reaching every module. */
export const Hundreds: Story = { args: { map: syntheticMap(500) } };
export const HundredsHalf: Story = { ...Hundreds, decorators: [storyWidths.half] };
export const HundredsDark: Story = { ...Hundreds, decorators: [storyWidths.wide, inDark] };

/** Five thousand synthetic modules: the overview of closed folders the world opens on. */
export const Thousands: Story = { args: { map: syntheticMap(5000) } };
