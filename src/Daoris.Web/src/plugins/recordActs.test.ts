import { beforeEach, describe, expect, it, vi } from 'vitest';

const askCommand = vi.hoisted(() => vi.fn());
vi.mock('./commandAsks', () => ({ askCommand }));

import { recordCommandActs } from './recordActs';

const none = { storage: null, views: [], refused: [], unknown: [] };
const catalog = {
  plugins: [
    {
      id: 'acme-boards', name: 'Acme boards', enabled: true, problem: null, switches: [],
      interfaces: {
        ...none,
        commands: [
          { name: 'open-card', title: 'Open a card', about: 'x', on: ['session', 'quest'], args: [{ name: 'session', type: 'text', title: 'Session', required: true, choices: [] }] },
          { name: 'sync', title: { en: 'Sync', zh: '同步' }, about: 'x', on: ['quest'], args: [] },
          { name: 'login', title: 'Sign in', about: 'x', on: [], args: [] },
        ],
      },
    },
    {
      id: 'off-plugin', name: 'Off', enabled: false, problem: null, switches: [],
      interfaces: { ...none, commands: [{ name: 'a', title: 'A', about: 'x', on: ['session'], args: [] }] },
    },
  ],
};

describe('the acts a plugin adds to a record\'s menu', () => {
  beforeEach(() => askCommand.mockClear());

  it('offers each command whose `on` names the record kind, headed by its plugin, and none of a plugin that is off', () => {
    expect(recordCommandActs(catalog, 'session', 's1', 'en').map((act) => act.label)).toEqual(['Acme boards: Open a card']);
    expect(recordCommandActs(catalog, 'quest', 'q1', 'zh').map((act) => act.label)).toEqual(['Acme boards: Open a card', 'Acme boards: 同步']);
    expect(recordCommandActs(catalog, 'run', 'r1', 'en')).toEqual([]);
  });

  it('opens the command with the record\'s id in the argument named for its kind, where it declares one', () => {
    recordCommandActs(catalog, 'session', 's1', 'en')[0].onSelect?.();
    recordCommandActs(catalog, 'quest', 'q1', 'en')[1].onSelect?.();
    expect(askCommand.mock.calls).toEqual([['acme-boards', 'open-card', { session: 's1' }], ['acme-boards', 'sync', undefined]]);
  });
});
