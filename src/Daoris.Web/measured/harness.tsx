import type { ComponentType } from 'react';
import { createRoot } from 'react-dom/client';
import { composeStories, setProjectAnnotations } from '@storybook/react-vite';
import preview from '../.storybook/preview';
import * as flow from '../src/design/Flow.stories';
import * as quest from '../src/quests/QuestFlow.stories';

// The measured-box check's page (FLOWCHART1a1; the flowchart design §7, trap 3): one of the flowchart's stories, composed
// as Storybook composes it, with the project's decorators, the shipped tokens and catalogues, and drawn alone in a real
// browser so `measure.ts` can measure every box. `?story=flow/Lifecycle` names the stories file and the story.

setProjectAnnotations(preview);

const books: Record<string, Record<string, ComponentType>> = {
  flow: composeStories(flow) as unknown as Record<string, ComponentType>,
  quest: composeStories(quest) as unknown as Record<string, ComponentType>,
};

const [book = '', name = ''] = (new URLSearchParams(window.location.search).get('story') ?? '').split('/');
const Story = books[book]?.[name];
const root = document.getElementById('root')!;
if (Story) createRoot(root).render(<Story />);
else root.textContent = `No story ${book}/${name}. Stories: ${Object.entries(books).flatMap(([at, stories]) => Object.keys(stories).map((each) => `${at}/${each}`)).join(', ')}`;

// Ready once the faces have loaded and two frames have passed, so every flow has measured its pane and drawn its edges.
void document.fonts.ready.then(() => requestAnimationFrame(() => requestAnimationFrame(() => {
  (window as unknown as { flowReady: boolean }).flowReady = true;
})));
