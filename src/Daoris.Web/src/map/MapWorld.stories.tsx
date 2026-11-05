import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { type ReactNode, useLayoutEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { keptAtZoom } from '../design/canvas/storyZoom';
import { Button } from '../design/ui';
import { memoryKey } from '../design/canvas/viewMemory';
import { inDark } from '../design/flowFrames';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import type { MapSelection } from './MapLines';
import { MapWorld } from './MapWorld';
import type { MapNode, QuestEdge, Topology } from './topology';

// The reference map on the canvas (CANVAS1e; D224, the canvas design §3.3): a ring of five, the layers of twenty-nine, several workspaces
// as blocks, a chosen line, the map as a list, and a kept place whose selection is gone, at the main area's 80, 39 and 25rem, in English and 中文 and both themes. At 25rem, below the 36rem
// floor, the map is drawn where it fits and pans in its pane. Keyboard: Tab reaches the toolbar, the region, then the world's one stop;
// → walks from a card to its first line out and on to the card beyond, ← back by the line it came by, ↑ and ↓ cycle the lines at a card.
// Frames and boxes are measured on these (`measured/map.spec.ts`).

const node = (id: string, extra: Partial<MapNode> = {}): MapNode =>
  ({ id, owns: [], accepts: [], open: 0, working: false, parked: false, sessions: 0, ...extra });
const quest = (from: string, to: string, open = 1, live: QuestEdge['live'] = null): QuestEdge => ({ from, to, quests: [], open, live });
const names = (count: number, prefix = 'repo') => Array.from({ length: count }, (_, at) => `${prefix}-${String(at).padStart(2, '0')}`);

const RING: Topology = {
  nodes: [node('engine', { open: 3, working: true, sessions: 2 }), node('game', { open: 1 }), node('portal-ui', { open: 2, parked: true, sessions: 1 }), node('portals-db'), node('tools')],
  quests: [quest('game', 'engine'), quest('engine', 'game', 0), quest('portal-ui', 'portals-db', 1, 'parked')],
  asks: [{ to: 'game', asks: ['a'], quests: [], open: 1, live: null }],
  chains: [{ from: 'game', to: 'portals-db', steps: [], waiting: [], live: null }],
  depends: [{ from: 'portal-ui', to: 'engine' }],
  knowledge: [{ a: 'engine', b: 'tools', groups: 2 }],
  outside: 0,
  circles: 1,
};

function layers(count: number): Topology {
  const ids = names(count);
  return {
    nodes: ids.map((id, at) => node(id, { open: at % 4, working: at === 5, parked: at === 9 })),
    quests: [...ids.slice(1, 20).map((id, at) => quest(ids[at]!, id, at % 3)), quest(ids[0]!, ids[5]!), quest(ids[3]!, ids[9]!), quest(ids[9]!, ids[3]!)],
    asks: [{ to: ids[1]!, asks: ['a'], quests: [], open: 1, live: null }],
    chains: [{ from: ids[2]!, to: ids[6]!, steps: [], waiting: [], live: null }],
    depends: [{ from: ids[4]!, to: ids[8]! }],
    knowledge: [{ a: ids[10]!, b: ids[12]!, groups: 2 }],
    outside: 0,
    circles: 1,
  };
}

function blocks(): Topology {
  const alpha = names(9, 'a');
  const beta = names(8, 'b');
  return {
    nodes: [...alpha.map((id) => node(id, { workspace: 'alpha', open: 1 })), ...beta.map((id) => node(id, { workspace: 'beta' }))],
    quests: [...alpha.slice(1).map((id, at) => quest(alpha[at]!, id)), ...beta.slice(1, 5).map((id, at) => quest(beta[at]!, id)), quest('a-02', 'b-01'), quest('b-01', 'a-02')],
    asks: [{ to: 'a-03', asks: ['a'], quests: [], open: 1, live: null }],
    chains: [],
    depends: [{ from: 'a-00', to: 'a-04' }],
    knowledge: [{ a: 'a-06', b: 'b-06', groups: 1 }],
    outside: 0,
    circles: 2,
  };
}

function MapStory({ topology, line = null, storeKey = 'story', inPlace = false }: { topology: Topology; line?: MapSelection | null; storeKey?: string; inPlace?: boolean }) {
  const { t } = useTranslation();
  const [selected, setSelected] = useState<MapSelection | null>(line);
  // The choice's detail as the page gives it below the 36rem floor: under the chosen row, its doors after it in the tab order.
  const detail = inPlace ? <Button variant="ghost">{t('map.detail.openRepository')}</Button> : undefined;
  return <MapWorld topology={topology} selected={selected} onSelect={setSelected} storeKey={storeKey} detail={detail} />;
}

/**
 * A place kept by this viewer under the map as it was, with a repository selected that has since left it: written before the map
 * opens, once per mount, and taken away after, so the canvas restores it and says the selection is gone.
 */
function KeptWithAGoneSelection({ children }: { children: ReactNode }) {
  const at = memoryKey('map', 'gone');
  useState(() => localStorage.setItem(at, JSON.stringify({
    identity: 'the map before', view: { x: 0, y: 0, k: 1 }, anchor: { id: 'repo-00', group: null, screen: { x: 24, y: 24 } }, selected: 'repo-99',
  })));
  useLayoutEffect(() => () => localStorage.removeItem(at), [at]);
  return <>{children}</>;
}
const keptWithAGoneSelection: Decorator = (Story) => <KeptWithAGoneSelection><Story /></KeptWithAGoneSelection>;

const meta: Meta<typeof MapStory> = {
  title: 'Map/MapWorld',
  component: MapStory,
  args: { topology: layers(29) },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof MapStory>;

/** Twenty-nine repositories in layers: the connected part by who asks whom, what nothing connects in a grid below. */
export const Layered: Story = {};
export const LayeredHalf: Story = { decorators: [storyWidths.half] };
export const LayeredNarrow: Story = { decorators: [storyWidths.narrow] };
export const LayeredChinese: Story = { decorators: [storyWidths.wide, chinese] };
export const LayeredChineseHalf: Story = { decorators: [storyWidths.half, chinese] };
export const LayeredDark: Story = { decorators: [storyWidths.wide, inDark] };
export const LayeredChineseDarkNarrow: Story = { decorators: [storyWidths.narrow, chinese, inDark] };

/** Five repositories on the ring, fitted, with no zoom. */
export const Ring: Story = { args: { topology: RING } };
export const RingHalf: Story = { args: { topology: RING }, decorators: [storyWidths.half] };
export const RingNarrow: Story = { args: { topology: RING }, decorators: [storyWidths.narrow] };
export const RingChineseDark: Story = { args: { topology: RING }, decorators: [storyWidths.wide, chinese, inDark] };

/** Two workspaces, each a block with its own layers under its name, the lines between them down the gap. */
export const Workspaces: Story = { args: { topology: blocks() } };
export const WorkspacesHalf: Story = { args: { topology: blocks() }, decorators: [storyWidths.half] };
export const WorkspacesChineseDark: Story = { args: { topology: blocks() }, decorators: [storyWidths.wide, chinese, inDark] };

/** A line chosen: it stands forward alone and the rest step back. */
export const ALineChosen: Story = { args: { line: { kind: 'quests', from: 'repo-03', to: 'repo-09' } } };
export const ALineChosenRing: Story = { args: { topology: RING, line: { kind: 'quests', from: 'game', to: 'engine' } }, decorators: [storyWidths.half] };

/**
 * Each zoom says less about the same cards (MAPUX1a): at 100% a name, its count and the session in words; at 50% the name and count; at
 * 25% a dot, the ring kept, and the chosen card's name beside it. The anchor keeps the view on the first repository.
 */
const atZoom = (k: number) => keptAtZoom('map', 'story', k, 'repo-00');
export const Zoom100: Story = { decorators: [atZoom(1), storyWidths.wide] };
export const Zoom50: Story = { decorators: [atZoom(0.5), storyWidths.wide] };
export const Zoom25: Story = { decorators: [atZoom(0.25), storyWidths.wide] };
export const Zoom100Chinese: Story = { decorators: [atZoom(1), storyWidths.wide, chinese] };
export const Zoom50Chinese: Story = { decorators: [atZoom(0.5), storyWidths.wide, chinese] };
export const Zoom25Chinese: Story = { decorators: [atZoom(0.25), storyWidths.wide, chinese] };
/** The chosen and its neighbours stand forward, the rest at 40%; at 25% the chosen card keeps its name beside it. */
export const ChoiceForward: Story = { args: { line: { kind: 'node', id: 'repo-03' } }, decorators: [atZoom(1), storyWidths.wide] };
export const ChoiceForwardChinese: Story = { args: { line: { kind: 'node', id: 'repo-03' } }, decorators: [atZoom(0.5), storyWidths.wide, chinese] };
export const ChoiceLabelledAtTheDotTier: Story = { args: { line: { kind: 'node', id: 'repo-03' } }, decorators: [atZoom(0.25), storyWidths.half] };
export const ChoiceForwardRing: Story = { args: { topology: RING, line: { kind: 'node', id: 'engine' } }, decorators: [storyWidths.half] };

/** *Show as list* pressed, as a keyboard reaches it in the toolbar. */
const asList: Story['play'] = async ({ canvasElement }) => {
  canvasElement.querySelector<HTMLButtonElement>('[data-canvas-list-toggle]')?.click();
  await new Promise((settled) => requestAnimationFrame(settled));
};

/** The map as a list, the reading that is not a canvas: the repositories, then each kind of line, a press choosing either. */
export const AsList: Story = { play: asList };
export const AsListNarrow: Story = { play: asList, decorators: [storyWidths.narrow] };
export const AsListChineseDark: Story = { play: asList, decorators: [storyWidths.half, chinese, inDark] };
export const AsListALineChosen: Story = { args: { line: { kind: 'quests', from: 'repo-03', to: 'repo-09' } }, play: asList };

/**
 * Below the 36rem floor the list is the reading (MAPUX1e): one tree, one row in the tab order, each row's count and state in words, and
 * the chosen row's detail opening under it.
 */
export const ListNarrowDetailInPlace: Story = { args: { line: { kind: 'node', id: 'repo-09' }, inPlace: true }, decorators: [storyWidths.narrow] };
export const ListNarrowDetailInPlaceChinese: Story = { args: { line: { kind: 'node', id: 'repo-09' }, inPlace: true }, decorators: [storyWidths.narrow, chinese] };
export const ListNarrowDetailInPlaceChineseDark: Story = { args: { line: { kind: 'node', id: 'repo-09' }, inPlace: true }, decorators: [storyWidths.narrow, chinese, inDark] };
export const ListNarrowALineChosen: Story = { args: { line: { kind: 'quests', from: 'repo-03', to: 'repo-09' }, inPlace: true }, decorators: [storyWidths.narrow] };

/** The viewer's place restored under a changed map: the view kept by its anchor, and the repository once selected said to be gone. */
export const ASelectionGone: Story = { args: { storeKey: 'gone' }, decorators: [keptWithAGoneSelection, storyWidths.wide] };
export const ASelectionGoneChineseDark: Story = { args: { storeKey: 'gone' }, decorators: [keptWithAGoneSelection, storyWidths.wide, chinese, inDark] };
