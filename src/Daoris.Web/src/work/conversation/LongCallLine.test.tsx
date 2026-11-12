import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../../locales/i18n';
import { LongCallLine } from './LongCallLine';

// A tool call running past its bound is said on the conversation's line, how long it has run, with *Stop* beside it: a word,
// never a kill.

const minutesAgo = (minutes: number) => new Date(Date.now() - minutes * 60_000).toISOString();

function show(since: string, onStop = vi.fn()) {
  render(<Tooltip.Provider><LongCallLine call={{ title: 'browser_navigate', since }} stopping={false} onStop={onStop} /></Tooltip.Provider>);
  return onStop;
}

describe('LongCallLine', () => {
  afterEach(cleanup);

  it('says a call past two minutes, how long it has run, with Stop beside it', async () => {
    const onStop = show(minutesAgo(5));

    expect(screen.getByRole('status').textContent).toContain('browser_navigate has run 5m');
    await userEvent.click(screen.getByRole('button', { name: 'Stop' }));
    expect(onStop).toHaveBeenCalledOnce();
  });

  it('says nothing of a call within its bound', () => {
    show(minutesAgo(1));

    expect(screen.queryByRole('status')).toBeNull();
  });
});
