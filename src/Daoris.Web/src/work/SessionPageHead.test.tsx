import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../i18n';
import type { Session } from '../api';
import { code } from '../test/code';
import { DeleteAsk, SessionPageHead, StopAsk } from './SessionPageHead';

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

  /**
   * UX7c (D152 §7, the UX7 design §5.2): its word leads, so it stays in one place whatever the title's length; then the
   * title on one line, whole in its tip; then its facts, once. Its id is in its ⋯ and its Details, never a line of the head.
   */
  it('leads with its word, then its title on one line whole in its tip, then its facts, and no id', () => {
    head({ facts: ['engine', 'account-1', '2m', 'attempt 4: the 3 before it failed'] });
    const header = screen.getByRole('banner');
    const title = within(header).getByRole('heading', { level: 1, name: 'Expose a streaming budget on the chunk API' });
    expect(title.getAttribute('title')).toBe('Expose a streaming budget on the chunk API');
    expect(title.className).toContain('truncate');
    const word = within(header).getByText('working');
    expect(word.compareDocumentPosition(title) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(within(header).getByText('engine · account-1 · 2m · attempt 4: the 3 before it failed')).toBeInTheDocument();
    expect(within(header).queryByText('s1a2b3c4')).toBeNull();
  });

  it('draws no facts line where it is handed none', () => {
    head();
    expect(screen.getByRole('banner').querySelectorAll('p')).toHaveLength(0);
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
    expect(within(ask).getByText(code('daoris driver retry abc123'))).toBeInTheDocument();
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

/** SESSUX1f (D126 §5.4): *Delete…* sits in the header's ⋯, and its ask says what goes and that nothing brings it back. */
describe('the delete’s ask', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('is offered in the header’s ⋯ after the archive marks and before the id', async () => {
    const act = vi.fn();
    head({ session: session({ quest: null, kind: 'chat', state: 'completed' }), shown: 'completed', acts: ['archive', 'delete', 'copy'], onAct: act });
    const user = userEvent.setup();
    within(screen.getByRole('banner')).getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');

    expect(screen.getAllByRole('menuitem').map((item) => item.textContent)).toEqual(['Archive', 'Delete…', 'Copy session ID']);
    await user.click(screen.getByRole('menuitem', { name: 'Delete…' }));
    expect(act).toHaveBeenCalledWith('delete');
  });

  it('says what goes, and deletes only on its second press, with never mind beside it', async () => {
    const remove = vi.fn();
    const cancel = vi.fn();
    render(<DeleteAsk onDelete={remove} onCancel={cancel} />);

    const ask = screen.getByRole('group', { name: 'delete this session' });
    expect(ask).toHaveTextContent(
      'Deletes this conversation’s record and what this machine kept of it: its words, its transcript and its files. Nothing brings it back.');
    expect(within(ask).getAllByRole('button').map((button) => button.textContent)).toEqual(['Delete session', 'Never mind']);
    await userEvent.click(within(ask).getByRole('button', { name: 'Never mind' }));
    expect(cancel).toHaveBeenCalledOnce();
    await userEvent.click(within(ask).getByRole('button', { name: 'Delete session' }));
    expect(remove).toHaveBeenCalledOnce();
  });

  it('waits while a delete is on its way, and speaks 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<DeleteAsk busy onDelete={() => {}} onCancel={() => {}} />);
    expect(screen.getByRole('button', { name: '确认删除会话' })).toBeDisabled();
    expect(screen.getByRole('group', { name: '删除这个会话' })).toHaveTextContent('无法找回');
  });
});
