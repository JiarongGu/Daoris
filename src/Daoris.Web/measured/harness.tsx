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

// The measured-box checks' page (FLOWCHART1a1; the flowchart design §7, trap 3; UIFIT1, the UI/UX design §5's first trap):
// one story, composed as Storybook composes it, with the project's decorators, the shipped tokens and catalogues, and drawn
// alone in a real browser so `measure.ts` can measure every box of a flow and `settingRows.ts` every row's value against
// its track. `?story=flow/Lifecycle` names the stories file, by its book below, and the story.

setProjectAnnotations(preview);

const books: Record<string, Record<string, ComponentType>> = {
  flow: composeStories(flow) as unknown as Record<string, ComponentType>,
  quest: composeStories(quest) as unknown as Record<string, ComponentType>,
  // Settings' rows on their tracks (UIFIT1): the atoms' story, This Daoris, Driver and AI features whole, and a workspace's Setup.
  ui: composeStories(ui) as unknown as Record<string, ComponentType>,
  wiring: composeStories(wiring) as unknown as Record<string, ComponentType>,
  driver: composeStories(driver) as unknown as Record<string, ComponentType>,
  ai: composeStories(ai) as unknown as Record<string, ComponentType>,
  workspace: composeStories(workspace) as unknown as Record<string, ComponentType>,
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
