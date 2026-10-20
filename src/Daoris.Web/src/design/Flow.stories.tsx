import type { Meta, StoryObj } from '@storybook/react-vite';
import { useLayoutEffect, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Flow } from './Flow';
import {
  APPEND_AFTER, APPEND_BEFORE, DENSE, type Fixture, LIFECYCLE, MANY_CURRENT, NONE_CURRENT, type Said, UNEQUAL,
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
        id: node.id, title: say(node.title), current: node.current, reached: node.reached,
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
 * tasks, 96 wires. It needs 1432 px top to bottom, so it is the rail at every frame width until folds come (FLOWCHART1d),
 * and drawn whole at 1440 px, the width it needs, for the measured-box check.
 */
const dense = looks(DENSE, [...FLOW_WIDTHS, 1440]);
export const Dense: Story = dense.light;
export const DenseDark: Story = dense.dark;
export const DenseChinese: Story = dense.chinese;
export const DenseChineseDark: Story = dense.chineseDark;

/**
 * Unequal boxes in both languages: a fork of three and its join, a title long in 中文 beside short English ones and the
 * other way about, so the rank's boxes differ in height and the edges are drawn from the boxes as they fell. Left to
 * right at 1200 px, top to bottom at 600.
 */
const unequal = looks(UNEQUAL, [...FLOW_WIDTHS, 600]);
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

/** *Show as list* chosen, at 1200 px where the drawing fits: the rail, with *Show as flowchart* to go back. */
export const ShownAsListAt1200: Story = {
  args: { fixture: MANY_CURRENT },
  decorators: [(Story) => <ListChosen><Story /></ListChosen>, atWidths([1200]), inLight],
};
