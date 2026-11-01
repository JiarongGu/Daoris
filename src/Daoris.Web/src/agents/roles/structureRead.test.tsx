import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';

// AGENTSPLACE1a: the read behind the Roles mode is scoped: a chosen place asks its chain alone, and no read asks a repository that is
// not driven.

const { whole, chain, driver } = vi.hoisted(() => ({
  whole: vi.fn(), chain: vi.fn(), driver: vi.fn(),
}));

vi.mock('../../bridge/driver', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../bridge/driver')>()),
  useDriver: () => driver(),
  useRoleStructure: (...args: unknown[]) => whole(...args),
  useRoleStructureChain: (...args: unknown[]) => chain(...args),
}));
vi.mock('../../data/queries', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../data/queries')>()),
  useRegistry: () => ({ data: [{ repository: 'engine', workspace: 'aurora' }, { repository: 'old-import', workspace: 'aurora' }, { repository: 'site', workspace: 'borealis' }] }),
}));

import { useStructureRead } from './structureRead';

const idle = { data: undefined, isError: false, error: null };

describe('useStructureRead', () => {
  beforeEach(() => {
    whole.mockReset().mockReturnValue(idle);
    chain.mockReset().mockReturnValue(idle);
    driver.mockReset().mockReturnValue({ data: { drivable: ['engine', 'site'] } });
  });

  it('asks this machine, each workspace and the driven repositories, and not an imported one', () => {
    renderHook(() => useStructureRead(true));

    expect(whole).toHaveBeenLastCalledWith(
      [{}, { workspace: 'aurora' }, { workspace: 'borealis' }, { workspace: 'aurora', repository: 'engine' }, { workspace: 'borealis', repository: 'site' }],
      undefined, { enabled: true },
    );
    expect(chain).toHaveBeenLastCalledWith({}, undefined, { enabled: false });
  });

  it('asks the chosen place\'s chain alone, and nothing of the whole', () => {
    chain.mockReturnValue({ data: { works: [], roles: [], places: [], agents: [] }, isError: false, error: null });

    const { result } = renderHook(() => useStructureRead(true, { workspace: 'aurora', repository: 'engine' }));

    expect(chain).toHaveBeenLastCalledWith({ workspace: 'aurora', repository: 'engine' }, undefined, { enabled: true });
    expect(whole).toHaveBeenLastCalledWith(expect.anything(), undefined, { enabled: false });
    expect(result.current.read).toBe('ok');
  });

  it('waits for the driver to say what it drives before it asks the whole', () => {
    driver.mockReturnValue({ data: undefined });

    const { result } = renderHook(() => useStructureRead(true));

    expect(whole).toHaveBeenLastCalledWith(expect.anything(), undefined, { enabled: false });
    expect(result.current.read).toBe('loading');
  });
});
