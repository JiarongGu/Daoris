import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { ReviewChip, type ReviewShown } from './ReviewChip';

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const SHOWN: ReviewShown = {
  quest: 'q2',
  title: 'Show #q1 in `local` for review',
  environment: 'local',
  look: 'http://localhost:4200/reports',
  shows: 'the new column, turned on',
  served: true,
};

/**
 * REVIEWENV1d (D154 point 8, the review environment design §3.3): beside the browser's door, a chip says a set-up waits for the
 * person's review, what it showed and whether Daoris still serves its tab; *Show it again* serves it again and brings the tab
 * forward. Never inside the browser, which an agent drives. The verdict presses are REVIEWENV1g's.
 */
describe('the review chip', () => {
  it('says nothing while no set-up waits', () => {
    const { container } = render(<ReviewChip reviews={[]} onShowAgain={() => {}} />);

    expect(container).toBeEmptyDOMElement();
    expect(screen.queryByText(/In review/)).toBeNull();
  });

  it('says the review is open, then what it showed, that it is still served, and shows it again on a press', async () => {
    const onShowAgain = vi.fn();
    const user = userEvent.setup();
    render(<ReviewChip reviews={[SHOWN]} onShowAgain={onShowAgain} />);

    const chip = screen.getByRole('button', { name: 'Set-up step #q2 waits for your review: Show #q1 in `local` for review' });
    expect(chip).toHaveTextContent('In review: #q2');
    chip.focus();
    await user.keyboard('{Enter}');

    expect(await screen.findByText('the new column, turned on')).toBeInTheDocument();
    expect(screen.getByText('Shown in Daoris\'s browser for local')).toBeInTheDocument();
    await user.click(screen.getByRole('menuitem', { name: /Show #q2 again/ }));

    expect(onShowAgain).toHaveBeenCalledWith('q2');
  });

  it('says so when its tab is no longer served, since a reload there loads the person\'s own server', async () => {
    const user = userEvent.setup();
    render(<ReviewChip reviews={[{ ...SHOWN, served: false }]} onShowAgain={() => {}} />);

    screen.getByRole('button', { name: /waits for your review/ }).focus();
    await user.keyboard('{Enter}');

    expect(await screen.findByText(/No longer shown: a reload there loads your own server/)).toBeInTheDocument();
  });

  it('counts two, and offers each to show again', async () => {
    const onShowAgain = vi.fn();
    const user = userEvent.setup();
    render(<ReviewChip reviews={[SHOWN, { ...SHOWN, quest: 'q9', title: 'Show #q8 in `local` for review', shows: null }]} onShowAgain={onShowAgain} />);

    const chip = screen.getByRole('button', { name: '2 set-ups wait for your review' });
    expect(chip).toHaveTextContent('In review: 2');
    chip.focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitem', { name: /Show #q9 again/ }));

    expect(onShowAgain).toHaveBeenCalledWith('q9');
  });
});
