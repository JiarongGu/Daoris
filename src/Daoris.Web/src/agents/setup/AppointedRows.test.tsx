import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { RoleCatalogue, RoleChangePress, RoleRow } from '../../data/wiring';
import i18n from '../../locales/i18n';
import { appointedOf } from '../roles/appointedHold';
import { AppointedRows } from './AppointedRows';
import type { PlaceRoleEditor } from './PlaceRoles';

// The supervisor and the steward are catalogue rows with a `hold`; their rows show holder and acts, and the one role editor sets
// them over SET_ROLE.

const SUPERVISOR: RoleRow = {
  id: 'supervisor', state: 'seed', seeded: true, name: { en: 'Supervisor', zh: '主管' }, takes: [], scopeKind: 'machine', revision: 0,
  hold: { kind: 'supervisor', acts: ['answer', 'defer'], holder: 'rules', window: 5 },
};
const STEWARD: RoleRow = {
  id: 'steward', state: 'seed', seeded: true, name: { en: 'Steward', zh: '管家' }, takes: [], scopeKind: 'machine', revision: 0,
  hold: { kind: 'steward', acts: ['drive', 'stop'], holder: null },
};
const CATALOGUE: RoleCatalogue = { roles: [SUPERVISOR, STEWARD], works: ['build'], kept: true };

const draw = (onSet = vi.fn()) => {
  const editor: PlaceRoleEditor = { agents: [], offers: {}, staffing: [], catalogue: CATALOGUE, read: 'ok', busy: false, onSet };
  render(<AppointedRows placed={(scope) => scope.kind === 'machine'} editor={editor} />);
  return onSet;
};

describe('The appointed roles of a place', () => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('shows each role with its holder and acts, and sets them over SET_ROLE', async () => {
    const onSet = draw();

    expect(screen.getByText('Held by your rules · answer a question and leave a question to you · an answer is held 5 minutes for your overrule')).toBeTruthy();
    expect(screen.getByText(/^Held by no one · drive and stop$/)).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: /Steward/ }));
    // The holder is the platform's select: opened from the keyboard, its option pressed.
    screen.getByRole('combobox', { name: 'Held by' }).focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('option', { name: 'a terminal' }));
    fireEvent.click(screen.getByRole('checkbox', { name: 'resume' }));
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));

    const [press] = onSet.mock.calls[0] as [RoleChangePress];
    expect(press).toMatchObject({
      change: 'set', id: 'steward', role: { takes: [], filled: 'appointed', authority: ['drive', 'stop', 'resume'], holder: 'terminal' },
    });
    expect(press.choices).toBeUndefined();
    expect(press.role).not.toHaveProperty('window');
  });

  it('speaks 中文', async () => {
    await i18n.changeLanguage('zh');
    draw();

    expect(screen.getByText(/^由你的规则担任 · 回答问题.*答复会保留 5 分钟，供你否决/)).toBeTruthy();
    expect(screen.getByText(/^由无人担任 · 驱动.*停止/)).toBeTruthy();
  });

  it('draws the diagram\'s appointed half from the catalogue: a role not held is dashed', () => {
    expect(appointedOf(CATALOGUE)).toMatchObject({
      stewardOffered: true, supervisorOffered: true,
      appointments: [{ role: 'supervisor', scope: 'machine', dormant: false }, { role: 'steward', scope: 'machine', dormant: true }],
    });
  });
});
