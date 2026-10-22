import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { FRAME_HELD } from '../../design/ui';
import { ZoomReadout } from './ZoomReadout';

// ZOOM1 (the UI/UX design §3.11): the level said as it changes, as a browser's zoom bubble says it, for a moment over the top of
// the content, then gone; the status bar keeps saying it while it is not 100%.

describe("the zoom's readout", () => {
  afterEach(() => vi.useRealTimers());

  it('says nothing until the level changes', () => {
    render(<ZoomReadout level={125} />);
    expect(screen.queryByRole('status')).toBeNull();
  });

  it('says the new level as it changes, held at the frame size, then goes', () => {
    vi.useFakeTimers();
    const { rerender } = render(<ZoomReadout level={100} />);
    rerender(<ZoomReadout level={110} />);

    const said = screen.getByRole('status');
    expect(said).toHaveTextContent('zoom 110%');
    expect(said).toHaveClass(FRAME_HELD);

    // A further step says the next level, and keeps it up for its own moment.
    act(() => { vi.advanceTimersByTime(1000); });
    rerender(<ZoomReadout level={125} />);
    act(() => { vi.advanceTimersByTime(1000); });
    expect(screen.getByRole('status')).toHaveTextContent('zoom 125%');
    act(() => { vi.advanceTimersByTime(1000); });
    expect(screen.queryByRole('status')).toBeNull();
  });

  it('says it back at 100% too, so the reset is heard', () => {
    const { rerender } = render(<ZoomReadout level={150} />);
    rerender(<ZoomReadout level={100} />);
    expect(screen.getByRole('status')).toHaveTextContent('zoom 100%');
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    try {
      const { rerender } = render(<ZoomReadout level={100} />);
      rerender(<ZoomReadout level={90} />);
      expect(screen.getByRole('status')).toHaveTextContent('缩放 90%');
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});
