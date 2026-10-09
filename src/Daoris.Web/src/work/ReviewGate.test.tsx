import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../i18n';
import { ReviewGate } from './ReviewGate';
import {
  STEP_DEPLOYED, STEP_ELSEWHERE, STEP_NOT_YET, STEP_REVIEWED, STEP_SETTING_UP, STEP_SHOWN, STEP_SHOWN_AGAIN,
} from './reviewFixtures';

const gate = () => screen.getByRole('region', { name: /Review in/ });

/**
 * REVIEWENV1g (D154 points 7–8; the review environment design §3.1–§3.3, §3.6): *Review in `<environment>`* in each state the
 * gate shows, in place of *Accept…*, and each press: *Reviewed* and *Not yet…* name the set-up the view drew, *Not yet* needs
 * the person's words, *Skip…* asks once, *Set it up* publishes a step, and a stale verdict is said in the host's sentence.
 */
describe('the review gate', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says nothing shows the work yet, and offers to set it up there or skip the review', async () => {
    const setUp = vi.fn();
    const user = userEvent.setup();
    render(
      <ReviewGate
        environment="local" state="not-shown"
        proposal={{ choice: 'off', reason: 'Only a README changes.' }}
        acts={{ setUp, skip: () => {} }}
      />,
    );

    expect(gate()).toHaveTextContent('not shown yet');
    expect(gate()).toHaveTextContent('Nothing shows this work in local yet');
    expect(gate()).toHaveTextContent('The intake proposed no review: Only a README changes.');
    await user.click(screen.getByRole('button', { name: 'Set it up in local' }));

    expect(setUp).toHaveBeenCalledWith('local', expect.objectContaining({ done: expect.any(Function) }));
    expect(screen.getByRole('button', { name: 'Setting it up…' })).toBeDisabled();
  });

  it('says a set-up step is showing it, and offers only the skip while it works', () => {
    render(<ReviewGate environment="local" state="being-set-up" step={STEP_SETTING_UP} acts={{ reviewed: () => {}, skip: () => {} }} />);

    expect(gate()).toHaveTextContent('setting up');
    expect(gate()).toHaveTextContent('Set-up step #q2 shows the work in local, then waits for your look.');
    expect(screen.queryByRole('button', { name: 'Reviewed' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Skip the review for this work…' })).toBeInTheDocument();
  });

  it('says what was shown and where, that it is still served, and sends a reviewed naming that set-up', async () => {
    const reviewed = vi.fn();
    const showAgain = vi.fn();
    const user = userEvent.setup();
    render(
      <ReviewGate
        environment="local" state="shown" step={STEP_SHOWN} served
        acts={{ reviewed, notYet: () => {}, skip: () => {}, showAgain }}
      />,
    );

    expect(gate()).toHaveTextContent(/^shown/);
    expect(gate()).toHaveTextContent('Set-up step #q2 showed it at a1b2c3d4.');
    expect(gate()).toHaveTextContent('The report with the compare setting turned on');
    expect(gate()).toHaveTextContent('http://localhost:4200/v3/reports/42');
    // A local address is never a link: a tab opened at it would load the person's own server.
    expect(within(gate()).queryByRole('link')).toBeNull();
    expect(gate()).toHaveTextContent('Still shown in Daoris\'s browser');
    expect(gate()).toHaveTextContent('Said by session s9');

    await user.click(screen.getByRole('button', { name: 'Show it again' }));
    expect(showAgain).toHaveBeenCalled();
    await user.click(screen.getByRole('button', { name: 'Reviewed' }));
    expect(reviewed).toHaveBeenCalledWith({ machine: 'desk', sequence: 7 }, expect.any(Object));
  });

  it("says a stale verdict's refusal in the host's sentence, under the presses", async () => {
    const stale = 'Set-up step `#q2` was shown again since that set-up, at `f0e1d2c3`: review the newest. Nothing was kept.';
    const user = userEvent.setup();
    render(
      <ReviewGate
        environment="local" state="shown" step={STEP_SHOWN}
        acts={{ reviewed: (_setUp, answered) => answered.refused(stale) }}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Reviewed' }));

    expect(screen.getByRole('alert')).toHaveTextContent('was shown again since that set-up');
    expect(screen.getByRole('button', { name: 'Reviewed' })).toBeEnabled();
  });

  it("asks for words before a not yet, and sends them with the set-up the view drew", async () => {
    const notYet = vi.fn();
    const user = userEvent.setup();
    render(<ReviewGate environment="local" state="shown" step={STEP_SHOWN_AGAIN} acts={{ reviewed: () => {}, notYet }} />);

    await user.click(screen.getByRole('button', { name: 'Not yet…' }));
    const ask = screen.getByRole('group', { name: 'Not yet: say what is not right' });
    expect(ask).toHaveTextContent("Your words go to set-up step #q2's session as its next turn");
    const send = within(ask).getByRole('button', { name: 'Send not yet' });
    expect(send).toBeDisabled();
    await user.type(within(ask).getByRole('textbox', { name: 'what is not right yet' }), 'The total is off by one.');
    await user.click(send);

    expect(notYet).toHaveBeenCalledWith({ machine: 'desk', sequence: 9 }, 'The total is off by one.', expect.any(Object));
  });

  it('says that no session hears a not yet on the person\'s own set-up', async () => {
    const user = userEvent.setup();
    const own = { ...STEP_SHOWN, setUps: [{ ...STEP_SHOWN.setUps![0]!, session: null }] };
    render(<ReviewGate environment="local" state="shown" step={own} acts={{ notYet: () => {} }} />);

    await user.click(screen.getByRole('button', { name: 'Not yet…' }));

    expect(screen.getByRole('group')).toHaveTextContent('That set-up was your own, so no session hears your words');
    expect(gate()).toHaveTextContent('Your own set-up');
  });

  it('asks once before a skip, with words where the person gives any', async () => {
    const skip = vi.fn();
    const user = userEvent.setup();
    render(<ReviewGate environment="local" state="shown" step={STEP_SHOWN} acts={{ skip }} />);

    await user.click(screen.getByRole('button', { name: 'Skip the review for this work…' }));
    const ask = screen.getByRole('group', { name: 'Skip the review of this work' });
    await user.type(within(ask).getByRole('textbox'), 'Only the docs changed.');
    await user.click(within(ask).getByRole('button', { name: 'Skip the review' }));

    expect(skip).toHaveBeenCalledWith('Only the docs changed.', expect.any(Object));
  });

  it("says a not yet's words, and waits for the step to show it again", () => {
    render(<ReviewGate environment="local" state="not-yet" step={STEP_NOT_YET} acts={{ reviewed: () => {}, skip: () => {} }} />);

    expect(gate()).toHaveTextContent(/^not yet/);
    expect(gate()).toHaveTextContent('You said not yet to what #q2 showed at a1b2c3d4.');
    expect(gate()).toHaveTextContent('The label still reads the old name.');
    expect(screen.queryByRole('button', { name: 'Reviewed' })).toBeNull();
  });

  it('says what was reviewed does not hold the work added since', () => {
    render(<ReviewGate environment="local" state="not-held" step={STEP_REVIEWED} acts={{ skip: () => {} }} />);

    expect(gate()).toHaveTextContent(/^newer work since/);
    expect(gate()).toHaveTextContent('What you reviewed does not hold these commits: #q2 was reviewed at f0e1d2c3');
    expect(screen.queryByRole('button', { name: 'Show it again' })).toBeNull();
  });

  it("says an unread gate with the driver's own sentence beneath, and offers nothing", () => {
    render(<ReviewGate environment="local" state="unread" said="the service did not answer" acts={{ skip: () => {} }} />);

    expect(gate()).toHaveTextContent('could not be read, so nothing lands until it can be');
    expect(gate()).toHaveTextContent('The driver says: the service did not answer');
    expect(screen.queryByRole('button')).toBeNull();
  });

  it("opens a deployed set-up's address, and offers no Show it again, which Daoris serves only for local", () => {
    render(<ReviewGate environment="dev" state="shown" step={STEP_DEPLOYED} acts={{ reviewed: () => {}, showAgain: () => {} }} />);

    expect(screen.getByRole('link', { name: 'https://dev.example.test/reports/42' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Show it again' })).toBeNull();
  });

  it("says where a teammate's local set-up was shown, and that no address is here", () => {
    render(<ReviewGate environment="local" state="shown" step={STEP_ELSEWHERE} />);

    expect(gate()).toHaveTextContent('local, on laptop, where its tab is');
    expect(gate()).toHaveTextContent('Your own set-up');
  });

  it('lists every set-up newest first on the step\'s own page, each with its verdict', () => {
    render(<ReviewGate environment="local" state="reviewed" step={STEP_REVIEWED} whole />);

    expect(gate()).toHaveTextContent('You reviewed it at f0e1d2c3');
    expect(gate()).toHaveTextContent('Shown before');
    expect(gate()).toHaveTextContent('The label still reads the old name.');
  });

  it('offers no verdict on a set-up named by half its reference, since the host refuses half', () => {
    const half = { ...STEP_SHOWN, setUps: [{ ...STEP_SHOWN.setUps![0]!, sequence: null }] };
    render(<ReviewGate environment="local" state="shown" step={half} acts={{ reviewed: () => {}, notYet: () => {} }} />);

    expect(screen.queryByRole('button', { name: 'Reviewed' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Not yet…' })).toBeNull();
  });

  it('says every state and press in 中文', async () => {
    await i18n.changeLanguage('zh');
    const { unmount } = render(
      <ReviewGate environment="local" state="shown" step={STEP_SHOWN} served={false} acts={{ reviewed: () => {}, notYet: () => {}, skip: () => {}, showAgain: () => {} }} />,
    );

    const region = screen.getByRole('region', { name: /在 local 中审阅/ });
    expect(region).toHaveTextContent('已展示');
    expect(region).toHaveTextContent('搭建步骤 #q2 在 a1b2c3d4 展示了它');
    expect(region).toHaveTextContent('已不再展示');
    for (const name of ['已审阅', '还不行…', '再展示一次', '为这些工作跳过审阅…']) {
      expect(screen.getByRole('button', { name })).toBeInTheDocument();
    }
    unmount();

    for (const [state, word] of [
      ['not-shown', '还没有展示'], ['being-set-up', '正在搭建'], ['not-yet', '还不行'], ['not-held', '有新工作'], ['unread', '未能读取'],
    ] as const) {
      const drawn = render(<ReviewGate environment="local" state={state} step={state === 'not-held' ? STEP_REVIEWED : STEP_NOT_YET} />);
      expect(screen.getByRole('region')).toHaveTextContent(word);
      expect(screen.getByRole('region').textContent).toMatch(/[一-鿿]/);
      drawn.unmount();
    }
  });
});
