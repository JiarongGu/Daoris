import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { RoleCatalogue, RoleChangePress, RoleRow } from '../../data/wiring';
import i18n from '../../locales/i18n';
import type { PlaceRoleEditor } from './PlaceRoles';
import { SecondCheckTurnOn } from './SecondCheckTurnOn';

const SECOND_CHECK: RoleRow = {
  id: 'second-check', state: 'seed', seeded: true, name: { en: 'Second check', zh: '复核' }, takes: [], scopeKind: 'machine', revision: 3,
  choices: [{ agent: 'codex' }, { agent: 'claude-code', model: 'opus', effort: 'high' }],
  hold: { kind: 'supervisor', acts: ['ask-daoris.report'], holder: 'rules', window: 5, decides: ['ask-daoris'] },
};

const catalogue = (row: RoleRow): RoleCatalogue => ({
  roles: [row], works: [], kept: true,
  policies: [{
    role: 'second-check', policyState: 'set', revision: 1,
    staffPolicy: { rule: 'order', choices: [{ agent: 'codex', budget: { starts: 40 } }, { agent: 'claude-code', model: 'opus', effort: 'high', budget: { starts: 40 } }] },
  }],
});

const editorOf = (row: RoleRow, onSet: PlaceRoleEditor['onSet']): PlaceRoleEditor => ({
  agents: [{ value: 'codex', label: 'Codex' }, { value: 'claude-code', label: 'Claude Code' }], offers: {}, staffing: [],
  catalogue: catalogue(row), read: 'ok', busy: false, onSet,
});

/** *Turn on…* for the second check: its card says whom the acts go to and what they spend, and its press holds the seeded role by an agent. */
describe('SecondCheckTurnOn', () => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('opens a card naming the readers in order and the spend, and turns the role on through its route', async () => {
    await i18n.changeLanguage('en');
    const onSet = vi.fn<(press: RoleChangePress, answered: { done: () => void; refused: (sentence: string) => void }) => void>(
      (_, answered) => answered.done(),
    );
    render(<SecondCheckTurnOn editor={editorOf(SECOND_CHECK, onSet)} />);

    await userEvent.click(screen.getByRole('button', { name: 'Turn on…' }));
    expect(screen.getByText(/sent to be read by Codex, Claude Code, in that order/)).toBeTruthy();
    expect(screen.getByText(/at most 40 starts a day for each choice/)).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'Turn on' }));

    expect(onSet).toHaveBeenCalledTimes(1);
    expect(onSet.mock.calls[0]![0]).toEqual({
      change: 'set', id: 'second-check', revision: 3, choices: null,
      role: {
        name: { en: 'Second check', zh: '复核' }, takes: [], scope: { kind: 'machine', id: null },
        filled: 'appointed', kind: 'supervisor', authority: ['ask-daoris.report'], holder: 'agent', window: 5, decides: ['ask-daoris'],
      },
    });
    expect(screen.queryByText(/sent to be read by/)).toBeNull();
  });

  it('says the same card in Chinese, and a refusal under it', async () => {
    await i18n.changeLanguage('zh');
    render(<SecondCheckTurnOn editor={editorOf(SECOND_CHECK, (_, answered) => answered.refused('目录已变'))} />);

    await userEvent.click(screen.getByRole('button', { name: '启用…' }));
    expect(screen.getByText(/依次发送给 Codex, Claude Code 复核/)).toBeTruthy();
    expect(screen.getByText(/每个选择每天最多 40 次启动/)).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: '启用' }));
    expect(screen.getByText('目录已变')).toBeTruthy();
  });

  it('is not offered once an agent reads the acts, nor where the catalogue is not this home\'s', () => {
    const { container } = render(<SecondCheckTurnOn editor={editorOf({ ...SECOND_CHECK, hold: { ...SECOND_CHECK.hold!, holder: 'agent' } }, vi.fn())} />);
    expect(container.textContent).toBe('');
    cleanup();
    const unkept = { ...editorOf(SECOND_CHECK, vi.fn()), catalogue: { ...catalogue(SECOND_CHECK), kept: false } };
    expect(render(<SecondCheckTurnOn editor={unkept} />).container.textContent).toBe('');
  });
});
