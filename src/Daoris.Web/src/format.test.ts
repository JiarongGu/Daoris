import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import i18n from './i18n';
import { ago, compact, elapsed, sentence, sittingDays } from './format';

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

  /**
   * An age and a span sit side by side in a rail row, and in 中文 they were set two ways: `9 分钟`
   * beside `1天前`. The catalogue sets a number apart from Chinese almost everywhere, so both do.
   */
  it('speaks the active catalog, setting a number apart from its unit as a span does', async () => {
    await i18n.changeLanguage('zh');
    try {
      expect(ago('2026-09-16T12:00:00Z')).toBe('3 天前');
      expect(ago('2026-09-19T11:15:00Z')).toBe('45 分钟前');
      expect(elapsed('2026-09-19T11:15:00Z')).toBe('45 分钟');
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  it('counts whole sitting days — the number the Overview leads with', () => {
    expect(sittingDays('2026-09-07T00:00:00Z')).toBe(12);
  });
});

/**
 * The fact `ago` cannot carry (D55): a session three minutes old and one three hours deep both read
 * "moved 4m ago". Elapsed is a SPAN, so it says which of the two you are looking at.
 */
describe('elapsed', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-09-19T12:00:00Z'));
  });
  afterEach(() => vi.useRealTimers());

  it('measures to now while a session is still going', () => {
    expect(elapsed('2026-09-19T11:57:00Z')).toBe('3m');
    expect(elapsed('2026-09-19T09:46:00Z')).toBe('2h 14m');
  });

  it('measures to the end once one is given — a finished session has a lifetime, not an age', () => {
    expect(elapsed('2026-09-19T09:00:00Z', '2026-09-19T09:45:00Z')).toBe('45m');
  });

  it('drops to the two largest units, so a long run stays a glanceable width', () => {
    expect(elapsed('2026-09-16T08:00:00Z')).toBe('3d 4h');
  });

  it('says "under a minute" rather than a bare 0m', () => {
    expect(elapsed('2026-09-19T11:59:41Z')).toBe('under a minute');
  });

  /**
   * A record mirrored from a machine whose clock is ahead arrives with a start in this machine's
   * future (D47 §6 — records travel, clocks do not). It reads as brand new, never as a negative
   * span, because "-4m" in a rail is a bug report the person cannot act on.
   */
  it('reads a start in the future as brand new rather than as a negative span', () => {
    expect(elapsed('2026-09-19T12:05:00Z')).toBe('under a minute');
  });

  it('speaks the active catalog', async () => {
    await i18n.changeLanguage('zh');
    expect(elapsed('2026-09-19T09:46:00Z')).toBe('2 小时 14 分');
    await i18n.changeLanguage('en');
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
      parameters: { message: "unknown adapter 'codex' — one of: acp-stub, claude-code, stub" },
    });

    expect(sentence(refusal)).toBe("unknown adapter 'codex' — one of: acp-stub, claude-code, stub");
  });

  /**
   * A page older than the host meets a code it has no entry for. A bare identifier in front of a
   * person is barely better than the generic failure this replaced, so it falls back to a sentence.
   */
  /**
   * REV3 web-rest F7: the bridge framework's own codes had no sentences, so a request that timed out —
   * the driver busy, the host slow — said "something on this machine refused", which it had not.
   */
  it('says what the bridge itself failed at, in its own words for each', () => {
    const said = (code: string) => sentence(Object.assign(new Error('x'), { code, parameters: { module: 'DAORIS.DRIVER', type: 'STOP' } }));
    const refused = sentence(Object.assign(new Error('x'), { code: 'FROM_A_NEWER_HOST' }));

    for (const code of ['TIMEOUT', 'NO_TRANSPORT', 'NO_HANDLER', 'NO_ROUTE', 'MISSING_PAYLOAD_VALUE', 'INVALID_PAYLOAD_VALUE', 'OPERATION_CANCELLED', 'CAPABILITY_NOT_SUPPORTED']) {
      expect(said(code), code).not.toBe(refused);
    }
    expect(said('TIMEOUT')).toMatch(/answer|time/i);
  });

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
