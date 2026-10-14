import { afterEach, describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import i18n from '../locales/i18n';
import { AskRow } from './AskRow';
import { PROPOSED } from './fixtures';

// SESSONE2c (D167): an ask's row counts what is unread on its channel, in the reader's language, and says nothing where there is none.

const row = (unread?: number) => render(<ul><AskRow ask={PROPOSED} unread={unread} onOpen={() => {}} /></ul>);

describe('an ask’s row and its channel', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('counts what is unread beside its state', () => {
    row(3);
    expect(screen.getByText('3 unread')).toBeInTheDocument();
  });

  it('says nothing where nothing is unread, or where no driver says', () => {
    const { unmount } = row(0);
    expect(screen.queryByText(/unread/)).toBeNull();
    unmount();
    row(undefined);
    expect(screen.queryByText(/unread/)).toBeNull();
  });

  it('counts in Chinese', async () => {
    await i18n.changeLanguage('zh');
    row(2);
    expect(screen.getByText('2 条未读')).toBeInTheDocument();
  });
});
