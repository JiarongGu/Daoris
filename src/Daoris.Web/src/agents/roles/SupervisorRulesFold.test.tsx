import { fireEvent, render as rtlRender, screen, waitFor, within } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import userEvent from '@testing-library/user-event';
import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { SupervisorRulesRead } from '../../data/wiring';
import i18n from '../../locales/i18n';
import { SupervisorRulesFold } from './SupervisorRulesFold';

// CONTEXTDRIVE1g (the context counters design §7.1): a place's supervisor rules edited on the page: each rule a row, each saying what it
// replaces, an inherited rule offered with *Set here instead…*, written whole at the revision read, the driver's refusal kept under Save.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const COUNTS: SupervisorRulesRead['counts'] = [
  { count: 'answers-alone', window: 'in-a-row', actions: ['hold', 'refer', 'off'], least: 1, most: 20 },
  { count: 'failures', window: 'since-try-again', actions: ['hold', 'refer', 'off'], least: 1, most: 20 },
  { count: 'tries', window: 'episode', actions: ['hold', 'ask', 'next', 'weighted', 'refer'], least: 1, most: 5 },
];
const READ: SupervisorRulesRead = {
  counts: COUNTS,
  sets: [
    { place: 'machine', revision: 3, rules: [
      { id: 'r-1', count: 'answers-alone', window: 'in-a-row', at: 2, do: 'hold' }, { id: 'r-2', count: 'failures', window: 'since-try-again', at: 3, do: 'hold' },
      { id: 'r-3', count: 'tries', window: 'episode', at: 5, do: 'hold' },
    ] },
    { place: 'repository:daoris', revision: 1, rules: [{ id: 'r-1', count: 'answers-alone', window: 'in-a-row', at: 3, do: 'hold' }] },
  ],
};
const ROLES = [{ value: 'build', label: 'Build' }];

const open = (place = 'repository:daoris', refuse?: string) => {
  const onSave = vi.fn((_press, answered) => (refuse ? answered.refused(refuse) : answered.done()));
  render(<SupervisorRulesFold place={place} placeName={place === 'machine' ? 'This Daoris' : 'daoris'} read={READ} roles={ROLES} busy={false} refused={null} onSave={onSave} />);
  return { onSave, form: within(screen.getByRole('form', { name: place === 'machine' ? 'Supervisor: its rules for This Daoris' : 'Supervisor: its rules for daoris' })) };
};

describe('The supervisor rules fold', () => {
  afterEach(() => i18n.changeLanguage('en'));

  it("draws the place's rules as rows, says the rule each replaces, and offers the machine's other rules to set here", () => {
    const { form } = open();

    expect(form.getByRole('combobox', { name: 'Rule 1: count' })).toHaveTextContent('Answers alone in a row');
    expect(form.getByRole('textbox', { name: 'Rule 1: reaches' })).toHaveValue('3');
    expect(form.getByRole('combobox', { name: 'Rule 1: then' })).toHaveTextContent('Wait for you');
    expect(form.getByText("Replaces This Daoris's: at 2: Wait for you")).toBeTruthy();
    expect(form.getByText('Failed sessions on a quest: at 3: Wait for you (from This Daoris)')).toBeTruthy();
    expect(form.getByText('The service did not answer: at 5: Wait for you (from This Daoris)')).toBeTruthy();
  });

  it("draws a repository's workspace as the place its rules replace and inherit from, and a Build rule leaves the machine's rule to the other roles", () => {
    const read: SupervisorRulesRead = { counts: COUNTS, wider: { 'repository:daoris': ['workspace:w'] }, sets: [
      READ.sets[0]!,
      { place: 'workspace:w', revision: 1, rules: [{ id: 'w-1', count: 'answers-alone', window: 'in-a-row', at: 4, do: 'hold' }, { id: 'w-2', count: 'failures', window: 'since-try-again', at: 5, do: 'hold', roles: ['build'] }] },
      { place: 'repository:daoris', revision: 1, rules: [{ id: 'r-1', count: 'answers-alone', window: 'in-a-row', at: 3, do: 'hold' }, { id: 'r-2', count: 'tries', window: 'episode', at: 2, do: 'hold', roles: ['build'] }] },
    ] };
    render(<SupervisorRulesFold place="repository:daoris" placeName="daoris" read={read} roles={[...ROLES, { value: 'chat', label: 'Chat' }]} busy={false} refused={null} onSave={vi.fn()} />);
    const form = within(screen.getByRole('form', { name: 'Supervisor: its rules for daoris' }));

    expect(form.getByText("Replaces Workspace w's: at 4: Wait for you")).toBeTruthy();
    expect(form.getByText("Replaces This Daoris's: at 5: Wait for you")).toBeTruthy();
    expect(form.getByText('Failed sessions on a quest: at 5: Wait for you for Build (from Workspace w)')).toBeTruthy();
    expect(form.getByText('Failed sessions on a quest: at 3: Wait for you (from This Daoris)')).toBeTruthy();
    expect(form.getByText('The service did not answer: at 5: Wait for you (from This Daoris)')).toBeTruthy();
  });

  it("keeps a rule set for two roles as read, and a save of another rule writes it back for both", async () => {
    const read: SupervisorRulesRead = { counts: COUNTS, sets: [READ.sets[0]!, { place: 'repository:daoris', revision: 2, rules: [
      { id: 'r-1', count: 'tries', window: 'episode', at: 2, do: 'weighted', roles: ['build', 'design'] },
      { id: 'r-2', count: 'failures', window: 'since-try-again', at: 3, do: 'hold', roles: ['build', 'design'] },
    ] }] };
    const onSave = vi.fn((_press, answered) => answered.done());
    render(<SupervisorRulesFold place="repository:daoris" placeName="daoris" read={read} roles={[...ROLES, { value: 'design', label: 'Design' }]} busy={false} refused={null} onSave={onSave} />);
    const form = within(screen.getByRole('form', { name: 'Supervisor: its rules for daoris' }));

    expect(form.getByLabelText('Rule 1: for')).toHaveTextContent('Build and Design');
    expect(form.queryByRole('combobox', { name: 'Rule 1: for' })).toBeNull();
    const at = form.getByRole('textbox', { name: 'Rule 2: reaches' });
    at.focus();
    await userEvent.keyboard('{ArrowUp}');
    fireEvent.click(form.getByRole('button', { name: 'Save' }));

    expect(onSave.mock.calls[0]![0].rules.map((rule: { roles?: string[] }) => rule.roles)).toEqual([['build', 'design'], ['build', 'design']]);
  });

  it('steps the threshold with the arrow keys within the count\'s bounds', async () => {
    const { form } = open();
    const at = form.getByRole('textbox', { name: 'Rule 1: reaches' });

    at.focus();
    await userEvent.keyboard('{ArrowUp}{ArrowUp}');
    expect(at).toHaveValue('5');
    for (let step = 0; step < 30; step++) await userEvent.keyboard('{ArrowDown}');
    expect(at).toHaveValue('1');
  });

  it('writes the place whole at the revision read, with a rule copied here and a rule added', async () => {
    const { form, onSave } = open();

    fireEvent.click(form.getAllByRole('button', { name: 'Set here instead…' })[0]!);
    fireEvent.click(form.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(onSave).toHaveBeenCalledTimes(1));
    expect(onSave.mock.calls[0]![0]).toEqual({
      change: 'set', id: 'supervisor', place: 'repository:daoris', rulesRevision: 1,
      rules: [{ count: 'answers-alone', at: 3, do: 'hold' }, { count: 'failures', at: 3, do: 'hold' }],
    });
  });

  it('adds a rule on the first count not yet ruled here, and takes one out', async () => {
    const { form, onSave } = open();

    fireEvent.click(form.getByRole('button', { name: 'Add a rule' }));
    expect(form.getByRole('combobox', { name: 'Rule 2: count' })).toHaveTextContent('Failed sessions on a quest');
    fireEvent.click(form.getByRole('button', { name: 'Remove rule 1' }));
    fireEvent.click(form.getByRole('button', { name: 'Save' }));

    expect(onSave.mock.calls[0]![0]).toMatchObject({ rules: [{ count: 'failures', at: 1, do: 'hold' }] });
  });

  it('keeps Save off until something changed, and keeps the draft under the driver\'s sentence', () => {
    const { form } = open('repository:daoris', 'daoris would leave build lines with no tries rule');

    expect(form.getByRole('button', { name: 'Save' })).toBeDisabled();
    fireEvent.change(form.getByRole('textbox', { name: 'Rule 1: reaches' }), { target: { value: '4' } });
    fireEvent.click(form.getByRole('button', { name: 'Save' }));

    expect(form.getByText('daoris would leave build lines with no tries rule')).toBeTruthy();
    expect(form.getByRole('textbox', { name: 'Rule 1: reaches' })).toHaveValue('4');
  });

  it('offers no removal of the machine\'s tries rule, and no tries rule to take out', () => {
    const { form } = open('machine');

    expect(form.getByRole('button', { name: 'Remove rule 3' })).toBeDisabled();
    expect(form.getByRole('button', { name: 'Remove rule 1' })).toBeEnabled();
  });

  it('is in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<SupervisorRulesFold place="repository:daoris" placeName="daoris" read={READ} roles={ROLES} busy={false} refused={null} onSave={vi.fn()} />);
    const form = within(screen.getByRole('form', { name: '主管：daoris的规则' }));

    expect(form.getByRole('combobox', { name: '第 1 条：计数' })).toHaveTextContent('连续仅靠回答继续');
    expect(form.getByText('取代整个 Daoris的：达到 2：等你处理')).toBeTruthy();
    expect(form.getByRole('button', { name: '保存' })).toBeTruthy();
  });
});
