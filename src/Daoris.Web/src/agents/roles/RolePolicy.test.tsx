import * as Tooltip from '@radix-ui/react-tooltip';
import { fireEvent, render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { ActorRow, RoleCatalogue, RolePolicyRow } from '../../data/wiring';
import i18n from '../../locales/i18n';
import { RolesCard, policyLine } from './Roles';

// STAFFPOLICY1f (the staff policy design §6.1): a role's staffing as a policy, set in the role editor. How it chooses, each choice's weight
// and aim, its limits and what happens when a choice cannot go on; saved through the role door with the policy revision read, the draft kept
// on a refusal; the stale and no-store states said; the next new start read from `ROLES`.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const POLICY: RolePolicyRow = {
  role: 'build', policyState: 'set', revision: 4,
  staffPolicy: {
    rule: 'weight',
    choices: [{ agent: 'claude-code', model: 'sonnet', effort: 'high', weight: 3 }, { agent: 'claude-code', model: 'opus', effort: 'high', weight: 1, budget: { starts: 6 }, takes: ['design'] }],
    moves: { limit: 'weighted', transient: { do: 'weighted', after: 2 } },
    most: 3,
  },
  next: { choice: '1', by: 'weight', said: 'by weight: 3 of the last 4 were choice 1, aim 3 in 4', agent: 'claude-code', model: 'sonnet', effort: 'high' },
};

const catalogue = (policy: RolePolicyRow | null = POLICY, over: Partial<RoleCatalogue> = {}): RoleCatalogue => ({
  roles: [{ id: 'build', state: 'seed', seeded: true, name: { en: 'Build', zh: '开发' }, takes: ['build'], scopeKind: 'machine', revision: 0 }],
  works: ['build', 'design'], kept: true, suggested: [], ...(policy ? { policies: [policy] } : {}), ...over,
});

const build: ActorRow = {
  id: 'build', state: 'set',
  choices: [{ agent: 'claude-code', model: 'sonnet', effort: 'high', from: '1' }, { agent: 'claude-code', model: 'opus', effort: 'high', from: '2' }],
};

const AGENTS = [{ value: 'claude-code', label: 'Claude Code' }];

async function choose(name: string, option: string) {
  const user = userEvent.setup();
  screen.getByRole('combobox', { name }).focus();
  await user.keyboard('{Enter}');
  await user.click(await screen.findByRole('option', { name: option }));
}

const open = (read: RoleCatalogue = catalogue(), refuse?: string, title = 'Build') => {
  const onSet = vi.fn((_press, answered) => (refuse ? answered.refused(refuse) : answered.done()));
  render(<RolesCard catalogue={read} staffing={[build]} agents={AGENTS} onSet={onSet} onSetActor={vi.fn()} />);
  fireEvent.click(screen.getByRole('button', { name: 'Edit Build' }));
  return { onSet, form: within(screen.getByRole('form', { name: title })) };
};

describe('The role editor, its policy', () => {
  afterEach(() => i18n.changeLanguage('en'));

  it('opens on the policy it was set with: the rule, weights with their aim, limits, moves and the next new start', () => {
    const { form } = open();

    expect(form.getByRole('combobox', { name: 'How it chooses' })).toHaveTextContent('Spread by weight');
    expect(form.getByRole('textbox', { name: "choice 1's weight" })).toHaveValue('3');
    expect(form.getByRole('textbox', { name: "choice 2's weight" })).toHaveValue('1');
    expect(form.getByText('3 in 4')).toBeTruthy();
    expect(form.getByText('1 in 4')).toBeTruthy();
    expect(form.getByText('Limits: design and decisions only · 6 starts a day')).toBeTruthy();
    expect(form.getByText('Limits: none')).toBeTruthy();
    expect(form.getByRole('combobox', { name: 'It reaches its limit' })).toHaveTextContent('Move to the next by weight');
    expect(form.getByText('Next new start: choice 1 (by weight: 3 of the last 4 were choice 1, aim 3 in 4)')).toBeTruthy();
  });

  it('steps a weight with the arrow keys within 0 to 10, and the aim follows', async () => {
    const { form } = open();
    const weight = form.getByRole('textbox', { name: "choice 2's weight" });

    weight.focus();
    await userEvent.keyboard('{ArrowUp}{ArrowUp}');
    expect(weight).toHaveValue('3');
    expect(form.getAllByText('3 in 6')).toHaveLength(2);
    await userEvent.keyboard('{ArrowDown}{ArrowDown}{ArrowDown}{ArrowDown}');
    expect(weight).toHaveValue('0');
    expect(form.getByText('only when no other can')).toBeTruthy();
    for (let at = 0; at < 12; at++) await userEvent.keyboard('{ArrowUp}');
    expect(weight).toHaveValue('10');
  });

  it('keeps the digits of a weight composed by an IME, full-width ones as ASCII, and drops the rest', () => {
    const { form } = open();
    const weight = form.getByRole('textbox', { name: "choice 1's weight" });

    fireEvent.compositionStart(weight);
    fireEvent.change(weight, { target: { value: 'さ' } });
    expect(weight).toHaveValue('さ');
    fireEvent.change(weight, { target: { value: '７' } });
    fireEvent.compositionEnd(weight, { target: { value: '７' } });
    expect(weight).toHaveValue('7');
    fireEvent.change(weight, { target: { value: '2x' } });
    expect(weight).toHaveValue('2');
  });

  it('says its ⓘ by the rule: today\'s sentences in order, the weights under a weight rule', async () => {
    const { form } = open();
    expect(form.getByRole('note')).toHaveAccessibleName(/Choices take new work by their weights; a choice that cannot run passes to the next by weight\./);
    expect(form.getByRole('note')).not.toHaveAccessibleName(/Choice 2 runs only when no account of choice 1 can/);

    await choose('How it chooses', 'First that can run');
    expect(form.getByRole('note')).toHaveAccessibleName(/Choice 2 runs only when no account of choice 1 can/);
    expect(form.getByRole('note')).toHaveAccessibleName(/Put the lighter choice first; Daoris knows no prices\./);
    expect(form.queryByRole('textbox', { name: "choice 1's weight" })).toBeNull();
  });

  it('fills the form from a preset and saves nothing', async () => {
    const { form, onSet } = open();

    await choose('How it chooses', 'Spread evenly');
    expect(form.getByRole('textbox', { name: "choice 1's weight" })).toHaveValue('1');
    expect(form.getByRole('textbox', { name: "choice 2's weight" })).toHaveValue('1');
    expect(onSet).not.toHaveBeenCalled();
  });

  it('carries the policy and the revision it read in the press, and the record as the door takes it', async () => {
    const { form, onSet } = open();

    fireEvent.change(form.getByRole('textbox', { name: "choice 2's weight" }), { target: { value: '2' } });
    await choose('It went quiet and was stopped', 'Try it again');
    fireEvent.click(form.getByRole('button', { name: 'Save' }));

    expect(onSet).toHaveBeenCalledWith(expect.objectContaining({
      change: 'set', id: 'build', choices: [
        { agent: 'claude-code', model: 'sonnet', effort: 'high' }, { agent: 'claude-code', model: 'opus', effort: 'high' },
      ],
      policyRevision: 4,
      policy: {
        rule: 'weight',
        choices: [
          { agent: 'claude-code', model: 'sonnet', effort: 'high', weight: 3 },
          { agent: 'claude-code', model: 'opus', effort: 'high', weight: 2, budget: { starts: 6 }, takes: ['design'] },
        ],
        moves: { limit: 'weighted', transient: { do: 'weighted', after: 2 }, quiet: 'retry' },
        most: 3,
      },
    }), expect.anything());
  });

  it('sends no policy for a role that has none and is left on First that can run, and null to clear one set', async () => {
    const none: RolePolicyRow = { role: 'build', policyState: 'default', revision: 0, staffPolicy: null, next: null };
    const first = open(catalogue(none));
    fireEvent.click(first.form.getByRole('button', { name: 'Save' }));
    expect(first.onSet.mock.calls[0]![0]).not.toHaveProperty('policy');
    expect(first.onSet.mock.calls[0]![0]).not.toHaveProperty('policyRevision');
  });

  it('clears a policy set when the form is put back to First that can run with no limit', async () => {
    const { form, onSet } = open();

    await choose('How it chooses', 'First that can run');
    fireEvent.click(form.getByRole('button', { name: 'Save' }));

    expect(onSet.mock.calls[0]![0]).toMatchObject({ policy: null, policyRevision: 4 });
  });

  it('keeps the draft under the driver\'s sentence when the policy changed since it was read', () => {
    const { form } = open(catalogue(), 'the policy changed since you read it');

    fireEvent.change(form.getByRole('textbox', { name: "choice 1's weight" }), { target: { value: '5' } });
    fireEvent.click(form.getByRole('button', { name: 'Save' }));

    expect(form.getByText('the policy changed since you read it')).toBeTruthy();
    expect(form.getByRole('textbox', { name: "choice 1's weight" })).toHaveValue('5');
    expect(screen.getByRole('form', { name: 'Build' })).toBeTruthy();
  });

  it('opens a stale policy with its fields kept and says it runs in order until saved again', () => {
    const { form } = open(catalogue({ ...POLICY, policyState: 'stale', why: 'its choices changed' }));

    expect(form.getByRole('status')).toHaveTextContent('Its policy was set for other choices; it runs them in order until you save it again.');
    expect(form.getByRole('textbox', { name: "choice 1's weight" })).toHaveValue('3');
  });

  it('says the store holds no policies here and offers none, for an editor over the staffing alone', () => {
    const { form } = open(catalogue(POLICY, { kept: false }), undefined, 'Build: your order');

    expect(form.getByText("Policies are kept in this machine's store, which this home does not use yet.")).toBeTruthy();
    expect(form.queryByRole('combobox', { name: 'How it chooses' })).toBeNull();
    expect(form.queryByText('When a choice cannot go on')).toBeNull();
  });

  it('offers no policy to a driver before policies', () => {
    const { form } = open(catalogue(null));

    expect(form.queryByRole('combobox', { name: 'How it chooses' })).toBeNull();
    expect(form.queryByText(/store, which this home does not use yet/)).toBeNull();
  });

  it('offers a conversation no wait, ask or bound of moves, as its door refuses them', async () => {
    const chat: RoleCatalogue = {
      ...catalogue({ ...POLICY, role: 'chat', staffPolicy: { rule: 'order', choices: [{ agent: 'claude-code', weight: 1 }] } }),
      roles: [{ id: 'chat', state: 'seed', seeded: true, name: { en: 'Chat', zh: '对话' }, takes: ['chat'], scopeKind: 'machine', revision: 0 }],
    };
    render(<RolesCard catalogue={chat} staffing={[{ id: 'chat', state: 'set', choices: [{ agent: 'claude-code', from: '1' }] }]} agents={AGENTS} onSet={vi.fn()} onSetActor={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: 'Edit Chat' }));
    const form = within(screen.getByRole('form', { name: 'Chat' }));
    fireEvent.click(form.getByText('When a choice cannot go on'));

    await choose('It reaches its limit', 'Move to the next choice');
    expect(form.queryByText(/moves to another choice a day for one quest/)).toBeNull();
    await userEvent.setup().click(screen.getByRole('combobox', { name: 'It reaches its limit' }));
    expect(screen.queryByRole('option', { name: 'Wait for it' })).toBeNull();
    expect(screen.queryByRole('option', { name: 'Ask me first' })).toBeNull();
  });

  it('is in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<RolesCard catalogue={catalogue()} staffing={[build]} agents={AGENTS} onSet={vi.fn()} onSetActor={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: '编辑开发' }));
    const form = within(screen.getByRole('form', { name: '开发' }));

    expect(form.getByRole('combobox', { name: '选择方式' })).toHaveTextContent('按权重分配');
    expect(form.getByRole('textbox', { name: '第 1 个选择的权重' })).toHaveValue('3');
    expect(form.getByText('4 份中占 3 份')).toBeTruthy();
    expect(form.getByText('当某个选择无法继续时')).toBeTruthy();
  });
});

describe('A role\'s folded line, its policy', () => {
  afterEach(() => i18n.changeLanguage('en'));
  const t = i18n.t.bind(i18n);

  it('says the rule with its proportions, a move other than as today and the bound', () => {
    expect(policyLine(t, POLICY)).toBe('spread 3:1 · moves on a limit and no answer · at most 3 moves');
    expect(policyLine(t, { ...POLICY, staffPolicy: { rule: 'order', choices: [] } })).toBe('first that can run');
    expect(policyLine(t, { ...POLICY, staffPolicy: { rule: 'weight', choices: [{ agent: 'a', weight: 1 }, { agent: 'b', weight: 1 }] } })).toBe('spread evenly');
  });

  it('says nothing for a role with no policy or a stale one, which runs in order as today', () => {
    expect(policyLine(t, null)).toBe('');
    expect(policyLine(t, { ...POLICY, policyState: 'stale' })).toBe('');
    expect(policyLine(t, { ...POLICY, policyState: 'default', staffPolicy: null })).toBe('');
  });
});
