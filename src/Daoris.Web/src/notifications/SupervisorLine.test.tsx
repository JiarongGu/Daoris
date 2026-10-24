import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

// The supervisor's line in *Notifications* (SUPERVISOR1a-ii, D208; the supervisor design §5, §6.2, §6.4): one per appointment, its
// repository and since when, that its answers are said here and held for the owner's overrule, *dormant* where found as Daoris
// started, and **Turn off** at once. Nothing where none is appointed.

import i18n from '../locales/i18n';
import { moment } from '../locales/format';
import type { AppointmentNow } from '../person/standing';
import { SupervisorLine } from './SupervisorLine';

const SINCE = '2026-10-24T14:02:00Z';
const LIVE: AppointmentNow = {
  role: 'supervisor', repository: 'engine', holder: 'terminal', since: SINCE, dormant: false, answers: 2, scope: 'repository:engine', holderId: 'h1',
  acts: [], setup: false, workspace: '', window: 5,
};

describe("Notifications' supervisor line", () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('says the appointment and turns it off at once', async () => {
    const onTurnOff = vi.fn();
    render(<SupervisorLine appointments={[LIVE]} onTurnOff={onTurnOff} />);
    expect(screen.getByText(`The supervisor answers questions in engine on your behalf, appointed ${moment(SINCE)}: each answer is said here and held 5 minutes for your overrule. 2 answers this run.`))
      .toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Turn off' }));
    expect(onTurnOff).toHaveBeenCalledWith({ repository: 'engine' });
  });

  // SUPERVISOR1e: a workspace's appointment is named as every repository of it, says its own window, and is turned off by its scope.
  it("says a workspace's appointment with its own window and turns it off by its workspace", async () => {
    const onTurnOff = vi.fn();
    const workspace: AppointmentNow = { ...LIVE, repository: '', scope: 'workspace:games', workspace: 'games', window: 1, answers: 1 };
    const { rerender } = render(<SupervisorLine appointments={[LIVE, workspace]} onTurnOff={onTurnOff} />);
    expect(screen.getByText(`The supervisor answers questions in every repository of games on your behalf, appointed ${moment(SINCE)}: each answer is said here and held 1 minute for your overrule. 1 answer this run.`))
      .toBeInTheDocument();
    await userEvent.click(screen.getAllByRole('button', { name: 'Turn off' })[1]);
    expect(onTurnOff).toHaveBeenCalledWith({ workspace: 'games' });

    await i18n.changeLanguage('zh');
    rerender(<SupervisorLine appointments={[workspace]} onTurnOff={onTurnOff} />);
    expect(screen.getByText(/主管代你回答 games 的每个仓库 中的问题.*并保留 1 分钟供你否决/)).toBeInTheDocument();
  });

  it('says one found as Daoris started dormant, and is drawn only where one is appointed', async () => {
    const { rerender } = render(<SupervisorLine appointments={[{ ...LIVE, dormant: true }]} onTurnOff={vi.fn()} />);
    expect(screen.getByText('Supervisor appointment for engine found as Daoris started: press Appoint again in its Setup to use it.'))
      .toBeInTheDocument();
    rerender(<SupervisorLine appointments={[]} onTurnOff={vi.fn()} />);
    expect(screen.queryByRole('region')).toBeNull();
    expect(document.querySelector('[data-supervisor-line]')).toBeNull();

    await i18n.changeLanguage('zh');
    rerender(<SupervisorLine appointments={[LIVE]} onTurnOff={vi.fn()} />);
    expect(screen.getByRole('button', { name: '停用' })).toBeInTheDocument();
  });
});
