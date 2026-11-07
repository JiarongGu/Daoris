import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { Session } from '../../data/api';
import { GitAccessFact, gitAccessRow, type SessionGitAccess, type SessionGitAccessHold } from './GitAccessFact';
import { SessionHead } from './SessionHead';

// A session says the git access it holds now, one fact in its head's Details, with Tighten… beside it that offers only
// a narrowing and sends it at once; the fact reads from the record the shell pushed, never from today's settings alone.

const record = (over: Partial<SessionGitAccess> = {}): SessionGitAccess => ({
  session: 's1a2b3c4', repository: 'engine', workspace: 'shop',
  classes: [
    { class: 'read', value: 'allow', source: 'default' },
    { class: 'write', value: 'ask', source: 'machine' },
    { class: 'dangerous', value: 'deny', source: 'default' },
  ],
  ...over,
});

const session = (): Session => ({
  id: 's1a2b3c4', quest: 'abc123', repository: 'engine', door: 'claude-code', state: 'working', kind: 'driven',
  created: '2026-10-02T09:00:00Z', updated: '2026-10-02T09:30:00Z',
});

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage('en');
});

describe('a session\'s git access', () => {
  it('says what the session holds, naming a class the person tightened', () => {
    render(<GitAccessFact record={record({ classes: [
      { class: 'read', value: 'allow', source: 'default' },
      { class: 'write', value: 'deny', source: 'session' },
      { class: 'dangerous', value: 'deny', source: 'default' },
    ] })} />);

    expect(screen.getByText(/git access: Read allow, Write deny, Dangerous deny · tightened for this session/)).toBeTruthy();
    expect(screen.queryByRole('button', { name: /Tighten/ })).toBeNull();
  });

  it('offers only a narrowing, and sends the one pressed at once with its refusal said under it', async () => {
    const onTighten = vi.fn<NonNullable<SessionGitAccessHold['onTighten']>>((_session, _access, answered) => answered.refused('git: no running session.'));
    render(<GitAccessFact record={record()} onTighten={onTighten} />);

    await userEvent.click(screen.getByRole('button', { name: 'Tighten…' }));
    // Read is allow, so ask and deny are tighter; Write is ask, so only deny; Dangerous is deny, so nothing.
    expect(screen.getAllByRole('button').map((each) => each.textContent)).toEqual(['Read: ask', 'Read: deny', 'Write: deny', 'Cancel']);
    await userEvent.click(screen.getByRole('button', { name: 'Write: deny' }));

    expect(onTighten).toHaveBeenCalledWith('s1a2b3c4', { write: 'deny' }, expect.anything());
    expect(await screen.findByText('git: no running session.')).toBeTruthy();
  });

  it('says its words in Chinese too', async () => {
    await i18n.changeLanguage('zh');
    render(<GitAccessFact record={record()} onTighten={vi.fn()} />);

    expect(screen.getByRole('button', { name: '收紧…' })).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: '收紧…' }));
    expect(screen.getByRole('button', { name: '推送：拒绝' })).toBeTruthy();
    expect(screen.getByRole('button', { name: '取消' })).toBeTruthy();
  });

  it('has a Details row only where the shell pushed a record for that session', () => {
    const t = i18n.t.bind(i18n);
    expect(gitAccessRow(t, { id: 's1a2b3c4' }, { records: [record()] }).value).not.toBeNull();
    expect(gitAccessRow(t, { id: 'other' }, { records: [record()] }).value).toBeNull();
    expect(gitAccessRow(t, { id: 's1a2b3c4' }, null).value).toBeNull();
  });

  it('is a fact of the head\'s Details', async () => {
    render(<SessionHead session={session()} headed gitAccess={{ records: [record()] }} />);
    await userEvent.click(screen.getByRole('button', { name: /Details|详情/ }));

    expect(screen.getByText('Git access', { selector: 'dt' }).nextElementSibling!.textContent).toContain('git access: Read allow, Write ask, Dangerous deny');
  });
});
