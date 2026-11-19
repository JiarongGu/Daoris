import { describe, expect, it } from 'vitest';
import { storyOf } from './story';
import { LINE } from './session.fixtures';

// A line of work with two turns, a step's start and end, a helper asked and answered, a second opinion handed as a turn and a
// channel message: the story reads them in the order they happened, whatever source each came from.

describe('storyOf', () => {
  it('reads turns, a step, a helper, an opinion and a channel message in time order', () => {
    const story = storyOf(LINE);
    expect(story.map((item) => item.kind)).toEqual([
      'step', 'owner', 'tools', 'helper', 'agent', 'channel', 'step', 'step', 'opinion', 'agent',
    ]);
    const times = story.map((item) => item.at);
    expect([...times].sort((a, b) => a - b)).toEqual(times);
  });

  it('folds each burst of tool activity into one line that counts its calls by kind', () => {
    const tools = storyOf(LINE).find((item) => item.kind === 'tools');
    expect(tools).toMatchObject({ kind: 'tools', failed: 1, files: 1 });
    expect(tools?.kind === 'tools' && tools.calls).toEqual([{ kind: 'read', count: 2 }, { kind: 'edit', count: 1 }, { kind: 'command', count: 1 }]);
  });

  it('joins a helper\'s answer to its question and never draws the answer\'s turn twice', () => {
    const story = storyOf(LINE);
    const helper = story.find((item) => item.kind === 'helper');
    expect(helper).toMatchObject({ question: 'Where is the Redis client?', answer: { text: 'src/infra/redis.ts, pooled; reuse it' } });
    expect(story.filter((item) => item.kind === 'owner' && item.text.includes('redis.ts'))).toEqual([]);
  });

  it('names the person\'s words as theirs and the channel\'s poster as posted', () => {
    const story = storyOf(LINE);
    expect(story.find((item) => item.kind === 'owner')).toMatchObject({ who: 'you', text: expect.stringContaining('Rate-limit') });
    expect(story.find((item) => item.kind === 'channel')).toMatchObject({ who: 'web-app', text: expect.stringContaining('quota') });
  });

  it('is empty for a line with nothing in it', () => {
    expect(storyOf({ turns: [], steps: [], exchanges: [], channel: [] })).toEqual([]);
  });
});
