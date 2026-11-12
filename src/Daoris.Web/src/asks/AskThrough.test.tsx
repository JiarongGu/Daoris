import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import type { Ask } from '../data/api';
import { AskThrough } from './AskThrough';

const ASK = {
  id: 'a1', state: 'Open',
  throughWords: [{ words: 'please do without my accept', at: '2026-11-12T09:00:00Z' }],
} as unknown as Ask;

describe('an ask’s through words', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it.each([
    ['en', 'Delivered on your words: “please do without my accept”', 'Take back…', 'Take back your words', 'Take back'],
    ['zh', '按你的原话交付：“please do without my accept”', '收回…', '收回你的原话', '收回'],
  ])('says the standing words and takes them back on the person’s press, in %s', async (language, standing, open, title, meanIt) => {
    await i18n.changeLanguage(language);
    const onTakeBack = vi.fn();
    render(<AskThrough ask={ASK} onTakeBack={onTakeBack} />);

    expect(screen.getByText(standing)).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: open }));
    const group = screen.getByRole('group', { name: title });
    await userEvent.click(within(group).getByRole('button', { name: meanIt }));

    expect(onTakeBack).toHaveBeenCalledTimes(1);
  });

  it('says nothing where the latest words are a take-back', () => {
    const { container } = render(<AskThrough ask={{ ...ASK, throughWords: [...(ASK.throughWords ?? []), { words: null, at: '2026-11-12T10:00:00Z' }] } as Ask} onTakeBack={vi.fn()} />);

    expect(container.textContent).toBe('');
  });
});
