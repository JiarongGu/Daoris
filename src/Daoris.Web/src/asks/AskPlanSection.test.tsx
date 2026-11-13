import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import { askPlanOf } from '../work/askPlan';
import { AskPlanSection } from './AskPlanSection';

const RAW = {
  ask: 'a1',
  plans: [
    {
      scope: null, revision: 2, depth: 1, why: 'one part per thing a person sees',
      parts: [
        { id: 'api', repository: 'media', quest: 'q1', outcome: 'the config names the field', lanes: [], claims: ['src/api/**'] },
        { id: 'page', repository: 'media', quest: null, outcome: 'the page shows it', lanes: ['web'], claims: [] },
      ],
      edges: [{ part: 'page', on: 'api', kind: 'after', why: 'it reads the field' }],
      flags: [{ kind: 'claim-record', part: 'api', text: 'api claims TASKS.md, a record' }],
    },
    { scope: 'api', revision: 1, depth: 2, why: 'three splits of my own', parts: [{ id: 'api.db', repository: 'media', quest: 'q3', outcome: 'the column exists' }], edges: [], flags: [] },
  ],
};

describe('an ask’s plan', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it.each([
    ['en', 'Plan, revision 2', 'Quest #q1', 'No quest yet', 'page builds on api', 'Record claimed', 'Split of api, revision 1'],
    ['zh', '计划，第 2 版', '委托 #q1', '尚无委托', 'page 建立在 api 之上', '占用了记录', 'api 的拆分，第 1 版'],
  ])('says each part with its quest, the order, the flag and the worker’s split, in %s', async (language, own, quest, none, after, flag, split) => {
    await i18n.changeLanguage(language);
    const onOpenQuest = vi.fn();
    render(<AskPlanSection plan={askPlanOf(RAW)!} onOpenQuest={onOpenQuest} />);

    expect(screen.getByText(own)).toBeTruthy();
    expect(screen.getByText('the page shows it')).toBeTruthy();
    expect(screen.getByText(none)).toBeTruthy();
    expect(screen.getByText(new RegExp(after))).toBeTruthy();
    expect(screen.getByText(flag)).toBeTruthy();
    expect(screen.getByText(split)).toBeTruthy();

    await userEvent.click(screen.getByRole('button', { name: quest }));
    expect(onOpenQuest).toHaveBeenCalledWith('q1');
  });

  it('reads no plan from an answer that is not one, and an ask with none is an answer with no revisions', () => {
    expect(askPlanOf({ asks: [] })).toBeNull();
    expect(askPlanOf({ ask: 'a1', plans: [] })).toEqual({ plans: [] });
    expect(askPlanOf({ plans: [{ parts: [{ outcome: 'no id' }] }] })!.plans[0]!.parts).toEqual([]);
  });
});
