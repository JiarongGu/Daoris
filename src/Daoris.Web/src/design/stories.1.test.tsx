import { everyStoryOf } from '../test/composeStories';

// A shard of every story's render, shared out by `everyStoryOf` among the `stories.<n>.test.tsx` present.
await everyStoryOf(import.meta.glob('/src/**/*.stories.tsx'), import.meta.glob('/src/design/stories.*.test.tsx'), 'stories.1.test.tsx');
