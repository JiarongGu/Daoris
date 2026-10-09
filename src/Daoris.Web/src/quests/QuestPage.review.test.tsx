import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import { STEP_REVIEWED, STEP_SETTING_UP, STEP_SHOWN, STEP_SHOWN_AGAIN, WORK } from '../work/reviewFixtures';
import { QuestPage } from './QuestPage';

// REVIEWENV1g (D154 point 8; the review environment design §3.3): a set-up step's page gains *Review in `<environment>`*,
// every set-up it said newest first, and the verdict's presses under the newest; a done its review holds waits for that,
// never for a yes.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const page = (over: Partial<Parameters<typeof QuestPage>[0]> = {}) => {
  const props = { quest: STEP_SHOWN, onRespond: vi.fn(), onDismiss: vi.fn(), onOpenQuest: vi.fn(), ...over };
  render(<QuestPage {...props} />);
  return props;
};

const review = () => screen.getByRole('region', { name: /Review in local/ });

describe("a set-up step's page", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says it awaits review, never a yes, and offers no accept the review would refuse', () => {
    page({ onAccept: vi.fn() });

    expect(screen.getByText('awaits review')).toBeInTheDocument();
    expect(screen.queryByText('awaits your yes')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Accept the departure' })).toBeNull();
  });

  it('shows what it showed and sends the verdict on the set-up the page drew', async () => {
    const reviewed = vi.fn();
    const user = userEvent.setup();
    page({ review: { acts: { reviewed, notYet: vi.fn(), skip: vi.fn(), showAgain: vi.fn() }, served: true } });

    expect(review()).toHaveTextContent('The report with the compare setting turned on');
    expect(review()).toHaveTextContent("Still shown in Daoris's browser");
    expect(review()).toHaveTextContent('Open http://localhost:4200/v3/reports/42, then turn on Compare');
    await user.click(within(review()).getByRole('button', { name: 'Reviewed' }));

    expect(reviewed).toHaveBeenCalledWith({ machine: 'desk', sequence: 7 }, expect.any(Object));
  });

  it('lists every set-up newest first, the not yet said to the first under it', () => {
    page({ quest: STEP_SHOWN_AGAIN, review: { acts: {} } });

    expect(review()).toHaveTextContent('The same report, with the label renamed as you said.');
    expect(review()).toHaveTextContent('Shown before');
    expect(review()).toHaveTextContent('You said not yet');
    expect(review()).toHaveTextContent('The label still reads the old name.');
  });

  it('says a step still working shows the work next, and offers no verdict yet', () => {
    page({ quest: STEP_SETTING_UP, review: { acts: { reviewed: vi.fn(), skip: vi.fn() } } });

    expect(review()).toHaveTextContent('Set-up step #q2 shows the work in local, then waits for your look.');
    expect(within(review()).queryByRole('button', { name: 'Reviewed' })).toBeNull();
  });

  it('says the review was given once reviewed, with nothing left to press', () => {
    page({ quest: STEP_REVIEWED, review: { acts: { reviewed: vi.fn(), skip: vi.fn() } } });

    expect(review()).toHaveTextContent('You reviewed it at f0e1d2c3');
    expect(within(review()).queryByRole('button')).toBeNull();
  });

  it('shows no review on a quest that is no set-up step', () => {
    page({ quest: WORK });

    expect(screen.queryByRole('region', { name: /Review in/ })).toBeNull();
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    page({ review: { acts: { reviewed: vi.fn(), skip: vi.fn() }, served: true } });

    expect(screen.getByText('等你审阅')).toBeInTheDocument();
    expect(screen.getByRole('region', { name: /在 local 中审阅/ })).toHaveTextContent('仍在 Daoris 浏览器中展示');
    expect(screen.getByRole('button', { name: '已审阅' })).toBeInTheDocument();
  });
});
