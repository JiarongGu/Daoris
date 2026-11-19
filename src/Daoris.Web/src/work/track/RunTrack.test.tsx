import * as Tooltip from '@radix-ui/react-tooltip';
import type { ReactElement } from 'react';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import '../../locales/i18n';
import { COMMANDS } from '../../menus/commands';
import { VIEW_IDS } from '../frame/placements';
import { RunTrack } from './RunTrack';
import { StepRows } from './StepRows';
import { type Passage, rowsWithin, stepRowsOf, trackOf } from './passage';

// A session's page reads its run in two places from one answer, the run's passage on `SESSION_GROUPS`: the head's track, a chip a step
// with its mark, its role where it waits and its attempt where it stands, and the timeline's rows, each step's start and end placed by
// time between turns. A failed step's press is the dock's, never the row's; the RightDock's Workflow view per session is gone.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const at = (minute: number) => `2026-11-19T09:${String(minute).padStart(2, '0')}:00.000Z`;

/** A `pull-request-release` run: its pull request opened, its checks failed once and passed, its merge failed, its release to come. */
const RELEASE: Passage = {
  run: 'q7', workflow: 'pull-request-release', version: 2,
  steps: [
    { node: 'open', kind: 'pull-request/open', mark: 'passed', role: 'operations', attempt: 1, since: at(2) },
    {
      node: 'checks', kind: 'check', mark: 'passed', role: 'tester', attempt: 2, since: at(10),
      earlier: [{ attempt: 1, at: at(5), outcome: 'failed', code: 'web' }],
    },
    { node: 'merge', kind: 'pull-request/merge', mark: 'failed', role: 'operations', attempt: 1, since: at(20), code: 'conflict' },
    { node: 'release', kind: 'pull-request/release', mark: 'next', role: 'release-manager' },
  ],
};

/** The same run standing at its release, held by an agent in the release manager's role. */
const RELEASING: Passage = {
  ...RELEASE,
  steps: [
    ...RELEASE.steps.slice(0, 2),
    { node: 'merge', kind: 'pull-request/merge', mark: 'passed', role: 'operations', attempt: 2, since: at(20) },
    { node: 'release', kind: 'pull-request/release', mark: 'current', role: 'release-manager', attempt: 3, since: at(30) },
  ],
};

const firstSeen = (nodes: readonly string[]) => nodes.filter((node, index) => nodes.indexOf(node) === index);

describe("a run's track and its step rows", () => {
  it('reads a pull-request-release run in the track and the timeline in one order with the same marks', () => {
    const track = trackOf(RELEASE, []);
    const rows = stepRowsOf(RELEASE);

    expect(track.chips.map((chip) => chip.node)).toEqual(['open', 'checks', 'merge', 'release']);
    expect(firstSeen(rows.map((row) => row.node))).toEqual(['open', 'checks', 'merge']);
    // Each step's last row carries the chip's mark: passed, passed, failed; the release has none, being to come.
    for (const chip of track.chips.filter((each) => each.mark !== 'next')) {
      expect(rows.filter((row) => row.node === chip.node).at(-1)?.mark).toBe(chip.mark);
    }
    // The checks' first attempt failed on its gate before its second started; each attempt's end comes before the next one's start.
    expect(rows.filter((row) => row.node === 'checks').map((row) => [row.edge, row.attempt, row.mark, row.code ?? null])).toEqual([
      ['start', 1, 'current', null], ['end', 1, 'failed', 'web'], ['start', 2, 'current', null], ['end', 2, 'passed', null],
    ]);
    expect(track.at).toBe(3);
    expect(track.of).toBe(4);
  });

  it('places each row between the turns whose words came before and after it, a step that ended with no later start at the foot', () => {
    const rows = stepRowsOf(RELEASE);
    expect(rowsWithin(rows, undefined, at(4)).map((row) => `${row.node}:${row.edge}`)).toEqual(['open:start']);
    expect(rowsWithin(rows, at(4), at(12)).map((row) => `${row.node}:${row.edge}`))
      .toEqual(['open:end', 'checks:start', 'checks:end', 'checks:start']);
    expect(rowsWithin(rows, at(12), undefined).map((row) => `${row.node}:${row.edge}`))
      .toEqual(['checks:end', 'merge:start', 'merge:end']);
  });

  it('draws the standing step with its attempt and whom it waits on, and a press on a chip leads to its rows', async () => {
    const onChip = vi.fn();
    render(
      <RunTrack
        track={trackOf(RELEASING, [{ run: 'q7', node: 'release', why: 'actor', role: 'release-manager' }])}
        holderOf={() => 'release-bot (agent)'}
        onChip={onChip}
        onFlowchart={() => {}}
      />,
    );

    const track = screen.getByRole('navigation', { name: 'Its workflow: pull-request-release' });
    const chips = within(track).getAllByRole('button').filter((button) => button.dataset.trackChip);
    expect(chips.map((chip) => [chip.dataset.trackChip, chip.dataset.mark])).toEqual([
      ['open', 'passed'], ['checks', 'passed'], ['merge', 'passed'], ['release', 'current'],
    ]);
    expect(chips[3]).toHaveAccessibleName('release, attempt 3: waiting on Release manager: release-bot (agent)');
    expect(within(track).getByText('4/4')).toBeInTheDocument();

    await userEvent.click(chips[1]!);
    expect(onChip).toHaveBeenCalledWith('checks');
  });

  it("says a failed step's attempt and what failed it, and leads to its press in the dock, never offering one of its own", async () => {
    const onToDock = vi.fn();
    const rows = stepRowsOf(RELEASE).filter((row) => row.node === 'merge');
    render(<StepRows rows={rows} docked={['step:q7/merge']} onToDock={onToDock} />);

    const failed = screen.getByText('merge failed · attempt 1 · conflict').closest('[data-step-row]') as HTMLElement;
    expect(failed.dataset.mark).toBe('failed');
    const buttons = within(failed).getAllByRole('button');
    expect(buttons.map((button) => button.textContent)).toEqual(['To its press']);
    await userEvent.click(buttons[0]!);
    expect(onToDock).toHaveBeenCalledWith('step:q7/merge');
  });

  it("has no Workflow view per session: the run's flowchart opens from the track", () => {
    expect(VIEW_IDS).not.toContain('workflow');
    expect(COMMANDS.some((command) => command.id === 'view.workflow')).toBe(false);
  });
});
