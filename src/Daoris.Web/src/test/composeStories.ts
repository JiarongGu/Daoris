import { composeStories, setProjectAnnotations } from '@storybook/react-vite';
import preview from '../../.storybook/preview';

// 🔴 Storybook's project annotations, so `composeStories` renders a story the way Storybook does.
//
// Without them, a composed story applies its OWN decorators and none of the project's — so the tooltip provider
// `.storybook/preview.tsx` mounts for every story is absent, and any component carrying a `Tip` throws. That reads as
// "this story needs wrapping", which is the wrong fix: the suite renders what a reviewer sees.
//
// They are set here, by the five files that compose stories, and not in the setup file every test file pays: loading
// Storybook's runtime costs a tenth of the unit run when each of 592 fresh vm contexts loads it.
setProjectAnnotations(preview);

export { composeStories };
