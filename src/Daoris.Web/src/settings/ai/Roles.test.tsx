import { fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { RoleCatalogue, RoleRow } from '../../data/wiring';
import i18n from '../../locales/i18n';
import { RolesCard } from './Roles';

// FOUNDATION1y-i (the foundation design §2.7): the role catalogue on AI features, where the person adds, changes and removes a working
// role (its names, work, place and choices), gives a seeded one back its seed, and takes the Designer Daoris suggests. Every press goes
// out as `SET_ROLE` takes it; the page judges nothing, and a refusal is the driver's sentence under Save.

const seed = (id: string, en: string, zh: string, work: string): RoleRow => ({
  id, state: 'seed', seeded: true, nameEn: en, nameZh: zh, takes: [work], scopeKind: 'machine', revision: 0,
});

const CATALOGUE: RoleCatalogue = {
  roles: [
    seed('build', 'Build', '开发', 'build'),
    { id: 'intake', state: 'removed', seeded: true, takes: [], revision: 2 },
    {
      id: 'night', state: 'set', seeded: false, nameEn: 'Night shift', nameZh: '夜班', takes: ['build'], scopeKind: 'workspace', scopeId: 'studio',
      revision: 3, choices: [{ agent: 'codex-acp', model: 'gpt-5', effort: 'high' }],
    },
  ],
  works: ['build', 'design', 'chat', 'ask-daoris', 'intake', 'help', 'partner', 'review', 'brainstorm'],
  kept: true,
  suggested: [{ id: 'designer', nameEn: 'Designer', nameZh: '设计师', takes: ['design'], scopeKind: 'machine', choices: [{ agent: 'claude-code', model: 'opus', effort: 'high' }] }],
};

const AGENTS = [{ value: 'claude-code', label: 'Claude Code' }, { value: 'codex-acp', label: 'Codex' }];

const card = (onSet = vi.fn(), catalogue = CATALOGUE) => {
  render(<RolesCard catalogue={catalogue} agents={AGENTS} onSet={onSet} />);
  return onSet;
};

describe('The role catalogue', () => {
  afterEach(() => i18n.changeLanguage('en'));

  it('says each role with its work and place, a removed one waiting, and an added one with its choices', () => {
    card();

    expect(screen.getByText('Night shift')).toBeTruthy();
    expect(screen.getByText(/building · in workspace studio · codex-acp · gpt-5 · high/)).toBeTruthy();
    expect(screen.getByText(/Removed: its work waits until a role takes it/)).toBeTruthy();
  });

  it('adds a role from its editor, every field going out as SET_ROLE takes it', () => {
    const onSet = card();
    fireEvent.click(screen.getByRole('button', { name: 'Add a role' }));
    const form = within(screen.getByRole('form', { name: 'A new role' }));

    fireEvent.change(form.getByLabelText('Id'), { target: { value: 'reviewer-2' } });
    fireEvent.change(form.getByLabelText('Name in English'), { target: { value: 'Night review' } });
    fireEvent.change(form.getByLabelText('Name in Chinese'), { target: { value: '夜审' } });
    fireEvent.change(form.getByLabelText("choice 1's model"), { target: { value: 'opus' } });
    fireEvent.click(form.getByRole('button', { name: 'Save' }));

    expect(onSet).toHaveBeenCalledWith({
      change: 'set', id: 'reviewer-2', revision: null,
      role: { name: { en: 'Night review', zh: '夜审' }, takes: ['build'], scope: { kind: 'machine', id: null } },
      choices: [{ agent: 'claude-code', model: 'opus', effort: null }],
    }, expect.anything());
  });

  it('removes an added role, gives a seeded one back its seed, and takes the suggested Designer, each by the revision it read', () => {
    const onSet = card();

    fireEvent.click(screen.getByRole('button', { name: 'Remove Night shift' }));
    fireEvent.click(screen.getByRole('button', { name: 'Back to the default: Intake' }));
    fireEvent.click(screen.getByRole('button', { name: 'Add the Designer' }));

    expect(onSet).toHaveBeenNthCalledWith(1, { change: 'remove', id: 'night', revision: 3 }, expect.anything());
    expect(onSet).toHaveBeenNthCalledWith(2, { change: 'default', id: 'intake', revision: 2 }, expect.anything());
    expect(onSet).toHaveBeenNthCalledWith(3, {
      change: 'set', id: 'designer', revision: 0,
      role: { name: { en: 'Designer', zh: '设计师' }, takes: ['design'], scope: { kind: 'machine', id: null } },
      choices: [{ agent: 'claude-code', model: 'opus', effort: 'high' }],
    }, expect.anything());
    expect(screen.getByText(/can spend more/)).toBeTruthy();
  });

  it("says the driver's refusal under Save, the editor still open", () => {
    card(vi.fn((_press, answered) => answered.refused('`night` already takes `build` in that scope.')));
    fireEvent.click(screen.getByRole('button', { name: 'Edit Night shift' }));
    const form = within(screen.getByRole('form', { name: 'Night shift' }));
    fireEvent.click(form.getByRole('button', { name: 'Save' }));

    expect(form.getByText(/in that scope/)).toBeTruthy();
    expect(screen.getByRole('form', { name: 'Night shift' })).toBeTruthy();
  });

  it('speaks 中文, a role named in Chinese as it was written', async () => {
    await i18n.changeLanguage('zh');
    card();

    expect(screen.getByText('夜班')).toBeTruthy();
    expect(screen.getByRole('button', { name: '添加角色' })).toBeTruthy();
    expect(screen.getByText(/开发 · 在工作区 studio/)).toBeTruthy();
  });
});
