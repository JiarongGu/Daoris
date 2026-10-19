import { useState } from 'react';
import { vi } from 'vitest';
import { render } from '@testing-library/react';
import realUserEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import { useStateHub } from '../bridge/state/stateHub';
import type { OpenGroup } from '../app/opener';
import { WorkFrame } from '../work/WorkFrame';

// What the parts of the Work frame's suite (`WorkFrame.*.test.tsx`) share besides the bridge: the typing, the records the
// frame is shown, and the two ways of mounting it. A part imports this after it has mocked the bridge (`workFrameBridge`).

// WEBSPEED1: user-event's default waits one macrotask between keys and each key re-renders the whole frame, which
// made the suite the slowest in the web unit set. `delay: null` keeps every key event and drops only the waits; a
// fresh instance per direct call keeps the pointer and keyboard state of one call from leaking into the next.
// Only typing and key presses drop the wait: a click with no wait did not open the review tab before its test read it.
export const userEvent = {
  ...realUserEvent,
  type: (...args: Parameters<ReturnType<typeof realUserEvent.setup>['type']>) => realUserEvent.setup({ delay: null }).type(...args),
  keyboard: (...args: Parameters<ReturnType<typeof realUserEvent.setup>['keyboard']>) => realUserEvent.setup({ delay: null }).keyboard(...args),
};

export const DRIVER_STATE = { drivable: ['engine'], holds: [], trees: [], running: ['s1a2b3c4'] };

export const ROSTER = {
  settingsPath: 'C:/somewhere/.daoris/harnesses.json',
  adapter: 'claude-code',
  harnesses: [
    {
      harness: 'claude-code', present: true, version: 'claude 9.9.9', problem: null,
      machineDefault: 'personal',
      profiles: [
        { name: 'personal', home: 'C:/somewhere/.daoris/harnesses/claude-code/personal', login: 'in' },
        { name: 'work', home: 'C:/somewhere/.daoris/harnesses/claude-code/work', login: 'out' },
      ],
    },
    {
      harness: 'codex', present: false, version: null,
      problem: '`codex` is not on this machine\'s PATH', machineDefault: null, profiles: [],
    },
  ],
};

/** A roster whose chat harness reads a structured wire — a door where a turn can be stopped (CONV4). */
export const STRUCTURED_ROSTER = {
  ...ROSTER,
  harnesses: [{ harness: 'stub', present: true, version: 'stub 1.0.0', problem: null, machineDefault: null, profiles: [], structured: true }],
};

export const REGISTRY = [{
  repository: 'engine', adopted: true, registered: true, summary: 'the engine',
  owns: [], accepts: [], packs: [], entries: 1, root: 'C:/somewhere/engine',
}];

export const QUESTS = [{
  id: 'abc123', from: 'game', to: 'engine',
  title: 'Expose a streaming budget', body: 'World streaming needs a per-frame cap.',
  status: 'Open', filed: '2026-09-01T00:00:00Z', updated: '2026-09-01T00:00:00Z',
}];

export const DRIVEN = {
  id: 's1a2b3c4', quest: 'abc123', repository: 'engine', adapter: 'claude-code', state: 'working',
  kind: 'driven', created: '2026-09-02T00:00:00Z', updated: '2026-09-02T01:00:00Z',
};

export const CHAT = {
  id: 'c0ffee11', quest: null, repository: 'engine', adapter: 'stub', state: 'working',
  kind: 'chat', created: '2026-09-02T00:00:00Z', updated: '2026-09-02T00:05:00Z',
};

export const PARKED = {
  ...DRIVEN, id: 'p4rk3d00', state: 'awaiting-person',
  note: 'Two ways forward; I recommend the second.',
};

/** An intake that could not settle whose an ask is, parked asking the person (INT4b). */
export const PARKED_INTAKE = {
  id: 'i9n8t7k6', quest: null, repository: 'ask #0fda18', ask: '0fda18', adapter: 'stub',
  state: 'awaiting-person', kind: 'chat', tree: 'C:/somewhere/data/intake/default',
  note: 'published nothing: it asks you rather than guess.',
  created: '2026-09-02T00:00:00Z', updated: '2026-09-02T00:05:00Z',
};

/** An intake still at work on its ask (INT4h) — one turn, with no message box. */
export const RUNNING_INTAKE = { ...PARKED_INTAKE, id: 'r7n8t7k6', state: 'working', note: undefined };

/** What words on their way say once the page stopped waiting on their send (SENDLOST1b). */
export const UNANSWERED = 'The machine has not answered yet; it may still take them.';
/** The bridge's own end of a call the page stopped waiting on (`stoppedWaiting`). */
export const timedOut = () => Object.assign(new Error('The driver did not answer in time.'), { code: 'TIMEOUT' });

/** A drag's data as a page sees it (DOCK1e): jsdom has no `DataTransfer`, and a region reads its types. */
export function carried() {
  const data: Record<string, string> = {};
  return {
    setData: (type: string, value: string) => { data[type] = value; },
    getData: (type: string) => data[type] ?? '',
    get types() { return Object.keys(data); },
    effectAllowed: 'all',
    dropEffect: 'none',
  };
}

/** The window's state hub, as `ShellSignals` mounts it beside the frame: what a press moved is asked again here. */
export function Hub() {
  useStateHub();
  return null;
}

/** The frame with the application's selection held for it, as `App` holds it. */
export function show(selected: string | null = null, notify: (text: string) => void = () => {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const onSelect = vi.fn();
  const view = render(
    <QueryClientProvider client={client}>
      <Hub />
      <Tooltip.Provider>
        <WorkFrame selected={selected} onSelect={onSelect} notify={notify} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
  return { ...view, onSelect, client };
}

/** The frame on a go's group (ENTRY1g), held as `App` holds it: let go once brought, which `brought` is told. */
export function going(group: OpenGroup) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const brought = vi.fn();
  function Going() {
    const [held, setHeld] = useState<OpenGroup | null>(group);
    const letGo = () => {
      brought();
      setHeld(null);
    };
    return <WorkFrame selected={null} onSelect={vi.fn()} notify={() => {}} group={held} onGroupBrought={letGo} />;
  }
  render(
    <QueryClientProvider client={client}>
      <Hub />
      <Tooltip.Provider>
        <Going />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
  return brought;
}
