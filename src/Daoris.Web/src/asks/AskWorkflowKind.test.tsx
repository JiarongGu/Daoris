import { type ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../locales/i18n';
import type { Ask } from '../data/api';
import type { Answered } from '../work/InlineConfirm';
import { AskPage } from './AskPage';
import { CLOSED, PROPOSED } from './fixtures';

// WORKFLOW1i-c (workflow design §4.3, D157): the ask's page shows the person's kind choice and each proposal its intake kept,
// and only the person's press applies one.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const CHOSEN: Ask = {
  ...PROPOSED,
  workflowChoices: [{ kind: 'bug', workflow: 'fix-flow', at: new Date(Date.now() - 3_600_000).toISOString() }],
};
const PROPOSING: Ask = {
  ...PROPOSED,
  workflowProposals: [{ kind: 'docs', reason: 'Only the README changes.', at: '2026-10-09T09:00:00Z', quest: 'q1' }],
};
const page = (ask: Ask, props: object = {}) => (
  <AskPage ask={ask} receivers={[]} questTitles={{}} onPublish={vi.fn()} onClose={vi.fn()} onOpenQuest={vi.fn()} {...props} />
);

describe("the kind of an ask's work", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says the kind standing, with its workflow', () => {
    render(page(CHOSEN));
    const section = screen.getByRole('region', { name: 'Kind of work' });
    expect(section).toHaveTextContent('You chose the kind bug, 1h ago, with the workflow fix-flow.');
  });

  it('says none is chosen', () => {
    render(page(PROPOSING));
    expect(screen.getByRole('region', { name: 'Kind of work' })).toHaveTextContent('No kind chosen here.');
  });

  it("shows a proposal with its reason, and applies it only on the person's press, carrying the workflow", async () => {
    const onChooseKind = vi.fn();
    const user = userEvent.setup();
    render(page({ ...PROPOSING, workflowChoices: [{ kind: null, workflow: 'fix-flow', at: '2026-10-09T10:00:00Z' }] }, { onChooseKind }));
    const section = screen.getByRole('region', { name: 'Kind of work' });
    expect(section).toHaveTextContent('The intake proposed the kind docs: Only the README changes.');
    expect(onChooseKind).not.toHaveBeenCalled();
    await user.click(within(section).getByRole('button', { name: 'Apply' }));

    expect(onChooseKind).toHaveBeenCalledWith('docs', 'fix-flow', expect.anything());
  });

  // ACCESSASK1 (the UI/UX design R3): an Apply refused is said under its proposal, in the service's sentence, never a toast.
  it('says an Apply refused under its proposal, until it is pressed again', async () => {
    const onChooseKind = vi.fn();
    const user = userEvent.setup();
    render(page(PROPOSING, { onChooseKind }));
    const section = screen.getByRole('region', { name: 'Kind of work' });
    await user.click(within(section).getByRole('button', { name: 'Apply' }));
    act(() => (onChooseKind.mock.lastCall![2] as Answered).refused('No workflow declares the kind docs.'));

    const proposal = within(section).getByText(/The intake proposed the kind docs/).closest('div')!;
    expect(within(proposal).getByRole('alert')).toHaveTextContent('No workflow declares the kind docs.');
    await user.click(within(proposal).getByRole('button', { name: 'Apply' }));
    expect(within(section).queryByRole('alert')).toBeNull();
  });

  it('hides a proposal the latest choice already has', () => {
    render(page({ ...PROPOSING, workflowChoices: [{ kind: 'docs', at: '2026-10-09T10:00:00Z' }] }, { onChooseKind: vi.fn() }));
    const section = screen.getByRole('region', { name: 'Kind of work' });
    expect(section).not.toHaveTextContent('The intake proposed');
    expect(within(section).queryByRole('button', { name: 'Apply' })).toBeNull();
  });

  it('offers no press on a closed ask', () => {
    render(page({ ...CLOSED, workflowProposals: PROPOSING.workflowProposals }, { onChooseKind: vi.fn() }));
    const section = screen.getByRole('region', { name: 'Kind of work' });
    expect(section).toHaveTextContent('The intake proposed the kind docs');
    expect(within(section).queryByRole('button', { name: 'Apply' })).toBeNull();
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(page(PROPOSING, { onChooseKind: vi.fn() }));
    const section = screen.getByRole('region', { name: '工作类型' });
    expect(section).toHaveTextContent('受理提议类型 docs');
    expect(within(section).getByRole('button', { name: '应用' })).toBeInTheDocument();
  });
});
