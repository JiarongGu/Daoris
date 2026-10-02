import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../i18n';
import { RunningIntake } from './RunningIntake';

// A RUNNING intake (INT4h): one turn, framed as one prompt, so it takes no messages — the pipe door
// gives it no stdin, and on the protocol door its stdin is the driver's own frames. What a person
// can do here while it runs is look at the ask it serves; its stop is the page header's (SESSUX1d, D126 §3.3).

const show = (props: Partial<Parameters<typeof RunningIntake>[0]> = {}) =>
  render(<RunningIntake ask="0fda18" onOpen={() => {}} {...props} />);

describe('a running intake', () => {
  it('names the ask it is reading', () => {
    show();
    expect(screen.getByText('This intake is reading ask #0fda18')).toBeInTheDocument();
  });

  /** The one line that replaces a message box: why there is none, and where an answer goes. */
  it('says it takes no messages, and where the answer goes if it asks', () => {
    show();
    expect(screen.getByText(/takes no messages/)).toBeInTheDocument();
    expect(screen.getByText(/the answer is on the ask/)).toBeInTheDocument();
  });

  it('opens the ask it serves', async () => {
    const onOpen = vi.fn();
    show({ onOpen });

    await userEvent.click(screen.getByRole('button', { name: 'Open ask #0fda18' }));
    expect(onOpen).toHaveBeenCalledWith('0fda18');
  });

  /** SESSUX1d (D126 §3.3): the stop and its sentence moved to the page header, the stop's one owner. */
  it('offers no stop of its own: the page header’s stop asks with the sentence it said', () => {
    show();

    expect(screen.queryByRole('button', { name: /^Stop/ })).toBeNull();
    expect(screen.queryByText(/the ask stays a proposal/)).toBeNull();
  });

  /** Where nothing can act (a story, a record mirrored from another machine), it still says why. */
  it('offers no door where nothing can act, and still says it takes no messages', () => {
    render(<RunningIntake ask="0fda18" />);

    expect(screen.queryByRole('button')).toBeNull();
    expect(screen.getByText(/takes no messages/)).toBeInTheDocument();
  });

  it('speaks the active catalog', async () => {
    await i18n.changeLanguage('zh');
    show();
    expect(screen.getByRole('button', { name: '打开需求 #0fda18' })).toBeInTheDocument();
    await i18n.changeLanguage('en');
  });
});
