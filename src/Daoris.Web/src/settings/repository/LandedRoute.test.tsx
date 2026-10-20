import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import { code } from '../../test/code';
import { NO_VERIFICATION, START } from '../../workflow/fixtures';
import { LandedRoute, landedSummary } from './LandedRoute';

// FABRIC-PIPELINE1a (D192 point 2, the pipeline design §1.3): Setup's route row, the landed route of a repository: the verification
// workflow the work that lands follows, what chose it, where its gates stand, and the person's choice among the verification workflows
// saved here, no verification, or the workspace's again. Choosing one accepts the declaration at the line's tip. A molecule.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

describe("a repository's landed route on its Setup", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says the route, what chose it and its gates, with its terminal twin', () => {
    render(<LandedRoute repository="engine" workspace="aurora" start={START} onChoose={vi.fn()} />);

    expect(screen.getByText('Verification of the work that lands')).toBeInTheDocument();
    expect(screen.getByText(/^Verify the line, as engine chooses\./)).toHaveTextContent(
      "Verify the line, as engine chooses. Its gates run unasked as you accepted them, at blob 3b18e51 on main's tip aa11bb2.");
    expect(screen.getByText(code('daoris driver workflow use <id> --repository engine --landed'))).toBeInTheDocument();
  });

  it("chooses among the verification workflows saved here, no verification, or the workspace's again, and the choice goes out", async () => {
    const onChoose = vi.fn();
    render(<LandedRoute repository="engine" workspace="aurora" start={START} onChoose={onChoose} />);

    expect(screen.getByRole('button', { name: 'Verify the line' })).toHaveAttribute('aria-pressed', 'true');
    await userEvent.click(screen.getByRole('button', { name: 'No verification here' }));
    expect(onChoose).toHaveBeenLastCalledWith('current', expect.objectContaining({ done: expect.any(Function) }));
    await userEvent.click(screen.getByRole('button', { name: "Follow aurora's" }));
    expect(onChoose).toHaveBeenLastCalledWith(null, expect.anything());
    expect(screen.getByText(/When you choose one, you accept the/)).toBeInTheDocument();
  });

  it('says how to make one where no verification workflow is saved here, and offers no workspace\'s where it names none of its own', () => {
    render(<LandedRoute repository="engine" workspace="aurora" start={NO_VERIFICATION} onChoose={vi.fn()} />);

    expect(screen.getByText(/No verification workflow is saved here/)).toHaveTextContent(
      'No verification workflow is saved here: daoris driver workflow import <file> makes one from a file marked verification.');
    expect(screen.getByRole('button', { name: 'No verification here' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.queryByRole('button', { name: "Follow aurora's" })).not.toBeInTheDocument();
  });

  it('folds to a line naming the route, and is worded in 中文', async () => {
    expect(landedSummary(i18n.t, START)).toBe('verified by Verify the line');
    expect(landedSummary(i18n.t, NO_VERIFICATION)).toBe('no verification');
    await i18n.changeLanguage('zh');
    render(<LandedRoute repository="engine" workspace="aurora" start={START} onChoose={vi.fn()} />);
    expect(screen.getByText('落地工作的验证')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '这里不做验证' })).toBeInTheDocument();
    expect(landedSummary(i18n.t, START)).toBe('由 Verify the line 验证');
  });
});
