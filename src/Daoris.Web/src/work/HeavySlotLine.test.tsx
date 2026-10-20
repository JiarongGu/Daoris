import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import type { Session } from '../data/api';
import { heavyPartOf, type HeavySlot, readHeavySlot } from './heavySlot';
import { heavyLine, heavyPhases, HeavySlotLine, HeavySlotRow } from './HeavySlotLine';
import { sessionFacts } from './head/headFacts';
import { SessionRow } from './rail/SessionRow';

// HEAVYSLOT1 (the UI/UX design §3.5, D185): a heavy run is seen running and waiting, from the driver's reading of the records its
// runs keep beside the slot (`HEAVY_SLOT`). The fixture is that answer; nothing here reads a run's output.

const AT = Date.now();
const ago = (minutes: number) => new Date(AT - minutes * 60_000).toISOString();
const titleOf = (id: string) => ({ 's-fts': "Fix the FTS index's ranking", 's-chan': "Add the ask's channel" } as Record<string, string>)[id] ?? id;

const RUNNING: HeavySlot = {
  repository: 'engine',
  holder: { session: 's-fts', gates: ['build', 'test:web', 'e2e'], since: ago(9), now: { gate: 'test:web', at: 1, since: ago(6) } },
  waiters: [{ session: 's-chan', since: ago(3) }],
};

const session = (over: Partial<Session> = {}): Session => ({
  id: 's-fts', quest: 'fts1', repository: 'engine', adapter: 'claude-code', state: 'working', kind: 'driven',
  created: ago(40), updated: ago(1), ...over,
});

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage('en');
});

describe('the slot as the driver answers it', () => {
  it('reads the holder at its gate and the waiters, and an older shell or a shape it does not know as nothing', () => {
    const read = readHeavySlot({
      repository: 'engine',
      holder: { session: 's-fts', gates: ['build', 'test:web', 7], since: ago(9), now: { gate: 'test:web', at: 1, since: ago(6) } },
      // A waiter with no session is a run from a terminal; one with no time is no waiter.
      waiters: [{ since: ago(3) }, { session: 's-x' }],
    });
    expect(read?.holder?.gates).toEqual(['build', 'test:web']);
    expect(read?.holder?.now).toEqual({ gate: 'test:web', at: 1, since: ago(6) });
    expect(read?.waiters).toEqual([{ session: null, since: ago(3) }]);

    expect(readHeavySlot(undefined)).toBeNull();
    expect(readHeavySlot({ marks: true })).toBeNull();
    // The bridge leaves a null out: no holder is no heavy run now.
    expect(readHeavySlot({ repository: 'engine', waiters: [] })).toEqual({ repository: 'engine', holder: null, waiters: [], refusal: null });
  });

  it('gives each session its part: the holder at its gate, a failed gate, a waiter behind the holder or a terminal, and none', () => {
    expect(heavyPartOf(RUNNING, 's-fts')).toEqual({ kind: 'running', n: 2, total: 3, gate: 'test:web', since: ago(6) });
    expect(heavyPartOf(RUNNING, 's-chan')).toEqual({ kind: 'waiting', behind: 's-fts', since: ago(3) });
    expect(heavyPartOf(RUNNING, 's-other')).toBeNull();
    expect(heavyPartOf(null, 's-fts')).toBeNull();

    const failed = { ...RUNNING, holder: { ...RUNNING.holder!, failed: { gate: 'test:web', exit: 1 } } };
    expect(heavyPartOf(failed, 's-fts')).toEqual({ kind: 'failed', gate: 'test:web' });

    const terminal = { ...RUNNING, holder: { ...RUNNING.holder!, session: null } };
    expect(heavyPartOf(terminal, 's-chan')).toEqual({ kind: 'waiting', behind: null, since: ago(3) });

    // Taken, and its first gate not started yet: at its first, from when it took the slot.
    const taken = { ...RUNNING, holder: { ...RUNNING.holder!, now: null } };
    expect(heavyPartOf(taken, 's-fts')).toEqual({ kind: 'running', n: 1, total: 3, gate: 'build', since: ago(9) });
  });
});

describe('the line', () => {
  it('says the gate running, whom it waits behind, and the gate that failed, in English and 中文', async () => {
    const t = i18n.t.bind(i18n);
    expect(heavyLine(t, heavyPartOf(RUNNING, 's-fts')!, titleOf)).toBe('heavy gates · 2 of 3 · test:web · 6m');
    expect(heavyLine(t, heavyPartOf(RUNNING, 's-chan')!, titleOf))
      .toBe("waiting for the heavy run · behind Fix the FTS index's ranking · 3m");
    expect(heavyLine(t, { kind: 'waiting', behind: null, since: ago(3) }, titleOf))
      .toBe('waiting for the heavy run · behind a run from a terminal · 3m');
    expect(heavyLine(t, { kind: 'failed', gate: 'test:web' }, titleOf)).toBe('heavy gates failed at test:web');

    await i18n.changeLanguage('zh');
    expect(heavyLine(i18n.t.bind(i18n), heavyPartOf(RUNNING, 's-fts')!, titleOf)).toBe('重型检查 · 第 2 项，共 3 项 · test:web · 6 分钟');
  });

  it('opens the holder from a waiter\'s line, and a run from a terminal is no door', async () => {
    const onOpen = vi.fn();
    render(<HeavySlotLine part={heavyPartOf(RUNNING, 's-chan')!} titleOf={titleOf} onOpen={onOpen} />);
    await userEvent.click(screen.getByRole('button', { name: "Fix the FTS index's ranking" }));
    expect(onOpen).toHaveBeenCalledWith('s-fts');
    cleanup();

    render(<HeavySlotLine part={{ kind: 'waiting', behind: null, since: ago(3) }} titleOf={titleOf} onOpen={onOpen} />);
    expect(screen.getByText('a run from a terminal')).toBeTruthy();
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('draws the holder\'s gates as phases: done, now, to come; a failed one with its exit', () => {
    const t = i18n.t.bind(i18n);
    expect(heavyPhases(t, RUNNING.holder!).map((phase) => phase.state)).toEqual(['done', 'now', 'to-come']);
    const failed = heavyPhases(t, { ...RUNNING.holder!, failed: { gate: 'test:web', exit: 3 } });
    expect(failed[1]).toMatchObject({ state: 'failed', sentence: 'test:web failed with exit 3.' });
  });
});

describe('where it is said', () => {
  it('the rail\'s line says the heavy run in place of the call it is inside', () => {
    render(
      <ul>
        <SessionRow
          session={session()}
          heavy={heavyPartOf(RUNNING, 's-fts')}
          grouping={{ session: 's-fts', group: 'working', calling: { title: 'daoris-driver run-heavy', since: ago(9) } } as never}
        />
      </ul>,
    );
    expect(screen.getByText(/heavy gates · 2 of 3 · test:web/)).toBeTruthy();
    expect(screen.queryByText(/running daoris-driver run-heavy/)).toBeNull();
  });

  it('the head\'s facts say it in place of the call, and a held permission still comes first', () => {
    const t = i18n.t.bind(i18n);
    const heavy = heavyLine(t, heavyPartOf(RUNNING, 's-chan')!, titleOf);
    const facts = sessionFacts(t, { session: session({ id: 's-chan' }), shown: 'working', heavy, calling: { title: 'daoris-driver run-heavy', since: ago(3) } });
    expect(facts).toContain(heavy);
    expect(facts.some((fact) => fact.includes('running daoris-driver'))).toBe(false);
  });

  it('the repository\'s row says who holds it at which gate and how many wait, each a door, or no heavy run now', async () => {
    const onOpen = vi.fn();
    render(<HeavySlotRow slot={RUNNING} titleOf={titleOf} onOpen={onOpen} />);
    expect(screen.getByText('2 of 3 · test:web · 6m')).toBeTruthy();
    expect(screen.getByText('1 waits:')).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: "Add the ask's channel" }));
    expect(onOpen).toHaveBeenCalledWith('s-chan');
    cleanup();

    render(<HeavySlotRow slot={{ repository: 'engine', waiters: [] }} titleOf={titleOf} />);
    expect(screen.getByText('No heavy run now')).toBeTruthy();
    cleanup();

    // Held by a run from a terminal: said, with no door.
    render(<HeavySlotRow slot={{ ...RUNNING, holder: { ...RUNNING.holder!, session: null }, waiters: [] }} titleOf={titleOf} onOpen={onOpen} />);
    expect(screen.getByText('a run from a terminal')).toBeTruthy();
    expect(screen.queryByRole('button')).toBeNull();
  });
});
