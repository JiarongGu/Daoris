import { describe, expect, it } from 'vitest';
import type { Turn } from '../conversation/conversationTypes';
import { lineRowsOf } from '../exchanges/rows';
import type { Wait } from '../waits/waits';
import { storyOf } from './story';
import { storyWaitsOf } from './storyWaits';
import { at, BESIDE_TURN, LINE, PIECE } from './session.fixtures';

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

  it('stands a call that started a subagent or a task, and the task\'s end, alone; thoughts and plans fold with the calls', () => {
    const story = storyOf({ ...LINE, turns: [BESIDE_TURN], steps: [], exchanges: [], rows: [] });
    expect(story.map((item) => item.key)).toEqual(['ask:t9', 'tools:c1', 'beside:c2', 'tools:c3', 'beside:c4', 'beside:c5']);
    const folded = story.find((item) => item.key === 'tools:c1');
    expect(folded?.kind === 'tools' && folded.blocks.map((block) => block.kind)).toEqual(['thought', 'plan']);
  });

  it('names words by their sender: an author by its mark, another session\'s claim on the channel, Daoris\'s target', () => {
    const who = (ask: Partial<Turn['ask'] & object>) => storyOf({
      turns: [{ key: 'w', ask: { key: 'w', text: 'Words', origin: 'target', at: at(9, 0), ...ask }, items: [] }], steps: [], exchanges: [], rows: [],
    })[0];
    expect(who({ from: 'partner' })).toMatchObject({ who: 'partner' });
    expect(who({ channel: 'm1' })).toMatchObject({ who: 'claim', channel: 'm1' });
    expect(who({})).toMatchObject({ who: 'daoris' });
    expect(who({ origin: 'person' })).toMatchObject({ who: 'you' });
  });

  it('reads a wait raised at a turn\'s end onto the turn\'s last line, and each wait once a line', () => {
    const story = storyOf(LINE);
    const goAhead: Wait = { kind: 'goAhead', number: 2 };
    const waits = storyWaitsOf(story, {
      marks: { 'end-t1': [{ kind: 'accept' }], b1: [goAhead], b2: [{ ...goAhead }] }, revealed: 'end-t1', onToWait: () => {},
    });
    expect(waits?.marks).toEqual({ 'helper:h1': [{ kind: 'accept' }], 'tools:b1': [goAhead] });
    expect(waits?.sought).toBe('helper:h1');
  });

  it('is empty for a line with nothing in it', () => {
    expect(storyOf({ turns: [], steps: [], exchanges: [], rows: [] })).toEqual([]);
  });
});
