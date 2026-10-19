import { vi } from 'vitest';

// The bridge the Work frame's suites mock, once for the files that share it (WEBSPEED1 cut `WorkFrame.test.tsx` into
// parts that run beside each other). A part mocks the module with
// `vi.mock('@shenora/react', async () => (await import('./workFrameBridge')).shenoraMock())`: the factory is lazy,
// so this file is loaded after the mock exists and never imports the frame, which would make the two wait on each other.

// Every hook listening to an event hears it, as in the shell: each hook's latest handler is kept,
// told apart by its source, since a render hands over a new closure each time. One handler per
// event name meant a second listener (the console's, CONSOLE3a) silenced the first (the tabs').
// 🔴 Two mounted instances of one hook share a source, so here only the last rendered hears: the frame's own second
// reading of a record (SENDLOST1b's `SentSettler`) is mounted only while words wait on it, never beside every conversation.
const byName = new Map<string, Map<string, (payload: unknown) => void>>();

export const invoke = vi.fn();
export const notifyReady = vi.fn(() => Promise.resolve());
export const eventHandlers = {
  set: (name: string, handler: (payload: unknown) => void) => {
    const all = byName.get(name) ?? new Map<string, (payload: unknown) => void>();
    all.set(handler.toString(), handler);
    byName.set(name, all);
  },
  get: (name: string) => (byName.has(name)
    ? (payload: unknown) => { for (const handler of byName.get(name)!.values()) handler(payload); }
    : undefined),
  has: (name: string) => byName.has(name),
  clear: () => byName.clear(),
};

export async function shenoraMock() {
  // What a press moved reaches the frame's state hub as the shell publishes it (STATEHUB1f, D169), never the press's hook.
  const { publishing } = await import('./publishing');
  const published = publishing(invoke, (type, payload) => eventHandlers.get(`DAORIS.${type}`)?.(payload));
  return {
    isShenoraAvailable: () => true,
    getBridge: () => ({ isAvailable: true, invoke: published, notifyReady }),
    useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
    useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
      eventHandlers.set(`${module}.${type}`, handler);
    },
  };
}
