import { useState } from 'react';
import type { Meta, StoryObj } from '@storybook/react-vite';
import type { Provenance } from '../data/api';
import { inDark } from '../design/flowFrames';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import { type CodeFixture, CYCLE_ACROSS_FOLDERS, LONG_IDS, REPOSITORY, SKIPPING_LAYERS, syntheticMap, WITH_A_CYCLE } from './codeFixtures';
import { CodeMapDetail, CodeMapWorld } from './CodeMap';

// A repository's own code map on the canvas (MAP3a; CANVAS1f; D224, the canvas design §3.4), in the shapes a producer writes: this
// repository's twenty modules, a map with a cycle, ids too long for their cards, a teammate's, and synthetic ones of 500 and 5,000
// that open on their closed folders. At the main area's 80, 39 and 25rem (below the 36rem floor the world is drawn where it fits and
// pans in its pane), in English and 中文, light and dark. Frames and cards are measured on these (`measured/code.spec.ts`).

const FED: Provenance = {
  commit: 'c0ffee1234567890', shortCommit: 'c0ffee12', committedAt: '2026-09-24T08:00:00Z', branch: 'main', origin: 'person@machine-b',
};

function Both({ map, fed, initial = null }: { map: CodeFixture; fed?: Provenance; initial?: string | null }) {
  const [selected, setSelected] = useState<string | null>(initial);
  return (
    // As MapView lays them out: beside each other by the main area's own width (D118 §3b), so the story
    // stands in for the main area as their container.
    <div className="@container/main">
      <div className="grid items-start gap-4 @4xl/main:grid-cols-[minmax(0,1fr)_20rem]">
        <CodeMapWorld repository="daoris" modules={map.modules} dependencies={map.dependencies} selected={selected} onSelect={setSelected} version={fed?.commit} />
        <CodeMapDetail modules={map.modules} dependencies={map.dependencies} selected={selected} file="docs/code-map.json" fed={fed} onSelect={setSelected} />
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
 * A teammate's map (MAP3e): no checkout here, so it came down with the sync, and the detail says
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
