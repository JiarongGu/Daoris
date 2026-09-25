import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import { ContextRing } from './ContextRing';

const show = (props: Parameters<typeof ContextRing>[0]) =>
  render(<Tooltip.Provider><ContextRing {...props} /></Tooltip.Provider>);

/**
 * CONV5: context used against the window, as the agent last reported it — under the composer, where
 * the reference keeps it. Absent is never zero (TOOL3): a session that reported nothing says so.
 */
describe('the context ring', () => {
  it('shows how full the context is, as a meter a screen reader can read', async () => {
    show({ usage: { used: 34_120, size: 1_000_000, most: 41_000 }, door: 'structured' });

    const meter = screen.getByRole('meter', { name: 'context' });
    expect(meter).toHaveAttribute('aria-valuenow', '34120');
    expect(meter).toHaveAttribute('aria-valuemax', '1000000');
    expect(meter).toHaveTextContent('3%');

    await userEvent.hover(meter);
    expect((await screen.findAllByText(/34,120 of 1,000,000 tokens/))[0]).toBeInTheDocument();
    expect(screen.getAllByText(/41,000/)[0]).toBeInTheDocument();
  });

  it('says under one percent rather than nothing, and warns as the window fills', () => {
    const { unmount } = show({ usage: { used: 4_000, size: 1_000_000, most: 4_000 }, door: 'structured' });
    expect(screen.getByRole('meter')).toHaveTextContent('<1%');
    unmount();

    show({ usage: { used: 170_000, size: 200_000, most: 170_000 }, door: 'structured' });
    expect(screen.getByRole('meter')).toHaveTextContent('85%');
    expect(screen.getByRole('meter')).toHaveAttribute('data-tone', 'warn');
  });

  it('never reads a session that reported nothing as empty', async () => {
    const { unmount } = show({ door: 'structured' });
    const pending = screen.getByRole('img', { name: 'context: not measured' });
    expect(pending).not.toHaveTextContent('0%');
    await userEvent.hover(pending);
    expect((await screen.findAllByText(/Not measured yet/))[0]).toBeInTheDocument();
    unmount();

    show({ door: 'text' });
    await userEvent.hover(screen.getByRole('img', { name: 'context: not measured' }));
    expect((await screen.findAllByText(/carries only text/))[0]).toBeInTheDocument();
  });

  it('speaks the active catalog', async () => {
    await i18n.changeLanguage('zh');
    try {
      show({ usage: { used: 34_120, size: 1_000_000, most: 34_120 }, door: 'structured' });
      expect(screen.getByRole('meter', { name: '上下文' })).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});
