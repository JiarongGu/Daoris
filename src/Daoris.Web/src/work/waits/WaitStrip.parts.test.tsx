import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import '../../locales/i18n';
import type { Session } from '../../data/api';
import { JOINT_HOLDING, JOINT_RETURNED, WAITING_PART } from '../../workflow/parts/partFixtures';
import type { WorkflowRun } from '../../workflow/run';
import { WaitStrip } from './WaitStrip';

// A run's working session awaiting its step: the dock says what its run's part waits on and what its joint check holds, from the
// session's run's reading, each with the other part's quest, and counts none of it as a wait on the person.

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: 'q9', repository: 'settings', door: 'claude-code', state: 'awaiting-step', kind: 'driven',
  created: '2026-11-19T08:00:00Z', updated: '2026-11-19T09:30:00Z', ...over,
});

const parts = (runs: readonly WorkflowRun[], onQuest = vi.fn()) => ({ waits: [], steps: [], parts: { runs, onQuest } });

describe('WaitStrip, a quest\'s parts in the dock', () => {
  it('says what the session\'s part waits on, uncounted, with the other part\'s quest', () => {
    const onQuest = vi.fn();
    render(<WaitStrip session={session()} runWaits={parts([WAITING_PART], onQuest)} />);
    const region = screen.getByRole('region', { name: 'What its run waits on' });

    expect(within(region).queryByRole('heading')).toBeNull();
    expect(region).toHaveTextContent(/waits on app’s stage-ended/);
    fireEvent.click(within(region).getByRole('button', { name: 'Open quest #q7' }));
    expect(onQuest).toHaveBeenCalledWith('q7');
  });

  it('draws a joint check\'s parts held and waited for, and the part its failure went back to', () => {
    const { unmount } = render(<WaitStrip session={session({ quest: 'q7', repository: 'engine' })} runWaits={parts([JOINT_HOLDING])} />);
    const holding = screen.getByRole('list', { name: 'Tested together' });
    expect(holding).toHaveTextContent(/holds app’s stage-ended/);
    expect(holding).toHaveTextContent(/waits for config’s config-written/);
    unmount();

    render(<WaitStrip session={session({ quest: 'q7', repository: 'engine' })} runWaits={parts([JOINT_RETURNED])} />);
    expect(screen.getByRole('list', { name: 'Tested together' })).toHaveTextContent(/failed on config, sent back to it/);
  });

  it('counts only the person\'s waits beside them', () => {
    render(
      <WaitStrip
        session={session({ state: 'awaiting-person', note: 'Which stage?' })}
        onResolve={vi.fn()}
        runWaits={parts([WAITING_PART])}
      />,
    );
    const region = screen.getByRole('region', { name: 'Waiting on you' });
    expect(within(region).getByRole('heading', { name: 'Waiting on you' })).toBeInTheDocument();
    expect(region).toHaveTextContent(/waits on app’s stage-ended/);
  });

  it('draws nothing where its run waits on no part', () => {
    render(<WaitStrip session={session()} runWaits={parts([{ ...WAITING_PART, graph: { ...WAITING_PART.graph!, partWaits: null } }])} />);
    expect(screen.queryByRole('region')).toBeNull();
  });
});
