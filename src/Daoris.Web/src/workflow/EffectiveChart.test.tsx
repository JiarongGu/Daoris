import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../locales/i18n';
import { EffectiveChart } from './EffectiveChart';
import {
  EFFECTIVE_CURRENT, EFFECTIVE_LANDED_NONE, EFFECTIVE_RELEASE, EFFECTIVE_UNREAD,
} from './effectiveFixtures';
import type { WorkflowEffective } from './current';

// SETUPUX1o (D221; the Setup design's §2.3): the chart of the chosen route, drawn from the driver's effective graph through the one drawing:
// its label says what is drawn, each step is a title and one fact, and its cell shows on selection. jsdom lays nothing out, so the drawing
// is the rail here, whose nodes are the same list.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

afterEach(async () => { await i18n.changeLanguage('en'); });

const draw = (effective: WorkflowEffective, props: Partial<Parameters<typeof EffectiveChart>[0]> = {}) => render(
  <EffectiveChart
    effective={effective}
    routeLabel={null}
    page="repository"
    name="engine"
    workspace="aurora"
    selected={null}
    onSelect={() => undefined}
    {...props}
  />,
);

const node = (id: string) => document.querySelector<HTMLElement>(`[data-flow-node="${id}"]`)!;

describe("a route's chart", () => {
  it('says what it draws: Current read from the rules, or the saved workflow and the route that follows it', () => {
    const { unmount } = draw(EFFECTIVE_CURRENT);
    expect(screen.getByText('Current, read from the rules as they stand')).toBeInTheDocument();
    unmount();

    draw(EFFECTIVE_RELEASE);
    expect(screen.getByText('Release for engine v2, the saved workflow default work follows')).toBeInTheDocument();
  });

  it('draws the graph in its sections, each step a title and who acts, and its settings not in full on the node', () => {
    draw(EFFECTIVE_RELEASE);

    expect(screen.getByRole('list', { name: 'How work in engine moves' })).toBeInTheDocument();
    expect(within(node('review')).getByText('Your review')).toBeInTheDocument();
    expect(within(node('review')).getByText('you')).toBeInTheDocument();
    // The cell's source and runtime wait for the step to be selected.
    expect(within(node('review')).queryByText('works today')).toBeNull();
    expect(within(node('review')).getByRole('button', { name: 'Its settings' })).toHaveAttribute('aria-pressed', 'false');
  });

  it("shows a step's cell on selection, and takes it away on the same press", async () => {
    const onSelect = vi.fn();
    const { unmount } = draw(EFFECTIVE_CURRENT, { onSelect });

    await userEvent.click(within(node('landing')).getByRole('button', { name: 'Its settings' }));
    expect(onSelect).toHaveBeenLastCalledWith('landing');
    unmount();

    draw(EFFECTIVE_CURRENT, { selected: 'landing', onSelect });
    const cell = screen.getByRole('region', { name: 'The landing: its settings' });
    expect(within(cell).getByText(/Merged into its line\./)).toBeInTheDocument();
    expect(within(cell).getByText('works today')).toBeInTheDocument();
    expect(within(node('landing')).getByRole('button', { name: 'Hide its settings' })).toHaveAttribute('aria-pressed', 'true');
    await userEvent.click(within(node('landing')).getByRole('button', { name: 'Hide its settings' }));
    expect(onSelect).toHaveBeenLastCalledWith(null);
  });

  it('selects a step from the keyboard: Enter on its node', async () => {
    const onSelect = vi.fn();
    draw(EFFECTIVE_CURRENT, { onSelect });

    node('review').focus();
    await userEvent.keyboard('{Enter}');

    expect(onSelect).toHaveBeenCalledWith('review');
  });

  it("opens a Current step's door to the setting that sets it, and a saved version's steps offer none", async () => {
    const setup = vi.fn();
    const current = { ...EFFECTIVE_CURRENT, cells: EFFECTIVE_CURRENT.cells.map((cell) => (cell.node === 'landing'
      ? { ...cell, step: { ...cell.step, source: { rule: 'landing' as const, level: 'repository' as const } } } : cell)) };
    const { unmount } = draw(current, { selected: 'landing', doors: { setup } });

    await userEvent.click(within(screen.getByRole('region', { name: 'The landing: its settings' })).getByRole('button', { name: 'Change in Setup' }));
    expect(setup).toHaveBeenCalledWith('work');
    unmount();

    draw(EFFECTIVE_RELEASE, { selected: 'review', doors: { setup } });
    expect(screen.queryByRole('button', { name: 'Change in Setup' })).toBeNull();
  });

  it('says a limit in the driver\'s words when the page has none of its own', () => {
    const limited = {
      ...EFFECTIVE_CURRENT, limits: { 'newer-limit': 'Partial: the driver says so.' },
      cells: EFFECTIVE_CURRENT.cells.map((cell) => (cell.node === 'review' ? { ...cell, step: { ...cell.step, limit: 'newer-limit' } } : cell)),
    };
    draw(limited, { selected: 'review' });
    expect(within(screen.getByRole('region', { name: 'Your review: its settings' })).getByText('Partial: the driver says so.')).toBeInTheDocument();
  });

  it('draws no chart for landed Current, which is no verification here, and says why', () => {
    draw(EFFECTIVE_LANDED_NONE);
    expect(screen.queryByRole('list', { name: /moves/ })).toBeNull();
    expect(screen.getByText('Landed work follows no verification here, so there is no chart to draw.')).toBeInTheDocument();
  });

  it("says a saved workflow that does not read in the driver's words, and draws nothing", () => {
    draw(EFFECTIVE_UNREAD);
    expect(screen.getByText(/keeps no version that reads here/)).toBeInTheDocument();
    expect(screen.queryByRole('list', { name: /moves/ })).toBeNull();
  });

  it('says its words in Chinese', async () => {
    await i18n.changeLanguage('zh');
    draw(EFFECTIVE_RELEASE);
    expect(screen.getByText(/已保存工作流/)).toBeInTheDocument();
    expect(within(node('review')).getByRole('button', { name: '它的设置' })).toBeInTheDocument();
  });
});
