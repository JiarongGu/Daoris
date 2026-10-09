import { describe, expect, it } from 'vitest';
import { aroundTheLanding, setupDoorOf, yourPart } from './current';
import { BRANCH_AUTOMATIC, FULL, LOOK, MERGE_YOU_ACCEPT, NOTHING_SET, OPINION, PULL_REQUEST, WORK, WORKSPACE } from './fixtures';

// WORKFLOW1b: what the page reads off a Current it was answered, and nothing it derives (design §2.7): the steps around
// the landing, the Setup section that sets each step, and the person's part composed from the steps.

describe('a Current read for drawing', () => {
  it('draws the landing last before it lands, and what follows it after', () => {
    expect(aroundTheLanding(FULL.steps)).toEqual({ before: [FULL.steps[0], OPINION, LOOK, BRANCH_AUTOMATIC], after: [PULL_REQUEST] });
    expect(aroundTheLanding(NOTHING_SET.steps)).toEqual({ before: NOTHING_SET.steps, after: [] });
    // A shape with no landing, which Current never draws, is drawn whole rather than dropped.
    expect(aroundTheLanding([WORK()])).toEqual({ before: [WORK()], after: [] });
  });

  it("opens the Setup that sets a step: the repository's own, or its workspace's where the workspace set it", () => {
    expect(setupDoorOf(WORK(), 'repository')).toBeNull();
    expect(setupDoorOf(MERGE_YOU_ACCEPT, 'repository')).toEqual({ where: 'repository', section: 'work' });
    expect(setupDoorOf(LOOK, 'repository')).toEqual({ where: 'repository', section: 'work' });
    expect(setupDoorOf(OPINION, 'repository')).toEqual({ where: 'workspace', section: 'defaults' });
    // On a workspace's page every rule is its own.
    for (const step of WORKSPACE.steps.slice(1)) expect(setupDoorOf(step, 'workspace')).toEqual({ where: 'workspace', section: 'defaults' });
    expect(setupDoorOf(WORK(), 'workspace')).toBeNull();
  });

  it("composes the person's part from the steps' presses, the look's with its environment", () => {
    expect(yourPart(FULL.steps)).toEqual([{ press: 'reviewed', environment: 'dev' }, { press: 'merge' }]);
    expect(yourPart(NOTHING_SET.steps)).toEqual([{ press: 'accept' }]);
    expect(yourPart([WORK(), BRANCH_AUTOMATIC])).toEqual([]);
  });
});
