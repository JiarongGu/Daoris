import { describe, expect, it } from 'vitest';
import { ATTENTION_GROUP, ATTENTION_PLACE } from './attention';

// Every kind of What needs you in its group, and at the one place its door opens, which the bell's count is summed over.

describe('the kinds of What needs you', () => {
  it('puts each kind in its group', () => {
    expect(ATTENTION_GROUP).toEqual({
      parked: 'holding', 'parked-quest': 'holding', 'steward-stopped': 'holding', 'policy-hold': 'holding', unclosed: 'holding', 'request-declined': 'holding', 'go-ahead': 'holding',
      permission: 'holding', answer: 'holding', task: 'holding', confirmation: 'holding', trust: 'holding', quiet: 'holding', 'helper-ended': 'holding',
      'account-wait': 'holding', 'signed-out': 'holding', proposal: 'word', intake: 'word', departure: 'word', 'set-up': 'word', opinion: 'word', rule: 'word', helper: 'word',
      'hand-over': 'word', unanswerable: 'word', left: 'word', review: 'ready', work: 'word', space: 'word', 'git-held': 'word', memory: 'word', integration: 'word', 'git-ask': 'holding', 'second-check': 'word',
    });
  });

  /**
   * A number on a place counts only what waits on the person, the bell's rows, each at the one place
   * its door opens, where its step is taken; so the places' numbers sum to what the bell counts of the rows, and a press is the bell's.
   */
  it('is the place its door opens, for every kind', () => {
    expect(ATTENTION_PLACE).toEqual({
      parked: 'sessions', review: 'sessions', left: 'sessions', opinion: 'sessions', helper: 'sessions', 'hand-over': 'sessions',
      permission: 'sessions', answer: 'sessions', quiet: 'sessions', 'helper-ended': 'sessions', memory: 'sessions',
      proposal: 'quests', intake: 'quests', unanswerable: 'quests', 'parked-quest': 'quests', 'steward-stopped': 'quests', 'policy-hold': 'quests', unclosed: 'quests', 'request-declined': 'quests', 'go-ahead': 'quests',
      departure: 'quests', 'set-up': 'quests', trust: 'quests', task: 'quests', work: 'projects', integration: 'projects',
      rule: 'agents', 'account-wait': 'agents', 'signed-out': 'agents', space: 'settings', confirmation: null, 'git-held': 'settings', 'git-ask': 'sessions', 'second-check': null,
    });
  });
});
