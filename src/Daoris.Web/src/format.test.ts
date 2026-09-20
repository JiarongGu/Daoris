import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import i18n from './i18n';
import { ago, compact, sentence, sittingDays } from './format';

describe('compact', () => {
  it('keeps small counts as locale numbers', () => {
    expect(compact(854)).toBe('854');
  });

  it('compacts ten-thousands to K and drops a trailing .0', () => {
    expect(compact(12_900)).toBe('12.9K');
    expect(compact(12_000)).toBe('12K');
  });

  it('compacts millions to M', () => {
    expect(compact(4_200_000)).toBe('4.2M');
  });
});

describe('ago and sittingDays', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-09-19T12:00:00Z'));
  });
  afterEach(() => vi.useRealTimers());

  it('says just now under a minute', () => {
    expect(ago('2026-09-19T11:59:30Z')).toBe('just now');
  });

  it('counts minutes, hours, then days', () => {
    expect(ago('2026-09-19T11:15:00Z')).toBe('45m ago');
    expect(ago('2026-09-19T05:00:00Z')).toBe('7h ago');
    expect(ago('2026-09-16T12:00:00Z')).toBe('3d ago');
  });

  it('speaks the active catalog', async () => {
    await i18n.changeLanguage('zh');
    expect(ago('2026-09-16T12:00:00Z')).toBe('3天前');
    await i18n.changeLanguage('en');
  });

  it('counts whole sitting days — the number the Overview leads with', () => {
    expect(sittingDays('2026-09-07T00:00:00Z')).toBe(12);
  });
});

/**
 * Two transports, two shapes, one rule: the person reads a sentence, never a code.
 *
 * The shell's bridge rejects with a structured `code` + `parameters` and expects the client to produce
 * the text; the HTTP service answers prose that this platform renders verbatim. Before this existed,
 * every bridge refusal reached the person as a generic failure — the module's sentence was dropped by
 * the host and nothing here looked for a code.
 */
describe('sentence', () => {
  it('renders a bridge refusal from its code, interpolating what it carries', () => {
    const refusal = Object.assign(new Error('dev fallback'), {
      code: 'REMOTE_HALF_DECLARED',
      parameters: { workspace: 'aurora' },
    });

    expect(sentence(refusal)).toContain('aurora');
    expect(sentence(refusal)).not.toContain('REMOTE_HALF_DECLARED');
  });

  /** The driver's own sentences are shown word for word, like the service's — never re-authored. */
  it('carries a driver refusal through verbatim', () => {
    const refusal = Object.assign(new Error('fallback'), {
      code: 'DRIVER_REFUSED',
      parameters: { message: "unknown adapter 'codex' — one of: claude-code, stub" },
    });

    expect(sentence(refusal)).toBe("unknown adapter 'codex' — one of: claude-code, stub");
  });

  /**
   * A page older than the host meets a code it has no entry for. A bare identifier in front of a
   * person is barely better than the generic failure this replaced, so it falls back to a sentence.
   */
  it('falls back to a sentence rather than showing a bare code', () => {
    const refusal = Object.assign(new Error('fallback'), { code: 'FROM_A_NEWER_HOST' });

    expect(sentence(refusal)).not.toContain('FROM_A_NEWER_HOST');
    expect(sentence(refusal).length).toBeGreaterThan(20);
  });

  /** An HTTP refusal has no code: the service's own words are the contract, and they pass through. */
  it('leaves a service sentence exactly as the service wrote it', () => {
    expect(sentence(new Error('`engine` already has an active session — `s1a2b3c4`.')))
      .toBe('`engine` already has an active session — `s1a2b3c4`.');
  });

  it('speaks the active catalog', async () => {
    const refusal = Object.assign(new Error('fallback'), { code: 'DRIVER_NOT_READY' });
    await i18n.changeLanguage('zh');
    expect(sentence(refusal)).toContain('驱动');
    await i18n.changeLanguage('en');
  });
});
