import { describe, expect, it, vi } from 'vitest';

// WEBWAIT1: the shared patience in setup.tsx reaches `vi.waitFor`, whose own default is one second.
describe('the page tests\' shared wait patience', () => {
  it('lets vi.waitFor wait past its own one second for a condition that comes late', async () => {
    let ready = false;
    setTimeout(() => { ready = true; }, 1_600);
    await vi.waitFor(() => expect(ready).toBe(true));
  });

  it('keeps a patience a call names', async () => {
    await expect(vi.waitFor(() => { throw new Error('never'); }, { timeout: 50, interval: 10 })).rejects.toThrow('never');
  });
});
