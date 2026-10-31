import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { RelatedSessions, type RelatedRow } from './RelatedSessions';

// LINESTOGETHER1c: the sessions beside one, named by the file they change, each a door to its head.

const row = (over: Partial<RelatedRow> & { id: string }): RelatedRow => ({
  kind: 'driven', quest: null, read: 'read', readAt: null, declaredOverlap: [], changedOverlap: [], ...over,
});

afterEach(async () => { cleanup(); await i18n.changeLanguage('en'); });

describe('the sessions beside one', () => {
  it('names two sessions changing one file, each a door', async () => {
    const onSession = vi.fn();
    render(<RelatedSessions repository="Lyntai" onSession={onSession} related={[
      row({ id: 'aaaa1111', changedOverlap: ['TASKS.md'] }),
      row({ id: 'bbbb2222', kind: 'chat', changedOverlap: ['TASKS.md'] }),
    ]} />);
    expect(screen.getByText(/Beside it in Lyntai/)).toBeTruthy();
    expect(screen.getByText(/2 sessions change/)).toBeTruthy();
    expect(screen.getByText('TASKS.md')).toBeTruthy();
    expect(screen.getAllByRole('button')).toHaveLength(2);
    await userEvent.click(screen.getByRole('button', { name: /bbbb2222/ }));
    expect(onSession).toHaveBeenCalledWith('bbbb2222');
  });

  it('tells a declared overlap apart from a changed one', () => {
    render(<RelatedSessions repository="Lyntai" related={[
      row({ id: 'aaaa1111', declaredOverlap: ['src/a.ts'] }),
      row({ id: 'bbbb2222', changedOverlap: ['src/b.ts'] }),
    ]} />);
    expect(screen.getByText(/1 session declares/)).toBeTruthy();
    expect(screen.getByText('src/a.ts')).toBeTruthy();
    expect(screen.getByText(/1 session changes/)).toBeTruthy();
    expect(screen.getByText('src/b.ts')).toBeTruthy();
  });

  it('says a session not read here', () => {
    render(<RelatedSessions repository="Lyntai" related={[row({ id: 'cccc3333', read: 'not-read', readAt: '2026-10-31T10:00:00Z' })]} />);
    expect(screen.getByText(/cccc3333/)).toBeTruthy();
    expect(screen.getByText(/not read here/)).toBeTruthy();
  });

  it('folds several unread sessions into one line, the earliest last read, each a door', async () => {
    const onSession = vi.fn();
    render(<RelatedSessions repository="Daoris" onSession={onSession} related={[
      row({ id: 'cccc3333', read: 'not-read', readAt: '2026-10-31T10:00:00Z' }),
      row({ id: 'dddd4444', read: 'not-read', readAt: '2026-10-31T09:00:00Z' }),
      row({ id: 'eeee5555', read: 'not-read', readAt: '2026-10-31T11:00:00Z' }),
    ]} />);
    expect(screen.getAllByText(/not read here/)).toHaveLength(1);
    expect(screen.getByText(/3 sessions not read here \(last read /)).toBeTruthy();
    expect(screen.getAllByRole('button')).toHaveLength(3);
    await userEvent.click(screen.getByRole('button', { name: /dddd4444/ }));
    expect(onSession).toHaveBeenCalledWith('dddd4444');
  });

  it('folds without a time where one has no last read', () => {
    render(<RelatedSessions repository="Daoris" related={[
      row({ id: 'cccc3333', read: 'not-read', readAt: '2026-10-31T10:00:00Z' }),
      row({ id: 'dddd4444', read: 'not-read' }),
    ]} />);
    expect(screen.getByText('2 sessions not read here')).toBeTruthy();
  });

  it('folds in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<RelatedSessions repository="Daoris" related={[
      row({ id: 'cccc3333', read: 'not-read' }), row({ id: 'dddd4444', read: 'not-read' }),
    ]} />);
    expect(screen.getByText('2 个会话此处未读取')).toBeTruthy();
  });

  it('says the own tree was not read once, and draws no line for the others', async () => {
    render(<RelatedSessions repository="Daoris" own={{ readAt: '2026-10-31T09:24:00Z' }} related={[]} />);
    expect(screen.getAllByText(/its own tree was not read here/)).toHaveLength(1);
    expect(screen.queryAllByRole('button')).toHaveLength(0);
    cleanup();
    await i18n.changeLanguage('zh');
    render(<RelatedSessions repository="Daoris" own={{ readAt: null }} related={[]} />);
    expect(screen.getByText('它自己的工作树此处未读取')).toBeTruthy();
  });

  it('says nothing where none overlaps', () => {
    const { container } = render(<RelatedSessions repository="Lyntai" related={[]} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('reads in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<RelatedSessions repository="Lyntai" related={[
      row({ id: 'aaaa1111', changedOverlap: ['TASKS.md'] }), row({ id: 'bbbb2222', changedOverlap: ['TASKS.md'] }),
    ]} />);
    expect(screen.queryByText(/Beside it/)).toBeNull();
    expect(screen.getByText(/在 Lyntai 中/)).toBeTruthy();
    expect(screen.getByText(/2 个会话/)).toBeTruthy();
  });

  it('draws no door in a read-only list', () => {
    render(<RelatedSessions repository="Lyntai" related={[row({ id: 'aaaa1111', changedOverlap: ['TASKS.md'] })]} />);
    expect(screen.queryAllByRole('button')).toHaveLength(0);
    expect(screen.getByText(/aaaa1111/)).toBeTruthy();
  });
});
