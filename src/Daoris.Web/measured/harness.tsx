import type { ComponentType } from 'react';
import { createRoot } from 'react-dom/client';
import { composeStories, setProjectAnnotations } from '@storybook/react-vite';
import preview from '../.storybook/preview';
import * as flow from '../src/design/Flow.stories';
import * as ui from '../src/design/ui.stories';
import * as quest from '../src/quests/QuestFlow.stories';
import * as workspace from '../src/projects/workspace/WorkspacePage.stories';
import * as ai from '../src/settings/AiJobs.stories';
import * as driver from '../src/settings/Driver.stories';
import * as wiring from '../src/settings/wiring/Wiring.stories';
import * as parts from '../src/work/head/PartsFlow.stories';
import * as session from '../src/work/head/SessionFlow.stories';
import * as landing from '../src/projects/git/LandingFlow.stories';
import * as head from '../src/work/AttendedSession.head.stories';
import * as workflow from '../src/workflow/WorkflowFlow.stories';

// The measured-box checks' page (FLOWCHART1a1; the flowchart design §7, trap 3; UIFIT1, the UI/UX design §5's first trap):
// one story, composed as Storybook composes it, with the project's decorators, the shipped tokens and catalogues, and drawn
// alone in a real browser so `measure.ts` can measure every box of a flow, `settingRows.ts` every row's value against
// its track, and `recordHead.ts` how far down the record's head puts the conversation (MILESTONEUX1).
// `?story=flow/Lifecycle` names the stories file, by its book below, and the story.

setProjectAnnotations(preview);

const books: Record<string, Record<string, ComponentType>> = {
  flow: composeStories(flow) as unknown as Record<string, ComponentType>,
  quest: composeStories(quest) as unknown as Record<string, ComponentType>,
  // A session's states (FLOWCHART1b): the return on its lane, the outcomes as one node.
  session: composeStories(session) as unknown as Record<string, ComponentType>,
  // A session's parts (FLOWCHART1b): a fork and a retry, an asked part from a faint node, the landing plan as a dashed node.
  parts: composeStories(parts) as unknown as Record<string, ComponentType>,
  // How a landed branch landed (FLOWCHART1c): tries and advances stacked, the push and the pull request hung apart from the landing.
  landing: composeStories(landing) as unknown as Record<string, ComponentType>,
  // A workflow and its run (FLOWCHART1d): its steps a rank each under their sections, a failure path, the run lit.
  workflow: composeStories(workflow) as unknown as Record<string, ComponentType>,
  // Settings' rows on their tracks (UIFIT1): the atoms' story, This Daoris, Driver and AI features whole, and a workspace's Setup.
  ui: composeStories(ui) as unknown as Record<string, ComponentType>,
  wiring: composeStories(wiring) as unknown as Record<string, ComponentType>,
  driver: composeStories(driver) as unknown as Record<string, ComponentType>,
  ai: composeStories(ai) as unknown as Record<string, ComponentType>,
  workspace: composeStories(workspace) as unknown as Record<string, ComponentType>,
  // The record head with every block, at the main area's three widths (MILESTONEUX1): how far down the conversation starts.
  head: composeStories(head) as unknown as Record<string, ComponentType>,
};

const [book = '', name = ''] = (new URLSearchParams(window.location.search).get('story') ?? '').split('/');
const Story = books[book]?.[name];
const root = document.getElementById('root')!;
if (Story) createRoot(root).render(<Story />);
else root.textContent = `No story ${book}/${name}. Stories: ${Object.entries(books).flatMap(([at, stories]) => Object.keys(stories).map((each) => `${at}/${each}`)).join(', ')}`;

// Ready once the faces have loaded and two frames have passed, so every flow has measured its pane and drawn its edges,
// and every track has sized to its labels in the loaded face.
void document.fonts.ready.then(() => requestAnimationFrame(() => requestAnimationFrame(() => {
  (window as unknown as { flowReady: boolean }).flowReady = true;
})));
