import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../../locales/i18n';
import { type BudgetChange, type CheckBudgetsView, CheckBudgetsRow, budgetSaid, budgetsSummary } from './CheckBudgets';
import type { Answered } from '../../work/InlineConfirm';

// A repository's check budgets on this machine, shown and set on its Setup as `daoris settings checks <repository>` sets them: each
// gate's timeout with where it comes from (a pin, what its green times taught, the default), a rise said, a pin set or taken away, and
// the machine's values for the repository. The page judges nothing: a press goes out and the driver's refusal is said where it is pressed.

const t = i18n.t.bind(i18n);

const VIEW: CheckBudgetsView = {
  repository: 'engine',
  gates: {
    cli: { budget: { minutes: 8, source: 'learned' }, greens: 20 },
    driver: { budget: { minutes: 25, source: 'pin', why: 'killed at 10 on a loaded machine' }, greens: 0, pin: { minutes: 25, why: 'killed at 10 on a loaded machine' } },
    reached: { budget: { minutes: 30, source: 'learned', rise: { from: 15, to: 30 } }, greens: 12, memoryGb: 6 },
    web: { budget: { minutes: 5, source: 'default' }, greens: 0 },
  },
  setTimeoutMs: { 'merge-e2e': 120000 },
  treeBaseline: 'node tools/quick.mjs --record-base',
  devkit: null,
  localInputs: ['local/a.props'],
  patternsFile: null,
};

type Press = (change: BudgetChange, answered: Answered) => void;

const draw = (view: CheckBudgetsView = VIEW, onChange = vi.fn<Press>()) => {
  render(
    <Tooltip.Provider>
      <CheckBudgetsRow repository="engine" view={view} onChange={onChange} />
    </Tooltip.Provider>,
  );
  return onChange;
};

afterEach(async () => { await i18n.changeLanguage('en'); });

describe('each gate\'s timeout says where it comes from', () => {
  it('names a pin with its why, what the green times taught, the default, and a rise', () => {
    expect(budgetSaid(t, VIEW.gates['driver']!)).toBe('25 min, pinned: killed at 10 on a loaded machine');
    expect(budgetSaid(t, VIEW.gates['cli']!)).toBe('8 min, from its last 20 green runs');
    expect(budgetSaid(t, VIEW.gates['web']!)).toBe('5 min, the default: no green run is kept yet');
    expect(budgetSaid(t, VIEW.gates['reached']!)).toBe('30 min, from its last 12 green runs; it rose from 15 min');
  });

  it('folds to a count of the gates that rose and the ones pinned', () => {
    expect(budgetsSummary(t, VIEW)).toBe('1 pinned, 1 slower');
    expect(budgetsSummary(t, { ...VIEW, gates: {} })).toBe('learned from each gate\'s green runs');
  });

  it('lists each gate with its budget', () => {
    draw();
    expect(screen.getByText('driver')).toBeInTheDocument();
    expect(screen.getByText('25 min, pinned: killed at 10 on a loaded machine')).toBeInTheDocument();
    expect(screen.getByText('30 min, from its last 12 green runs; it rose from 15 min')).toBeInTheDocument();
  });
});

describe('a press goes out as the door takes it', () => {
  it('pins a gate with its minutes and why', async () => {
    const onChange = draw();
    await userEvent.click(screen.getByRole('button', { name: 'Pin web' }));
    await userEvent.type(screen.getByLabelText('Minutes for web'), '12');
    expect(screen.getByRole('button', { name: 'Save pin for web' })).toBeDisabled();
    await userEvent.type(screen.getByLabelText('Why for web'), 'a quiet machine killed it');
    await userEvent.click(screen.getByRole('button', { name: 'Save pin for web' }));
    expect(onChange).toHaveBeenCalledWith({ member: 'pin', name: 'web', value: 12, why: 'a quiet machine killed it' }, expect.anything());
  });

  it('takes a pin away', async () => {
    const onChange = draw();
    await userEvent.click(screen.getByRole('button', { name: 'Unpin driver' }));
    expect(onChange).toHaveBeenCalledWith({ member: 'pin', name: 'driver', clear: true }, expect.anything());
  });

  it('says the driver\'s refusal where the press was', async () => {
    const onChange = vi.fn<Press>((_change, answered) => answered.refused('a pin is a whole number of minutes.'));
    draw(VIEW, onChange);
    await userEvent.click(screen.getByRole('button', { name: 'Pin web' }));
    await userEvent.type(screen.getByLabelText('Minutes for web'), '1');
    await userEvent.type(screen.getByLabelText('Why for web'), 'x');
    await userEvent.click(screen.getByRole('button', { name: 'Save pin for web' }));
    expect(await screen.findByText('a pin is a whole number of minutes.')).toBeInTheDocument();
  });

  it('sets the tree baseline, and clears the local inputs', async () => {
    const onChange = draw();
    const baseline = screen.getByLabelText('Tree baseline');
    expect(baseline).toHaveValue('node tools/quick.mjs --record-base');
    await userEvent.clear(baseline);
    await userEvent.type(baseline, 'node base.cjs');
    await userEvent.click(screen.getByRole('button', { name: 'Save tree baseline' }));
    expect(onChange).toHaveBeenCalledWith({ member: 'treeBaseline', value: 'node base.cjs' }, expect.anything());
    await userEvent.click(screen.getByRole('button', { name: 'Clear local inputs' }));
    expect(onChange).toHaveBeenCalledWith({ member: 'localInputs', clear: true }, expect.anything());
  });
});

describe('in Chinese', () => {
  it('reads the same row', async () => {
    await i18n.changeLanguage('zh');
    draw();
    expect(screen.getByText('25 分钟，已固定：killed at 10 on a loaded machine')).toBeInTheDocument();
  });
});
