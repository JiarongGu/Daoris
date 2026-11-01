import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { LIST } from '../edit/editFixtures';
import type { WorkflowUseAnswer } from '../edit/answers';
import { FULL } from '../fixtures';
import { ChangeWorkflow } from './ChangeWorkflow';

// AGENTSPLACE1h (the Agents-place design §3): Change… lists the presets by their composed line, the saved workflows and Current, labelled
// live and derived. A molecule: the lists arrive as props, and the choice and the editor's start go out.

const USED: WorkflowUseAnswer = { done: true, refused: null, message: null, changed: true, reach: [] };

const SAVED_ELSEWHERE = {
  ...LIST,
  workflows: [
    ...LIST.workflows,
    { id: 'verify-docs', name: null, problem: null, versions: [1], schema: 2 as const, digest: 'cd', line: 'Docs are checked first.', pipeline: null,
      chosen: [{ level: 'workspace' as const, name: 'aurora', role: 'kind' as const, kind: 'docs' }] },
  ],
};

const draw = (props: Partial<Parameters<typeof ChangeWorkflow>[0]> = {}) => {
  const onUse = vi.fn(async () => USED);
  const onStartFrom = vi.fn();
  const onClose = vi.fn();
  render(<ChangeWorkflow list={SAVED_ELSEWHERE} current={FULL} place={{ repository: 'engine' }} onUse={onUse} onStartFrom={onStartFrom} onClose={onClose} {...props} />);
  return { onUse, onStartFrom, onClose };
};

// The language goes back inside `act`, with the page still drawn (FRAMEACT1).
afterEach(async () => { await act(async () => { await i18n.changeLanguage('en'); }); });

describe('Change…', () => {
  it('lists the presets by their composed line, the saved workflows, and Current labelled live and derived', () => {
    draw();

    expect(screen.getByText(/Live and derived from the rules as they stand/)).toBeInTheDocument();
    expect(screen.getByText('v4740fb017006')).toBeInTheDocument();
    expect(screen.getByText('Documents')).toBeInTheDocument();
    expect(screen.getByText('The work merges into the line.')).toBeInTheDocument();
    expect(screen.getByText('Docs release')).toBeInTheDocument();
    expect(screen.getByText('v2')).toBeInTheDocument();
  });

  it('says where a saved workflow is chosen, and offers *Use here* for the ones that are not chosen here', () => {
    draw();

    // `docs-release` is chosen by engine: it is chosen here; `verify-docs` is chosen at the workspace.
    expect(screen.getByText('Chosen here')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Use Docs release here' })).toBeNull();
    expect(screen.getByText('Chosen at aurora')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Use verify-docs here' })).toBeEnabled();
  });

  it('chooses a saved workflow at the version it read, and Current by name', async () => {
    const { onUse } = draw();

    await userEvent.click(screen.getByRole('button', { name: 'Use verify-docs here' }));
    expect(onUse).toHaveBeenLastCalledWith({ repository: 'engine', workflow: 'verify-docs', version: 1 });

    await userEvent.click(screen.getByRole('button', { name: 'Use Current here' }));
    expect(onUse).toHaveBeenLastCalledWith({ repository: 'engine', workflow: 'current' });
  });

  it("says the driver's refusal where a choice is refused", async () => {
    const onUse = vi.fn(async (): Promise<WorkflowUseAnswer> => ({
      done: false, refused: 'stale', message: '`verify-docs` is at v2 now, not v1: read it again and choose that. Nothing was written.', changed: false, reach: [],
    }));
    draw({ onUse });

    await act(async () => { await userEvent.click(screen.getByRole('button', { name: 'Use verify-docs here' })); });

    expect(await screen.findByRole('alert')).toHaveTextContent('verify-docs is at v2 now, not v1');
  });

  it("starts the editor from a preset's steps, and not from one that has none", async () => {
    const { onStartFrom } = draw();

    await userEvent.click(screen.getByRole('button', { name: 'Edit steps from Documents' }));
    expect(onStartFrom).toHaveBeenCalledWith({ graph: expect.objectContaining({ nodes: expect.any(Array) }), name: 'Documents' });
    expect(screen.queryByRole('button', { name: 'Edit steps from A pull request with no press' })).toBeNull();
  });

  it('says the lists are on their way, and a refusal of the read', () => {
    const { unmount } = render(<ChangeWorkflow list={null} current={FULL} place={{ repository: 'engine' }} onUse={vi.fn()} onStartFrom={vi.fn()} onClose={vi.fn()} />);
    expect(screen.getByText('Reading the saved workflows…')).toBeInTheDocument();
    unmount();

    render(<ChangeWorkflow list={null} error={new Error('the driver is still coming up.')} current={FULL} place={{ repository: 'engine' }} onUse={vi.fn()} onStartFrom={vi.fn()} onClose={vi.fn()} />);
    expect(screen.getByRole('alert')).toHaveTextContent('the driver is still coming up.');
  });

  it('closes, and says its words in Chinese', async () => {
    const { onClose } = draw();
    await userEvent.click(screen.getByRole('button', { name: 'Close' }));
    expect(onClose).toHaveBeenCalledOnce();

    await act(async () => { await i18n.changeLanguage('zh'); });
    expect(within(screen.getByRole('region', { name: '更改它的工作所遵循的工作流' })).getByText('预设')).toBeInTheDocument();
    expect(screen.getByText('文档')).toBeInTheDocument();
  });
});
