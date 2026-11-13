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
        { deployable: 'backend/mirror-api', on: 'schema/config-migration', why: 'it mirrors the schema', source: 'declaration', evidence: { repository: 'backend', file: 'daoris.json', range: '', blob: 'b2' }, stale: true },
        { deployable: 'frontend/web-app', on: 'backend/mirror-api', why: 'the owner releases the API first', source: 'policy', evidence: null },
        { deployable: 'frontend/docs', on: 'frontend/web-app', why: 'docs follow the app', source: 'person', evidence: null, waived: 'docs ship alone' },
      ],
      questions: [{ key: 'contract:viewers', kind: 'open', text: 'Viewers or admins?', yes: 'viewers see candidates', blocks: 'the contract', default: 'admins only' }],
      answers: [{ key: 'target:definition', words: 'the second one', at: '2026-11-14T09:00:00Z' }],
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
    ['en', 'backend/mirror-api is released after schema/config-migration', 'declaration', 'read in backend daoris.json', 'an order, no evidence', 'Questions for you', 'Viewers or admins?', 'If not answered: admins only'],
    ['zh', 'backend/mirror-api 在 schema/config-migration 之后发布', '声明', '读自 backend daoris.json', '仅为指定顺序，无证据', '需要你回答', 'Viewers or admins?', '不回答则：admins only'],
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
    ['en', 'evidence changed', 'waived', 'Waived by you: docs ship alone', 'You answered target:definition', 'the second one'],
    ['zh', '依据已变更', '已豁免', '你已豁免：docs ship alone', '你已回答 target:definition', 'the second one'],
  ])('marks a stale order, a waived one with its reason and what the person answered, in %s', async (language, stale, waived, reason, answered, words) => {
    await i18n.changeLanguage(language);
    render(<AskPlanSection plan={askPlanOf(RAW)!} onOpenQuest={vi.fn()} />);

    expect(screen.getByText(stale)).toBeTruthy();
    expect(screen.getByText(waived)).toBeTruthy();
    expect(screen.getByText(reason)).toBeTruthy();
    expect(screen.getByText(new RegExp(answered))).toBeTruthy();
    expect(screen.getByText(words)).toBeTruthy();
  });

  it('offers no answer and no waiver without a door', () => {
    render(<AskPlanSection plan={askPlanOf(RAW)!} onOpenQuest={vi.fn()} />);

    expect(screen.queryByRole('button', { name: 'Answer' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Waive this order' })).toBeNull();
  });

  it('gives the person’s words for a question by its key, and keeps them with the service’s sentence where it refuses', async () => {
    const onSettle = vi.fn((_settle, answered) => answered.refused('No open question `contract:viewers`.'));
    render(<AskPlanSection plan={askPlanOf(RAW)!} onOpenQuest={vi.fn()} onSettle={onSettle} />);
    const press = screen.getByRole('button', { name: 'Answer' });
    expect((press as HTMLButtonElement).disabled).toBe(true);

    await userEvent.type(screen.getByRole('textbox', { name: 'Your answer' }), 'Admins only.');
    await userEvent.click(press);

    expect(onSettle).toHaveBeenCalledWith({ key: 'contract:viewers', words: 'Admins only.' }, expect.anything());
    expect(screen.getByRole('alert').textContent).toContain('No open question');
    expect((screen.getByRole('textbox', { name: 'Your answer' }) as HTMLInputElement).value).toBe('Admins only.');
  });

  it('waives an order with the person’s reason', async () => {
    const onSettle = vi.fn((_settle, answered) => answered.done());
    render(<AskPlanSection plan={askPlanOf(RAW)!} onOpenQuest={vi.fn()} onSettle={onSettle} />);

    await userEvent.click(screen.getAllByRole('button', { name: 'Waive this order' })[0]!);
    await userEvent.type(screen.getByRole('textbox', { name: 'Why this order is waived' }), 'additive change');
    await userEvent.click(screen.getByRole('button', { name: 'Waive' }));

    expect(onSettle).toHaveBeenCalledWith(
      { deployable: 'backend/mirror-api', on: 'schema/config-migration', reason: 'additive change' }, expect.anything());
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
