import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// An adopted repository's note of the shared doctrine, a molecule over the reconcile's plan as the
// driver answers it: offered where the doctrine tool's status says the canon moved or a keep reopened, its press the person's, confirmed in
// the page; nothing where nothing is offered; a refusal the driver's sentence; in English and in Chinese.

import i18n from '../../locales/i18n';
import { ReconcileNoteView } from './ReconcileNote';
import { type ReconcilePlanned, readReconcilePlan } from './reconcilePlan';

const DIFFERS: ReconcilePlanned = {
  repository: 'engine',
  offered: true,
  status: { synced: true, update: 'differs', carried: '0.0.1', locked: '0.0.1', changed: 2, added: 1, retired: 0, reopened: 1 },
  refusals: [],
  pressable: true,
  title: 'Reconcile this repository with the shared doctrine (2026-11-04)',
};

const BEHIND: ReconcilePlanned = {
  ...DIFFERS, status: { synced: true, update: 'behind', carried: '0.0.2', locked: '0.0.1', changed: 0, added: 0, retired: 1, reopened: 0 },
};

describe("The shared doctrine, on an adopted repository's page", () => {
  beforeEach(() => { i18n.changeLanguage('en'); });
  afterEach(cleanup);

  it('says how the canon moved, the counts a sync would move, and asks the reconcile once confirmed', async () => {
    const onReconcile = vi.fn();
    render(<ReconcileNoteView plan={DIFFERS} onReconcile={onReconcile} />);

    const note = screen.getByRole('region', { name: 'The shared doctrine' });
    expect(note.textContent).toContain('engine synced from another canon at the same version, 0.0.1, so which is newer only the text can say.');
    expect(note.textContent).toContain('2 files to change · 1 file to add · 1 kept file to judge again');

    await userEvent.click(screen.getByRole('button', { name: 'Reconcile…' }));
    expect(onReconcile).not.toHaveBeenCalled();
    expect(screen.getByText(/Asks engine's own session to decide each difference/)).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'Ask its session' }));
    expect(onReconcile).toHaveBeenCalledTimes(1);
  });

  it('says a canon newer than the one synced, with both versions', () => {
    render(<ReconcileNoteView plan={BEHIND} onReconcile={() => {}} />);
    expect(screen.getByText('The canon has moved on since engine synced: 0.0.1 then, 0.0.2 now.')).toBeTruthy();
    expect(screen.getByText('1 file to remove')).toBeTruthy();
  });

  it('draws nothing where nothing is offered, or no plan arrived', () => {
    const { container } = render(<ReconcileNoteView plan={{ ...DIFFERS, offered: false }} onReconcile={() => {}} />);
    expect(container.textContent).toBe('');
    cleanup();
    expect(render(<ReconcileNoteView plan={null} onReconcile={() => {}} />).container.textContent).toBe('');
  });

  it("says a refusal in the driver's sentence and offers no press", () => {
    const open = { code: 'open', sentence: '`engine` already has an open reconcile, `#q7`.' };
    render(<ReconcileNoteView plan={{ ...DIFFERS, pressable: false, refusals: [open] }} onReconcile={() => {}} />);
    expect(screen.getByText(/already has an open reconcile/)).toBeTruthy();
    expect((screen.getByRole('button', { name: 'Reconcile…' }) as HTMLButtonElement).disabled).toBe(true);
  });

  it('reads in Chinese', async () => {
    i18n.changeLanguage('zh');
    render(<ReconcileNoteView plan={DIFFERS} onReconcile={() => {}} />);
    const note = screen.getByRole('region', { name: '共享规范' });
    expect(note.textContent).toContain('engine 同步自同一版本 0.0.1 的另一份典章，哪一份更新只能由文本判断。');
    expect(note.textContent).toContain('2 个文件待改动 · 1 个文件待添加 · 1 个保留的文件需重新判断');
    await userEvent.click(screen.getByRole('button', { name: '与典章对齐…' }));
    expect(screen.getByRole('button', { name: '请它的会话处理' })).toBeTruthy();
  });

  it("reads the answer field by field, a none read as no plan", () => {
    expect(readReconcilePlan({ pending: true })).toBeNull();
    expect(readReconcilePlan({
      repository: 'engine', offered: true, pressable: true, refusals: [{ code: 'open', sentence: 's' }, { code: 1 }],
      status: { synced: true, update: 'sideways', added: 2, changed: -1 },
    })).toEqual({
      repository: 'engine', offered: true, pressable: true, refusals: [{ code: 'open', sentence: 's' }],
      status: { synced: true, changed: 0, added: 2, retired: 0, reopened: 0 },
    });
  });
});
