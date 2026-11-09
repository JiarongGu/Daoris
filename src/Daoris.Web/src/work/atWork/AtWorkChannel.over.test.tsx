import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { AtWorkView } from './AtWorkView';
import { LANES_ALONGSIDE_AND_UNTAKEN, LINE_SAID } from './atWork.fixtures';

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage('en');
});

describe('a line’s own channel on a session that has ended', () => {
  it('offers no box to write in and no Send: nobody is left to read it', () => {
    render(<AtWorkView held={LANES_ALONGSIDE_AND_UNTAKEN} channel={{ words: LINE_SAID, onPost: vi.fn(), over: true }} />);

    expect(screen.getByText('On the channel')).toBeTruthy();
    expect(screen.queryByRole('textbox')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Send' })).toBeNull();
  });

  it('offers them while the session works', () => {
    render(<AtWorkView held={LANES_ALONGSIDE_AND_UNTAKEN} channel={{ words: LINE_SAID, onPost: vi.fn() }} />);

    expect(screen.getByRole('textbox')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Send' })).toBeTruthy();
  });
});
