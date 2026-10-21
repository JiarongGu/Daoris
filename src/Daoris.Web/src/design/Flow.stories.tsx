import type { Meta, StoryObj } from '@storybook/react-vite';
import { useLayoutEffect, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Flow } from './Flow';
import {
  APPEND_AFTER, APPEND_BEFORE, DENSE, type Fixture, LARGE_ONE, LARGE_SETTLED, LARGE_THREE, LIFECYCLE, MANY_CURRENT, NONE_CURRENT, type Said,
  UNEQUAL,
} from './flowFixtures';
import { atWidths, FLOW_WIDTHS, inDark, inLight } from './flowFrames';
import { chinese } from './storyLanguage';

// The flowchart's stories (FLOWCHART1a1; the flowchart design §4.4): the hardest graphs, each in both themes, in English
// and 中文, at the frame's widths, 300 px (the side bar's floor), 400 (the main area's), 544 (a 680 px window's main area)
// and 1200, each width a frame of its own so the flow measures its own pane. The measured-box check (`measured/`) draws
// these same stories in a rendered browser and measures every box. The quest's own are `quests/QuestFlow.stories.tsx`.

/** A fixture worded in the reader's language, as a caller words its flow. */
function FixtureFlow({ fixture }: { fixture: Fixture }) {
  const { i18n } = useTranslation();
  const say = (words: Said) => (i18n.language.startsWith('zh') ? words.zh : words.en);
  return (
    <Flow
      label={say(fixture.label)}
      entries={fixture.entries}
      nodes={fixture.nodes.map((node) => ({
        id: node.id, title: say(node.title), current: node.current, reached: node.reached, settled: node.settled,
        state: node.state && { word: say(node.state.word), tone: node.state.tone },
      }))}
      edges={fixture.edges.map((edge) => ({
        id: edge.id, from: edge.from, to: edge.to, says: say(edge.says), label: edge.label && say(edge.label), style: edge.style,
      }))}
    />
  );
}

/** The person's choice of the list, held for the story's life and given back after: nothing else is remembered. */
function ListChosen({ children }: { children: ReactNode }) {
  const was = localStorage.getItem('daoris.flow.list');
  if (was === null) localStorage.setItem('daoris.flow.list', 'list');
  useLayoutEffect(() => () => { if (was === null) localStorage.removeItem('daoris.flow.list'); }, [was]);
  return <>{children}</>;
}

const meta: Meta<typeof FixtureFlow> = {
  title: 'Design/Flow',
  component: FixtureFlow,
};
export default meta;

type Story = StoryObj<typeof FixtureFlow>;

/**
 * Four looks of one fixture: light and dark, in English and in 中文. The first decorator is the innermost: the language
 * wraps the flow, the frames wrap that, and the theme's page wraps the frames, so its padding takes nothing from a frame.
 */
const looks = (fixture: Fixture, widths: readonly number[] = FLOW_WIDTHS) => {
  const frames = atWidths(widths);
  return {
    light: { args: { fixture }, decorators: [frames, inLight] } satisfies Story,
    dark: { args: { fixture }, decorators: [frames, inDark] } satisfies Story,
    chinese: { args: { fixture }, decorators: [chinese, frames, inLight] } satisfies Story,
    chineseDark: { args: { fixture }, decorators: [chinese, frames, inDark] } satisfies Story,
  };
};

const lifecycle = looks(LIFECYCLE);
/**
 * A node's lifecycle, the hardest graph drawn (§2.6): a multigraph with reciprocal moves, several returns to ready, each on
 * a lane of its own, and parallel moves by different causes as one path marked ×n. Top to bottom at 1200 px; the rail
 * narrower, where its lanes do not fit.
 */
export const Lifecycle: Story = lifecycle.light;
export const LifecycleDark: Story = lifecycle.dark;
export const LifecycleChinese: Story = lifecycle.chinese;
export const LifecycleChineseDark: Story = lifecycle.chineseDark;

/**
 * A dense graph at the bounds (§E7): 24 authored steps, `allOf` and `anyOf` groups with their joins, failure sinks and
 * tasks, 96 wires. Past 24 nodes and nothing started, it is focused after its entries and the rest folded into *later steps*
 * (FLOWCHART1d), each edge into the fold drawn to it and counted. *Show whole* draws it whole, which needs 1432 px top to bottom:
 * the measured-box check presses it in its 1440 px frame.
 */
const dense = looks(DENSE, [...FLOW_WIDTHS, 1440]);
export const Dense: Story = dense.light;
export const DenseDark: Story = dense.dark;
export const DenseChinese: Story = dense.chinese;
export const DenseChineseDark: Story = dense.chineseDark;

/**
 * Unequal boxes in both languages: a fork of three and its join, a title long in 中文 beside short English ones and the
 * other way about, so the rank's boxes differ in height and the edges are drawn from the boxes as they fell. Left to
 * right at 1200 px, and top to bottom at 570, between what top to bottom needs (568) and left to right (576).
 */
const unequal = looks(UNEQUAL, [...FLOW_WIDTHS, 570]);
export const Unequal: Story = unequal.light;
export const UnequalDark: Story = unequal.dark;
export const UnequalChinese: Story = unequal.chinese;
export const UnequalChineseDark: Story = unequal.chineseDark;

/** An instance before a node is appended: two parts begun apart, each followed by one. */
const before = looks(APPEND_BEFORE);
export const BeforeAnAppend: Story = before.light;
export const BeforeAnAppendDark: Story = before.dark;
export const BeforeAnAppendChinese: Story = before.chinese;
export const BeforeAnAppendChineseDark: Story = before.chineseDark;

/** The same after a part is appended: the sweeps order the second rank X, Z, Y, so Y moves (§3.1 step 5). */
const after = looks(APPEND_AFTER);
export const AfterAnAppend: Story = after.light;
export const AfterAnAppendDark: Story = after.dark;
export const AfterAnAppendChinese: Story = after.chinese;
export const AfterAnAppendChineseDark: Story = after.chineseDark;

/** A run with three nodes current at once, each lit by its own state, open's hue only on what waits on the person. */
const many = looks(MANY_CURRENT);
export const SeveralCurrent: Story = many.light;
export const SeveralCurrentDark: Story = many.dark;
export const SeveralCurrentChinese: Story = many.chinese;
export const SeveralCurrentChineseDark: Story = many.chineseDark;

/** The same run not started: nothing current, nothing reached; its layout is the one above, since only what is lit differs. */
const none = looks(NONE_CURRENT);
export const NoneCurrent: Story = none.light;
export const NoneCurrentDark: Story = none.dark;
export const NoneCurrentChinese: Story = none.chinese;
export const NoneCurrentChineseDark: Story = none.chineseDark;

/**
 * A run too large to show whole (FLOWCHART1d; §3.6, trap 16): 96 nodes, three lines of work of 31 steps, folded around its one
 * current node, the build at its 20th step: what settled two ranks before it as *steps done*, the ranks from two before to three
 * after it drawn, and the rest as *later steps*, every edge across a fold's boundary drawn to it with its cause. *Show whole* opens
 * every fold.
 */
const largeOne = looks(LARGE_ONE);
export const TooLarge: Story = largeOne.light;
export const TooLargeDark: Story = largeOne.dark;
export const TooLargeChinese: Story = largeOne.chinese;
export const TooLargeChineseDark: Story = largeOne.chineseDark;

/** The same run folded around three current nodes far apart, their union, with a fold between where they part. */
const largeThree = looks(LARGE_THREE);
export const TooLargeThreeCurrent: Story = largeThree.light;
export const TooLargeThreeCurrentDark: Story = largeThree.dark;
export const TooLargeThreeCurrentChinese: Story = largeThree.chinese;
export const TooLargeThreeCurrentChineseDark: Story = largeThree.chineseDark;

/** The same run ended, none current: focused before the end, what settled before it folded as *steps done*. */
const largeSettled = looks(LARGE_SETTLED);
export const TooLargeNoneCurrent: Story = largeSettled.light;
export const TooLargeNoneCurrentDark: Story = largeSettled.dark;
export const TooLargeNoneCurrentChinese: Story = largeSettled.chinese;
export const TooLargeNoneCurrentChineseDark: Story = largeSettled.chineseDark;

/** *Show as list* chosen, at 1200 px where the drawing fits: the rail, with *Show as flowchart* to go back. */
export const ShownAsListAt1200: Story = {
  args: { fixture: MANY_CURRENT },
  decorators: [(Story) => <ListChosen><Story /></ListChosen>, atWidths([1200]), inLight],
};
