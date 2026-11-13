import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import { askPlanOf } from './askPlan';
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
      releases: [
        { deployable: 'backend/mirror-api', on: 'schema/config-migration', why: 'it mirrors the schema', source: 'declaration', evidence: 'backend: daoris.json' },
        { deployable: 'frontend/web-app', on: 'backend/mirror-api', why: 'the owner releases the API first', source: 'policy', evidence: null },
      ],
      questions: [{ key: 'contract:viewers', kind: 'open', text: 'Viewers or admins?', yes: 'viewers see candidates', blocks: 'the contract', default: 'admins only' }],
    },
    { scope: 'api', revision: 1, depth: 2, why: 'three splits of my own', parts: [{ id: 'api.db', repository: 'media', quest: 'q3', outcome: 'the column exists' }], edges: [], flags: [] },
  ],
  team: [{ part: null, role: 'lead', session: 's1', revision: 2 }, { part: 'api', role: 'lead', session: 's2', revision: 1 }],
  decisions: [{ scope: null, revision: 2, decision: 'revised', reason: 'the page needs the field first', author: 's1', act: 'api #q1' }],
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

  it.each([
    ['en', 'backend/mirror-api is released after schema/config-migration', 'declaration', 'read in backend: daoris.json', 'an order, no evidence', 'Questions for you', 'Viewers or admins?', 'If not answered: admins only'],
    ['zh', 'backend/mirror-api 在 schema/config-migration 之后发布', '声明', '读自 backend: daoris.json', '仅为指定顺序，无证据', '需要你回答', 'Viewers or admins?', '不回答则：admins only'],
  ])('shows each release order with its source and evidence and the one queue of questions, in %s', async (language, order, source, evidence, policy, queue, question, fallback) => {
    await i18n.changeLanguage(language);
    render(<AskPlanSection plan={askPlanOf(RAW)!} onOpenQuest={vi.fn()} />);

    expect(screen.getByText(new RegExp(order))).toBeTruthy();
    expect(screen.getByText(source)).toBeTruthy();
    expect(screen.getByText(evidence)).toBeTruthy();
    expect(screen.getByText(policy)).toBeTruthy();
    expect(screen.getByText(queue)).toBeTruthy();
    expect(screen.getAllByText(question)).toHaveLength(1);
    expect(screen.getByText(fallback)).toBeTruthy();
  });

  it.each([
    ['en', 'Lead s1: the whole plan', 'Lead s2: api', 'Revision 2, revised by s1: the page needs the field first'],
    ['zh', '负责人 s1：整个计划', '负责人 s2：api', '第 2 版，由 s1 修订: the page needs the field first'],
  ])('says who is on the team and what was decided, in %s', async (language, lead, split, decided) => {
    await i18n.changeLanguage(language);
    render(<AskPlanSection plan={askPlanOf(RAW)!} onOpenQuest={vi.fn()} />);

    expect(screen.getByText(lead)).toBeTruthy();
    expect(screen.getByText(split)).toBeTruthy();
    expect(screen.getByText(decided)).toBeTruthy();
  });

  it('reads no plan from an answer that is not one, and an ask with none is an answer with no revisions', () => {
    expect(askPlanOf({ asks: [] })).toBeNull();
    expect(askPlanOf({ ask: 'a1', plans: [] })).toEqual({ plans: [], team: [], decisions: [] });
    expect(askPlanOf({ plans: [{ parts: [{ outcome: 'no id' }] }] })!.plans[0]!.parts).toEqual([]);
  });
});
