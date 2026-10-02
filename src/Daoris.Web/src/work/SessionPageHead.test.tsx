import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../i18n';
import type { Session } from '../api';
import { SessionPageHead, StopAsk } from './SessionPageHead';

// The session's page header (SESSUX1d, D126 §3.2) and its stop's ask (§3.3), as molecules: every state is reached by
// passing it. Which acts apply is the rule's (`acts.test.ts`); this holds how the header draws what it is handed.

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: 'abc123', repository: 'engine', adapter: 'claude-code', state: 'working', kind: 'driven',
  created: '2026-10-02T09:00:00Z', updated: '2026-10-02T09:30:00Z', ...over,
});

const head = (props: Partial<Parameters<typeof SessionPageHead>[0]> = {}) => render(
  <SessionPageHead
    session={session()} title="Expose a streaming budget on the chunk API" shown="working"
    acts={['stop', 'review', 'openFolder', 'terminal', 'detach', 'copy']} onAct={() => {}} {...props}
  />,
);

describe('a session’s page header', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('names the session on one line, whole in its tip, with its word and its id', () => {
    head();
    const header = screen.getByRole('banner');
    const title = within(header).getByRole('heading', { level: 1, name: 'Expose a streaming budget on the chunk API' });
    expect(title.getAttribute('title')).toBe('Expose a streaming budget on the chunk API');
    expect(title.className).toContain('truncate');
    expect(within(header).getByText('working')).toBeInTheDocument();
    expect(within(header).getByText('s1a2b3c4')).toBeInTheDocument();
  });

  /** §3.2: the primary act where the state has one, then *Stop…* while live, then ⋯ with the rest in §3.1's order. */
  it('leads with the primary act, then Stop…, and holds the rest in its ⋯ in order', async () => {
    const act = vi.fn();
    head({ acts: ['stop', 'retry', 'review', 'openFolder', 'terminal', 'detach', 'archive', 'copy'], primary: 'retry', onAct: act });
    const header = screen.getByRole('banner');

    const buttons = within(header).getAllByRole('button').map((button) => button.textContent || button.getAttribute('aria-label'));
    expect(buttons).toEqual(['Try again', 'Stop…', 'More actions']);
    await userEvent.click(within(header).getByRole('button', { name: 'Try again' }));
    expect(act).toHaveBeenCalledWith('retry');

    const user = userEvent.setup();
    within(header).getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    expect(screen.getAllByRole('menuitem').map((item) => item.textContent))
      .toEqual(['Review', 'Open folder', 'Open a terminal here', 'Open in its own window', 'Archive', 'Copy session ID']);
    await user.click(screen.getByRole('menuitem', { name: 'Open a terminal here' }));
    expect(act).toHaveBeenCalledWith('terminal');
  });

  it('asks for its stop rather than stopping on the first press', async () => {
    const act = vi.fn();
    head({ onAct: act });
    await userEvent.click(screen.getByRole('button', { name: 'Stop…' }));
    expect(act).toHaveBeenCalledWith('stop');
  });

  /** A teammate's record is read here, and offered only what reaches no process: its ⋯ alone, no Stop… and no primary. */
  it('offers a record with nothing but its id its ⋯ alone', async () => {
    head({ session: session({ id: 'person@machine-b/s1a2b3c4' }), acts: ['copy'] });
    const header = screen.getByRole('banner');
    expect(within(header).getAllByRole('button').map((button) => button.getAttribute('aria-label'))).toEqual(['More actions']);
  });

  it('draws no ⋯ where there is nothing to put in it, and no act at all where nothing can act', () => {
    const { rerender } = head({ acts: ['stop'] });
    expect(within(screen.getByRole('banner')).getAllByRole('button').map((button) => button.textContent)).toEqual(['Stop…']);
    rerender(<SessionPageHead session={session()} title="Expose a streaming budget" shown="working" acts={[]} />);
    expect(within(screen.getByRole('banner')).queryAllByRole('button')).toHaveLength(0);
  });

  /** It is pinned: the page scrolls under it, and below 560 px its acts take their own line (§3.2). */
  it('is pinned at the top of the main area, and puts its acts on their own line when the area is narrow', () => {
    head();
    const header = screen.getByRole('banner');
    expect(header.className).toContain('sticky');
    expect(within(header).getByRole('group', { name: 'Session actions' }).className).toContain('@max-[35rem]/main:basis-full');
  });

  it('holds what asks under it, pinned with it', () => {
    head({ asking: <p>the stop’s ask</p> });
    expect(within(screen.getByRole('banner')).getByText('the stop’s ask')).toBeInTheDocument();
  });

  it('names its acts in 中文', async () => {
    await i18n.changeLanguage('zh');
    head({ acts: ['stop', 'retry', 'copy'], primary: 'retry' });
    expect(within(screen.getByRole('banner')).getAllByRole('button').map((button) => button.textContent || button.getAttribute('aria-label')))
      .toEqual(['重试', '停止…', '更多操作']);
  });
});

describe('the stop’s ask', () => {
  it('says what follows, and stops only on its second press, with never mind beside it', async () => {
    const stop = vi.fn();
    const cancel = vi.fn();
    render(<StopAsk sentence="Stops the session now. Run `daoris driver retry abc123` later." onStop={stop} onCancel={cancel} />);

    const ask = screen.getByRole('group', { name: 'stop this session' });
    expect(within(ask).getByText('daoris driver retry abc123').tagName).toBe('CODE');
    expect(within(ask).getAllByRole('button').map((button) => button.textContent)).toEqual(['Stop session', 'Never mind']);
    await userEvent.click(within(ask).getByRole('button', { name: 'Never mind' }));
    expect(cancel).toHaveBeenCalledOnce();
    await userEvent.click(within(ask).getByRole('button', { name: 'Stop session' }));
    expect(stop).toHaveBeenCalledOnce();
  });

  it('waits while a stop is on its way', () => {
    render(<StopAsk sentence="Stops it." busy onStop={() => {}} onCancel={() => {}} />);
    expect(screen.getByRole('button', { name: 'Stop session' })).toBeDisabled();
  });
});
