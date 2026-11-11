import { describe, expect, it } from 'vitest';
import { drawnRevisions } from '../sessions/entries';
import { nodeWordPayload } from './workflow';

// What the window's presses send: the node door's Accept names the record it drew and the opinion's token it answers, the review's
// Stop says `whole`, and an entry's Accept names each part's revision; a skip sends what it always sent.
describe('the presses on the review record', () => {
  it('sends an Accept with the record drawn, and a skip with only its words', () => {
    expect(nodeWordPayload({ run: 'q1', node: 'review', act: 'accept', words: null, attempt: 2, revision: 'feedc0de', answers: 't1' }))
      .toEqual({ run: 'q1', node: 'review', act: 'accept', attempt: 2, revision: 'feedc0de', answers: 't1' });
    expect(nodeWordPayload({ run: 'q1', node: 'look', act: 'skip', words: 'not needed' }))
      .toEqual({ run: 'q1', node: 'look', act: 'skip', words: 'not needed' });
  });

  it('sends the review\'s Stop as whole, and a send-back with its part', () => {
    expect(nodeWordPayload({ run: 'q1', node: 'review', act: 'stop', words: 'enough', whole: true }))
      .toEqual({ run: 'q1', node: 'review', act: 'stop', words: 'enough', whole: true });
    expect(nodeWordPayload({ run: 'q1', node: 'review', act: 'changes', words: 'shorter', part: 'q2' }))
      .toEqual({ run: 'q1', node: 'review', act: 'changes', words: 'shorter', part: 'q2' });
  });

  it('names each part\'s revision the entry plan drew, and none for a part with no review', () => {
    expect(drawnRevisions({ parts: [{ session: 's1', revision: 'a1' }, { session: 's2', revision: null }, { session: 's3' }] }))
      .toEqual([{ session: 's1', revision: 'a1' }]);
    expect(drawnRevisions(null)).toEqual([]);
  });
});
