import type { ReactElement } from 'react';
import { render as rtlRender, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../locales/i18n';
import { NotificationsBell } from './NotificationsBell';

const render = (node: ReactElement) => {
  const drawn = rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);
  return { ...drawn, rerender: (next: ReactElement) => drawn.rerender(<Tooltip.Provider>{next}</Tooltip.Provider>) };
};

// The title bar's *Notifications* (NOTIFY2b, the notification design §2.1, §3.3, Fable's F1.1): a bell counting what needs the person
// in open's hue, and, while any press waits unshown, the presses' own count in the warning hue; its name says which.

describe("the title bar's bell", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it("counts what needs the person in open's hue, and says so by its name", () => {
    render(<NotificationsBell needs={3} presses={1} warn={false} onPress={() => {}} />);

    const bell = screen.getByRole('button', { name: 'Notifications: 3 need you' });
    expect(bell.querySelector('[data-tone]')).toHaveAttribute('data-tone', 'open');
    expect(bell).toHaveTextContent('3');
  });

  it("wears the warning hue with the presses' own count while a press waits unshown", () => {
    render(<NotificationsBell needs={5} presses={2} warn onPress={() => {}} />);

    const bell = screen.getByRole('button', { name: 'Notifications: 2 waiting for your confirmation' });
    expect(bell.querySelector('[data-tone]')).toHaveAttribute('data-tone', 'warn');
    expect(bell).toHaveTextContent('2');
    expect(bell).not.toHaveTextContent('5');
  });

  it('is the bell alone with nothing to count, and is pressed', async () => {
    const onPress = vi.fn();
    render(<NotificationsBell needs={0} presses={0} warn={false} onPress={onPress} />);

    const bell = screen.getByRole('button', { name: 'Notifications' });
    expect(bell.querySelector('[data-tone]')).toBeNull();
    await userEvent.click(bell);
    expect(onPress).toHaveBeenCalledTimes(1);
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    const { rerender } = render(<NotificationsBell needs={3} presses={1} warn={false} onPress={() => {}} />);
    expect(screen.getByRole('button', { name: '通知：3 个等你处理' })).toBeInTheDocument();
    rerender(<NotificationsBell needs={3} presses={1} warn onPress={() => {}} />);
    expect(screen.getByRole('button', { name: '通知：1 个等你确认' })).toBeInTheDocument();
  });
});
