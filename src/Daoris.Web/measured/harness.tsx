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
import * as graph from '../src/workflow/GraphFlow.stories';
import * as runView from '../src/workflow/WorkflowRunFlow.stories';
import * as zoom from '../src/work/frame/ZoomReadout.stories';
import * as waitPanel from '../src/work/waits/WaitPanel.stories';
import * as roles from '../src/agents/roles/RoleDiagram.stories';
import * as place from '../src/agents/setup/PlacePage.stories';
import * as setUpReview from '../src/agents/setup/PlaceReview.stories';
import * as placeWorkflow from '../src/workflow/place/PlaceWorkflowView.stories';
import * as editor from '../src/workflow/edit/WorkflowEditor.stories';
import * as canvas from '../src/design/canvas/Canvas.stories';
import { applyZoom } from '../src/design/zoom';

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
  // One drawing of a workflow graph (SETUPUX1n): the release graph down at 80 and 39rem, the rail at 25rem.
  graph: composeStories(graph) as unknown as Record<string, ComponentType>,
  // A run's view (SETUPUX1u): a release run of each outcome and a review waiting on you, at 300 px, 25rem and 45% of the window.
  runview: composeStories(runView) as unknown as Record<string, ComponentType>,
  // Settings' rows on their tracks (UIFIT1): the atoms' story, This Daoris, Driver and AI features whole, and a workspace's Setup.
  ui: composeStories(ui) as unknown as Record<string, ComponentType>,
  wiring: composeStories(wiring) as unknown as Record<string, ComponentType>,
  driver: composeStories(driver) as unknown as Record<string, ComponentType>,
  ai: composeStories(ai) as unknown as Record<string, ComponentType>,
  workspace: composeStories(workspace) as unknown as Record<string, ComponentType>,
  // The record head with every block, at the main area's three widths (MILESTONEUX1): how far down the conversation starts.
  head: composeStories(head) as unknown as Record<string, ComponentType>,
  // The window at each zoom level (ZOOM1): the frame's bars at their own size, the content scaled.
  zoom: composeStories(zoom) as unknown as Record<string, ComponentType>,
  // The waiting panel (WAITPANEL1): its edge dragged and kept, and the conversation's presses above it, never under it.
  waitPanel: composeStories(waitPanel) as unknown as Record<string, ComponentType>,
  // The roles' diagram (COMPANYVIEW1f): across at 80rem with its detail beside, its list at 39 and 25rem.
  roles: composeStories(roles) as unknown as Record<string, ComponentType>,
  // A place's page (SETUPUX1h): the head and the three section lines within the first 400 px at the three widths.
  place: composeStories(place) as unknown as Record<string, ComponentType>,
  // *Set up…*'s review (SETUPUX1k): two columns at 80rem, one below, and where Apply sits at 680 px (39rem).
  review: composeStories(setUpReview) as unknown as Record<string, ComponentType>,
  // A place's Workflow section on its own (SETUPUX1o): the chart in its column at 80rem, one press away at 39 and 25rem.
  placeWorkflow: composeStories(placeWorkflow) as unknown as Record<string, ComponentType>,
  // The workflow editor (FLOWCHART2b): its chart drawn at 80 and 39rem with a ＋ on each way, and the rail at 25rem.
  editor: composeStories(editor) as unknown as Record<string, ComponentType>,
  // The canvas (CANVAS1a): a flow in the viewport at the three widths, the dense graph whole, and the synthetic worlds measured for frames.
  canvas: composeStories(canvas) as unknown as Record<string, ComponentType>,
};

const query = new URLSearchParams(window.location.search);
// `?zoom=200` draws the story at a window zoom, the root's size as the window applies it (trap 12).
const level = Number(query.get('zoom'));
if (level > 0) applyZoom(level);
const [book = '', name = ''] = (query.get('story') ?? '').split('/');
const Story = books[book]?.[name];
const root = document.getElementById('root')!;
// The moment the story's props are handed to React, from which a drawing's first usable frame is timed (the canvas design §4.1).
performance.mark('story-render');
if (Story) createRoot(root).render(<Story />);
else root.textContent = `No story ${book}/${name}. Stories: ${Object.entries(books).flatMap(([at, stories]) => Object.keys(stories).map((each) => `${at}/${each}`)).join(', ')}`;

// Ready once the faces have loaded and two frames have passed, so every flow has measured its pane and drawn its edges,
// and every track has sized to its labels in the loaded face.
void document.fonts.ready.then(() => requestAnimationFrame(() => requestAnimationFrame(() => {
  (window as unknown as { flowReady: boolean }).flowReady = true;
})));
