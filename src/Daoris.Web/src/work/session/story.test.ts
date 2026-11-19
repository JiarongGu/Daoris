import { describe, expect, it } from 'vitest';
import { lineRowsOf } from '../exchanges/rows';
import { storyOf } from './story';
import { LINE, PIECE } from './session.fixtures';

// A line of work with two turns, a step's start and end, a helper asked and answered, a second opinion handed as a turn, a channel
// message and a piece asked of a partner: the story reads them in the order they happened, whatever source each came from.

describe('storyOf', () => {
  it('reads turns, a step, a helper, an opinion and its line\'s rows in time order', () => {
    const story = storyOf(LINE);
    expect(story.map((item) => item.kind)).toEqual([
      'step', 'owner', 'tools', 'helper', 'agent', 'line', 'step', 'step', 'line', 'opinion', 'agent',
    ]);
    expect(story.filter((item) => item.kind === 'line').map((item) => item.key)).toEqual(['line:message:m1', 'line:piece:p1/asked']);
    const times = story.map((item) => item.at);
    expect([...times].sort((a, b) => a - b)).toEqual(times);
  });

  it('places the session that published its quest first and a row with no moment under the last line', () => {
    const rows = lineRowsOf({ askedBy: 'parent', exchanges: [{ ...PIECE, id: 'opinion:o1', expects: 'opinion', at: undefined }] });
    const story = storyOf({ ...LINE, rows });
    expect(story[0]).toMatchObject({ kind: 'line', key: 'line:asked-by:parent' });
    expect(story[story.length - 1]).toMatchObject({ kind: 'line', key: 'line:opinion:o1/asked' });
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

  it('names the person\'s words as theirs', () => {
    const story = storyOf(LINE);
    expect(story.find((item) => item.kind === 'owner')).toMatchObject({ who: 'you', text: expect.stringContaining('Rate-limit') });
  });

  it('is empty for a line with nothing in it', () => {
    expect(storyOf({ turns: [], steps: [], exchanges: [], rows: [] })).toEqual([]);
  });
});
