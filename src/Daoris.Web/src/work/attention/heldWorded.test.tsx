import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { HeldAttempt, HeldVerification } from '../head/milestone';
import { attentionOf } from './attention';
import { AttentionRow } from './AttentionRow';
import type { PushedAttention } from './attentionPushed';

// The pipeline design §4.3, §4.5: a failed or unknown verification on *What needs you*, said as that verification (its repository, its
// verdict, what it judged), with *Run again* and *Not now*, the check's own words, in each language.

const verification = (over: Partial<HeldVerification> = {}): HeldVerification => ({
  run: 'v-0123456789abcdef', repository: 'engine', commit: '2222222222222222222222222222222222222222', level: 'repository-landed',
  line: 'main', base: '1111111111111111111111111111111111111111', ...over,
});

const held = (over: Partial<HeldAttempt> = {}): HeldAttempt => ({
  unit: 'run:v-0123456789abcdef', node: 'verify', attempt: 1, plugin: 'check', step: 'check', nodeState: 'unknown', why: 'changed',
  at: '2026-11-06T09:00:00Z', said: 'the verification of engine …', words: ['again', 'stop'], verification: verification({ why: 'changed' }), ...over,
});

const failed = held({ nodeState: 'failed', why: 'failure-kept', code: 'gate-failed', words: ['again'], verification: verification({ gate: 'suite' }) });

const row = (attempt: HeldAttempt): PushedAttention => ({
  kind: 'work', id: `${attempt.unit}#${attempt.node}#${attempt.attempt}`, since: '2026-11-06T09:00:00Z', where: attempt.unit, held: attempt,
});

const worded = (attempt: HeldAttempt) => attentionOf([row(attempt)])[0];

describe('a held verification on What needs you', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says a failure by its gate and the batch it judged, which runs again only on the person’s word', () => {
    const item = worded(failed);
    expect(item.title).toBe('The verification of engine failed: suite');
    expect(item.detail).toBe('It judged 2222222 on main, what landed since 1111111. Nothing verifies it again until you run it again.');
  });

  it('says an unknown by why, and that it holds its line until the person runs it again or says not now', () => {
    const item = worded(held());
    expect(item.title).toBe('The verification of engine is not known: gates changed');
    expect(item.detail).toContain('It judged 2222222 on main, what landed since 1111111, and holds its line');
    expect(worded(held({ verification: verification({ base: null, why: 'bound' }) })).detail).toContain('2222222 on main, its first verification');
  });

  it('says a person’s verification by the branch it was asked of, holding no line', () => {
    const item = worded(held({ verification: verification({ level: 'person', line: null, base: null, branch: 'feature/x', why: 'busy' }) }));
    expect(item.title).toBe('The verification of engine is not known: runner busy');
    expect(item.detail).toBe('It judged feature/x at 2222222, and waits until you run it again or say not now.');
  });

  it('offers Run again and Not now, each the check’s own word', async () => {
    const onHeld = vi.fn();
    const item = worded(held());
    render(<ul><AttentionRow item={item} acts={{ held: onHeld }} /></ul>);

    await userEvent.click(screen.getByRole('button', { name: 'Run again' }));
    await userEvent.click(screen.getByRole('button', { name: 'Not now' }));
    expect(onHeld.mock.calls.map((call) => call[1])).toEqual(['again', 'stop']);
  });

  it('offers a failure Run again alone', () => {
    render(<ul><AttentionRow item={worded(failed)} acts={{ held: vi.fn() }} /></ul>);
    expect(screen.getByRole('button', { name: 'Run again' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Not now' })).toBeNull();
  });

  it('says the person’s word once given as any held step does', () => {
    expect(worded(held({ resolution: 'stop' })).title).toBe('verify, attempt 1');
  });

  it('is worded in 中文', async () => {
    await i18n.changeLanguage('zh');
    const item = worded(failed);
    expect(item.title).toBe('engine 的验证失败：suite');
    expect(item.detail).toBe('它验证的是 主线 main 上的 2222222，即自 1111111 以来合入的提交。在你再运行一次之前，不会再验证它。');
    render(<ul><AttentionRow item={worded(held())} acts={{ held: vi.fn() }} /></ul>);
    expect(screen.getByRole('button', { name: '再运行一次' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '暂不处理' })).toBeInTheDocument();
  });
});
