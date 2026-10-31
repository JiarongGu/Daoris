import { fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { ActorRow, RoleCatalogue, RoleRow, StaffingFound } from '../../data/wiring';
import i18n from '../../locales/i18n';
import { RolesCard } from './Roles';

// FOUNDATION1y-i (the foundation design §2.7), COMPANYVIEW1a (the company-view design §3.1): the roles on AI features, one row each. The
// person adds, changes and removes a working role (its names, work, place and choices), turns it off, puts its order back to as before,
// gives a seeded one back its seed, and takes the Designer Daoris suggests. Each press goes by its door: Save and Off over `SET_ROLE`,
// Clear order over `SET_ACTOR` only; the page judges nothing, and a refusal is the driver's sentence under its press.

const seed = (id: string, en: string, zh: string, work: string): RoleRow => ({
  id, state: 'seed', seeded: true, nameEn: en, nameZh: zh, takes: [work], scopeKind: 'machine', revision: 0,
});

const CATALOGUE: RoleCatalogue = {
  roles: [
    seed('build', 'Build', '开发', 'build'),
    seed('intake', 'Intake', '受理', 'intake'),
    { id: 'chat', state: 'removed', seeded: true, takes: [], revision: 2 },
    {
      id: 'night', state: 'set', seeded: false, nameEn: 'Night shift', nameZh: '夜班', takes: ['build'], scopeKind: 'workspace', scopeId: 'studio',
      revision: 3, choices: [{ agent: 'codex-acp', model: 'gpt-5', effort: 'high' }],
    },
  ],
  works: ['build', 'design', 'chat', 'ask-daoris', 'intake', 'help', 'partner', 'review', 'brainstorm'],
  kept: true,
  suggested: [{ id: 'designer', nameEn: 'Designer', nameZh: '设计师', takes: ['design'], scopeKind: 'machine', choices: [{ agent: 'claude-code', model: 'opus', effort: 'high' }] }],
};

const actor = (id: string, state: ActorRow['state'], ...agents: string[]): ActorRow => ({
  id, state, choices: agents.map((agent, at) => ({ agent, from: state === 'before' ? 'before' : String(at + 1) })),
});

const STAFFING: ActorRow[] = [actor('build', 'set', 'claude-code', 'codex-acp'), actor('intake', 'before', 'claude-code'), actor('chat', 'before', 'claude-code')];

const AGENTS = [{ value: 'claude-code', label: 'Claude Code' }, { value: 'codex-acp', label: 'Codex' }];

type Props = Partial<Parameters<typeof RolesCard>[0]>;

const card = (props: Props = {}) => {
  // A press the driver took: its answer lands, so the editor closes as it does on the page.
  const onSet = vi.fn((_press, answered) => answered.done());
  const onSetActor = vi.fn((_actor, _choices, answered) => answered.done());
  render(<RolesCard catalogue={CATALOGUE} staffing={STAFFING} agents={AGENTS} onSet={onSet} onSetActor={onSetActor} {...props} />);
  return { onSet, onSetActor };
};

describe('The roles', () => {
  afterEach(() => i18n.changeLanguage('en'));

  it('says each role with its work and place, a removed one waiting, and an added one with its choices', () => {
    card();

    expect(screen.getByText('Night shift')).toBeTruthy();
    expect(screen.getByText(/building · in workspace studio · codex-acp · gpt-5 · high/)).toBeTruthy();
    expect(screen.getByText(/Removed: its work waits until a role takes it/)).toBeTruthy();
  });

  it('draws each seeded role once, with its staffing on its row, and the word Actors nowhere', () => {
    card();

    expect(screen.getAllByText('Build')).toHaveLength(1);
    expect(screen.getByText(/building · on this machine · claude-code · account default · account default \+1/)).toBeTruthy();
    expect(screen.getByText(/intake · on this machine · claude-code · account default · account default · as before/)).toBeTruthy();
    expect(screen.queryByText(/Actors/)).toBeNull();
    expect(screen.queryByText('Role catalogue')).toBeNull();
  });

  it('adds a role from its editor, every field going out as SET_ROLE takes it', () => {
    const { onSet } = card();
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
    const { onSet } = card();

    fireEvent.click(screen.getByRole('button', { name: 'Remove Night shift' }));
    fireEvent.click(screen.getByRole('button', { name: 'Back to the default: Chat' }));
    fireEvent.click(screen.getByRole('button', { name: 'Add the Designer' }));

    expect(onSet).toHaveBeenNthCalledWith(1, { change: 'remove', id: 'night', revision: 3 }, expect.anything());
    expect(onSet).toHaveBeenNthCalledWith(2, { change: 'default', id: 'chat', revision: 2 }, expect.anything());
    expect(onSet).toHaveBeenNthCalledWith(3, {
      change: 'set', id: 'designer', revision: 0,
      role: { name: { en: 'Designer', zh: '设计师' }, takes: ['design'], scope: { kind: 'machine', id: null } },
      choices: [{ agent: 'claude-code', model: 'opus', effort: 'high' }],
    }, expect.anything());
    expect(screen.getByText(/can spend more/)).toBeTruthy();
  });

  it("says the driver's refusal under Save, the editor still open", () => {
    card({ onSet: vi.fn((_press, answered) => answered.refused('`night` already takes `build` in that scope.')) });
    fireEvent.click(screen.getByRole('button', { name: 'Edit Night shift' }));
    const form = within(screen.getByRole('form', { name: 'Night shift' }));
    fireEvent.click(form.getByRole('button', { name: 'Save' }));

    expect(form.getByText(/in that scope/)).toBeTruthy();
    expect(screen.getByRole('form', { name: 'Night shift' })).toBeTruthy();
  });

  describe('the doors, per press', () => {
    it("saves a seeded role's order by SET_ROLE, the editor starting from its staffing and reordering it", () => {
      const { onSet, onSetActor } = card();
      fireEvent.click(screen.getByRole('button', { name: 'Edit Build' }));
      const form = within(screen.getByRole('form', { name: 'Build' }));

      fireEvent.click(form.getByRole('button', { name: 'Move choice 1 down' }));
      expect(form.getByRole('button', { name: 'Move choice 2 down' })).toBeDisabled();
      fireEvent.click(form.getByRole('button', { name: 'Save' }));

      expect(onSet).toHaveBeenCalledWith({
        change: 'set', id: 'build', revision: 0,
        role: { name: { en: 'Build', zh: '开发' }, takes: ['build'], scope: { kind: 'machine', id: null } },
        choices: [{ agent: 'codex-acp', model: null, effort: null }, { agent: 'claude-code', model: null, effort: null }],
      }, expect.anything());
      expect(onSetActor).not.toHaveBeenCalled();
    });

    it('turns a role that takes intake off by SET_ROLE with no choices, and offers Off to no other', () => {
      const { onSet } = card();
      fireEvent.click(screen.getByRole('button', { name: 'Edit Build' }));
      expect(within(screen.getByRole('form', { name: 'Build' })).queryByRole('button', { name: 'Off' })).toBeNull();
      fireEvent.click(within(screen.getByRole('form', { name: 'Build' })).getByRole('button', { name: 'Never mind' }));

      fireEvent.click(screen.getByRole('button', { name: 'Edit Intake' }));
      fireEvent.click(within(screen.getByRole('form', { name: 'Intake' })).getByRole('button', { name: 'Off' }));

      expect(onSet).toHaveBeenCalledWith({
        change: 'set', id: 'intake', revision: 0,
        role: { name: { en: 'Intake', zh: '受理' }, takes: ['intake'], scope: { kind: 'machine', id: null } },
        choices: [],
      }, expect.anything());
    });

    it('turns an added role off by SET_ROLE when its work allows it', () => {
      const { onSet } = card({
        catalogue: {
          ...CATALOGUE,
          roles: [{ id: 'desk', state: 'set', seeded: false, nameEn: 'Desk', nameZh: '前台', takes: ['intake'], scopeKind: 'workspace', scopeId: 'studio', revision: 4, choices: [{ agent: 'claude-code' }] }],
        },
      });
      fireEvent.click(screen.getByRole('button', { name: 'Edit Desk' }));
      fireEvent.click(within(screen.getByRole('form', { name: 'Desk' })).getByRole('button', { name: 'Off' }));

      expect(onSet).toHaveBeenCalledWith(expect.objectContaining({ change: 'set', id: 'desk', revision: 4, choices: [] }), expect.anything());
    });

    it("clears a seeded role's order by SET_ACTOR only, and offers Clear to an added role never", () => {
      const { onSet, onSetActor } = card();
      fireEvent.click(screen.getByRole('button', { name: 'Edit Build' }));
      fireEvent.click(within(screen.getByRole('form', { name: 'Build' })).getByRole('button', { name: 'Clear order' }));

      expect(onSetActor).toHaveBeenCalledWith('build', null, expect.anything());
      expect(onSet).not.toHaveBeenCalled();

      fireEvent.click(screen.getByRole('button', { name: 'Edit Night shift' }));
      expect(within(screen.getByRole('form', { name: 'Night shift' })).queryByRole('button', { name: 'Clear order' })).toBeNull();
    });

    it('offers no Clear to a role already as before, and words Clear order apart from Back to the default', () => {
      card();
      fireEvent.click(screen.getByRole('button', { name: 'Edit Intake' }));

      expect(within(screen.getByRole('form', { name: 'Intake' })).queryByRole('button', { name: 'Clear order' })).toBeNull();
      expect(i18n.t('settings.ai.roles.clear')).not.toBe(i18n.t('settings.ai.roles.default'));
    });

    it('says a refusal of Off or Clear under the editor, the editor still open', () => {
      card({ onSetActor: vi.fn((_actor, _choices, answered) => answered.refused('`build` has nothing to clear.')) });
      fireEvent.click(screen.getByRole('button', { name: 'Edit Build' }));
      const form = within(screen.getByRole('form', { name: 'Build' }));
      fireEvent.click(form.getByRole('button', { name: 'Clear order' }));

      expect(form.getByText(/nothing to clear/)).toBeTruthy();
    });
  });

  describe("a home whose driver.json is the file's own", () => {
    const unkept = { catalogue: { ...CATALOGUE, kept: false, suggested: [] } };

    it("keeps each seeded role's staffing editor over SET_ACTOR, and holds Add a role, Remove and the place", () => {
      const { onSet, onSetActor } = card(unkept);

      expect(screen.getByText(/driver\.json is the file's own/)).toBeTruthy();
      expect(screen.getByRole('button', { name: 'Add a role' })).toBeDisabled();
      expect(screen.getByRole('button', { name: 'Remove Build' })).toBeDisabled();

      fireEvent.click(screen.getByRole('button', { name: 'Edit Build' }));
      const form = within(screen.getByRole('form', { name: 'Build: your order' }));
      expect(form.queryByLabelText('Name in English')).toBeNull();
      expect(form.queryByLabelText('Where')).toBeNull();
      fireEvent.click(form.getByRole('button', { name: 'Save' }));

      expect(onSetActor).toHaveBeenCalledWith('build', [
        { agent: 'claude-code', model: null, effort: null }, { agent: 'codex-acp', model: null, effort: null },
      ], expect.anything());
      expect(onSet).not.toHaveBeenCalled();
    });

    it('turns Intake off and clears Build by SET_ACTOR there', () => {
      const { onSet, onSetActor } = card(unkept);
      fireEvent.click(screen.getByRole('button', { name: 'Edit Intake' }));
      fireEvent.click(within(screen.getByRole('form', { name: 'Intake: your order' })).getByRole('button', { name: 'Off' }));
      fireEvent.click(screen.getByRole('button', { name: 'Edit Build' }));
      fireEvent.click(within(screen.getByRole('form', { name: 'Build: your order' })).getByRole('button', { name: 'Clear order' }));

      expect(onSetActor).toHaveBeenNthCalledWith(1, 'intake', [], expect.anything());
      expect(onSetActor).toHaveBeenNthCalledWith(2, 'build', null, expect.anything());
      expect(onSet).not.toHaveBeenCalled();
    });
  });

  describe('a shell that answers the actors and not the roles', () => {
    it('keeps one staffing editor over SET_ACTOR, and says the catalogue is not offered by this shell', () => {
      const { onSet, onSetActor } = card({ catalogue: null, read: 'notOffered' });

      expect(screen.getByText(/Not offered by this shell/)).toBeTruthy();
      expect(screen.queryByRole('button', { name: 'Add a role' })).toBeNull();
      expect(screen.getAllByText('Build')).toHaveLength(1);

      fireEvent.click(screen.getByRole('button', { name: 'Edit Build' }));
      fireEvent.click(within(screen.getByRole('form', { name: 'Build: your order' })).getByRole('button', { name: 'Save' }));

      expect(onSetActor).toHaveBeenCalledWith('build', expect.any(Array), expect.anything());
      expect(onSet).not.toHaveBeenCalled();
    });

    it('tells a failed read from a shell that does not offer it, and from an empty catalogue', () => {
      const { unmount } = render(<RolesCard catalogue={null} read="failed" staffing={STAFFING} agents={AGENTS} onSet={() => {}} onSetActor={() => {}} />);
      expect(screen.getByText(/could not be read/)).toBeTruthy();
      expect(screen.queryByText(/Not offered by this shell/)).toBeNull();
      unmount();

      render(<RolesCard catalogue={{ ...CATALOGUE, roles: [], suggested: [] }} staffing={[]} agents={AGENTS} onSet={() => {}} onSetActor={() => {}} />);
      expect(screen.getByText('No role is set yet.')).toBeTruthy();
      expect(screen.queryByText(/could not be read/)).toBeNull();
      expect(screen.queryByText(/Not offered by this shell/)).toBeNull();
    });
  });

  describe('the changes found outside Daoris', () => {
    const before = (id: string, agent: string): ActorRow => ({ id, state: 'before', choices: [{ agent, from: 'before' }] });
    const FOUND: StaffingFound[] = [{
      id: 7,
      at: '2026-10-30T10:00:00Z',
      changes: [
        { role: 'build', was: before('build', 'claude-code-acp'), now: before('build', 'codex-acp'), applied: false },
        { role: 'intake', was: before('intake', 'codex-acp'), now: { id: 'intake', state: 'off', choices: [] }, applied: true },
      ],
    }];

    it('says each change against what Daoris last wrote, a turn-off applied at once, and presses Take it and Dismiss', () => {
      const onAnswerFound = vi.fn();
      card({ found: FOUND, onAnswerFound });
      const found = within(screen.getByRole('list', { name: 'Changed outside Daoris' }));

      expect(found.getByText(/Build: claude-code-acp · account default · account default · as before → codex-acp/)).toBeTruthy();
      expect(found.getByText(/Intake: .* → Off — declarations only \(turned off at once\)/)).toBeTruthy();
      fireEvent.click(found.getByRole('button', { name: 'Take it' }));
      fireEvent.click(found.getByRole('button', { name: 'Dismiss' }));

      expect(onAnswerFound).toHaveBeenNthCalledWith(1, 7, 'take', expect.anything());
      expect(onAnswerFound).toHaveBeenNthCalledWith(2, 7, 'dismiss', expect.anything());
    });
  });

  it('speaks 中文, a role named in Chinese as it was written', async () => {
    await i18n.changeLanguage('zh');
    card();

    expect(screen.getByText('夜班')).toBeTruthy();
    expect(screen.getByRole('button', { name: '添加角色' })).toBeTruthy();
    expect(screen.getByText(/开发 · 在工作区 studio/)).toBeTruthy();
  });
});
