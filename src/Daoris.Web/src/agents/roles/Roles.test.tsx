import { fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { ActorRow, RoleCatalogue, RoleRow, StaffingFound } from '../../data/wiring';
import i18n from '../../locales/i18n';
import { RolesCard, roleIdFor, roleName } from './Roles';

// FOUNDATION1y-i (the foundation design §2.7), COMPANYVIEW1a (the company-view design §3.1): the roles on AI features, one row each. The
// person adds, changes and removes a working role (its names, work, place and choices), turns it off, puts its order back to as before,
// gives a seeded one back its seed, and takes the Designer Daoris suggests. Each press goes by its door: Save and Off over `SET_ROLE`,
// Clear order over `SET_ACTOR` only; the page judges nothing, and a refusal is the driver's sentence under its press.

const seed = (id: string, en: string, zh: string, work: string): RoleRow => ({
  id, state: 'seed', seeded: true, name: { en, zh }, takes: [work], scopeKind: 'machine', revision: 0,
});

const CATALOGUE: RoleCatalogue = {
  roles: [
    seed('build', 'Build', '开发', 'build'),
    seed('intake', 'Intake', '受理', 'intake'),
    { id: 'chat', state: 'removed', seeded: true, takes: [], revision: 2 },
    {
      id: 'night', state: 'set', seeded: false, name: { en: 'Night shift', zh: '夜班' }, takes: ['build'], scopeKind: 'workspace', scopeId: 'studio',
      revision: 3, choices: [{ agent: 'codex-acp', model: 'gpt-5', effort: 'high' }],
    },
  ],
  works: ['build', 'design', 'chat', 'ask-daoris', 'intake', 'help', 'partner', 'review', 'brainstorm'],
  kept: true,
  suggested: [{ id: 'designer', name: { en: 'Designer', zh: '设计师' }, takes: ['design'], scopeKind: 'machine', choices: [{ agent: 'claude-code', model: 'opus', effort: 'high' }] }],
};

const actor = (id: string, state: ActorRow['state'], ...agents: string[]): ActorRow => ({
  id, state, choices: agents.map((agent, at) => ({ agent, from: state === 'before' ? 'before' : String(at + 1) })),
});

const STAFFING: ActorRow[] = [actor('build', 'set', 'claude-code', 'codex-acp'), actor('intake', 'before', 'claude-code'), actor('chat', 'before', 'claude-code')];

const AGENTS = [{ value: 'claude-code', label: 'Claude Code' }, { value: 'codex-acp', label: 'Codex' }];

async function choose(name: string, option: string) {
  const user = userEvent.setup();
  screen.getByRole('combobox', { name }).focus();
  await user.keyboard('{Enter}');
  await user.click(await screen.findByRole('option', { name: option }));
}

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

    expect(form.queryByLabelText('Id')).toBeNull();
    expect(form.queryByLabelText('Where')).toBeNull();
    fireEvent.change(form.getByLabelText('Name'), { target: { value: 'Night review' } });
    fireEvent.click(form.getByRole('button', { name: 'Add a language' }));
    fireEvent.change(form.getByLabelText('Language tag'), { target: { value: 'zh' } });
    fireEvent.change(form.getByLabelText('Name in that language'), { target: { value: '夜审' } });
    fireEvent.change(form.getByLabelText("choice 1's model"), { target: { value: 'opus' } });
    fireEvent.click(form.getByRole('button', { name: 'Save' }));

    expect(onSet).toHaveBeenCalledWith({
      change: 'set', id: 'night-review', revision: null,
      role: { name: { en: 'Night review', zh: '夜审' }, takes: ['build'], scope: { kind: 'machine', id: null } },
      choices: [{ agent: 'claude-code', model: 'opus', effort: null }],
    }, expect.anything());
  });

  it('names a role in one language alone as a plain string (D222)', () => {
    const { onSet } = card();
    fireEvent.click(screen.getByRole('button', { name: 'Add a role' }));
    const form = within(screen.getByRole('form', { name: 'A new role' }));
    fireEvent.change(form.getByLabelText('Name'), { target: { value: 'Lecteur' } });
    fireEvent.click(form.getByRole('button', { name: 'Save' }));
    expect(onSet).toHaveBeenLastCalledWith(expect.objectContaining({ role: expect.objectContaining({ name: 'Lecteur' }) }), expect.anything());
  });

  it('names a role in a third language by its tag, the text beside it optional (D222)', () => {
    const { onSet } = card();
    fireEvent.click(screen.getByRole('button', { name: 'Add a role' }));
    const form = within(screen.getByRole('form', { name: 'A new role' }));
    fireEvent.click(form.getByRole('button', { name: 'Add a language' }));
    fireEvent.change(form.getByLabelText('Language tag'), { target: { value: 'fr' } });
    fireEvent.change(form.getByLabelText('Name in that language'), { target: { value: 'Relecteur' } });
    fireEvent.click(form.getByRole('button', { name: 'Save' }));
    expect(onSet).toHaveBeenLastCalledWith(expect.objectContaining({ role: expect.objectContaining({ name: { fr: 'Relecteur' } }) }), expect.anything());
  });

  it('shows a role in the viewer\'s language, else its base language, else as written', () => {
    const t = ((key: string) => key) as never;
    const row = (name: RoleRow['name']) => ({ id: 'night', seeded: false, name });
    expect(roleName(t, 'zh-TW', row({ en: 'Night', zh: '夜班' }))).toBe('夜班');
    expect(roleName(t, 'fr', row({ fr: 'Nuit', en: 'Night' }))).toBe('Nuit');
    expect(roleName(t, 'de', row({ fr: 'Nuit', en: 'Night' }))).toBe('Nuit');
    expect(roleName(t, 'en', row('Night'))).toBe('Night');
  });

  it('restores every role default in one press, offered while a seeded role differs', () => {
    const { onSet } = card();

    fireEvent.click(screen.getByRole('button', { name: 'Restore all role defaults' }));

    expect(onSet).toHaveBeenCalledWith({ change: 'default-all', id: '*' }, expect.anything());
  });

  it('removes an added role after naming the role it falls back to, gives a seeded one back its seed, and takes the suggested Designer, each by the revision it read', () => {
    const { onSet } = card({ catalogue: { ...CATALOGUE, roles: [...CATALOGUE.roles, { ...CATALOGUE.roles[3]!, id: 'night-2', name: 'Night two', scopeKind: 'machine', scopeId: undefined, revision: 5 }] } });

    fireEvent.click(screen.getByRole('button', { name: 'Remove Night two' }));
    expect(onSet).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Never mind' }));
    fireEvent.click(screen.getByRole('button', { name: 'Remove Night two' }));
    expect(within(screen.getByRole('group', { name: 'Remove Night two' })).getByText(/building waits until a role takes it/)).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Remove role' }));

    fireEvent.click(screen.getByRole('button', { name: 'Restore role defaults: Chat' }));
    fireEvent.click(screen.getByRole('button', { name: 'Add the Designer' }));

    expect(onSet).toHaveBeenNthCalledWith(1, { change: 'remove', id: 'night-2', revision: 5 }, expect.anything());
    expect(onSet).toHaveBeenNthCalledWith(2, { change: 'default', id: 'chat', revision: 2 }, expect.anything());
    expect(onSet).toHaveBeenNthCalledWith(3, {
      change: 'set', id: 'designer', revision: 0,
      role: { name: { en: 'Designer', zh: '设计师' }, takes: ['design'], scope: { kind: 'machine', id: null } },
      choices: [{ agent: 'claude-code', model: 'opus', effort: 'high' }],
    }, expect.anything());
    expect(screen.getByText(/can spend more/)).toBeTruthy();
  });

  it("says the driver's refusal under Save, the editor still open", () => {
    card({ only: 'night', onSet: vi.fn((_press, answered) => answered.refused('`night` already takes `build` in that scope.')) });
    fireEvent.click(screen.getByRole('button', { name: 'Edit Night shift' }));
    const form = within(screen.getByRole('form', { name: 'Night shift' }));
    fireEvent.click(form.getByRole('button', { name: 'Save' }));

    expect(form.getByText(/in that scope/)).toBeTruthy();
    expect(screen.getByRole('form', { name: 'Night shift' })).toBeTruthy();
  });

  // AGENTSPLACE1g: All roles sets the machine-wide roles apart, reads the others and opens them at their place; the editor asks for no id
  // and picks from what the agent offers.
  describe('All roles', () => {
    it('sets the roles placed machine-wide apart from those placed elsewhere, and reads the others', () => {
      const onOpenPlace = vi.fn();
      card({ onOpenPlace });

      const machine = within(screen.getByRole('region', { name: 'Placed machine-wide' }));
      const elsewhere = within(screen.getByRole('region', { name: 'Placed elsewhere' }));
      expect(machine.getByText('Build')).toBeTruthy();
      expect(elsewhere.getByText('Night shift')).toBeTruthy();
      expect(elsewhere.queryByRole('button', { name: 'Edit Night shift' })).toBeNull();
      expect(elsewhere.queryByRole('button', { name: 'Remove Night shift' })).toBeNull();

      fireEvent.click(elsewhere.getByRole('button', { name: 'Open Night shift at studio' }));
      expect(onOpenPlace).toHaveBeenCalledWith('workspace', 'studio');
    });

    it('filters by where a role is placed, and says so when none is', async () => {
      card();
      await choose('Roles placed', 'A workspace');
      expect(screen.queryByRole('region', { name: 'Placed machine-wide' })).toBeNull();
      expect(screen.getByText('Night shift')).toBeTruthy();

      await choose('Roles placed', 'A repository');
      expect(screen.getByText('No role is placed there.')).toBeTruthy();
    });

    it('names the role a removed one falls back to', () => {
      const { onSet } = card({ only: 'night' });
      fireEvent.click(screen.getByRole('button', { name: 'Remove Night shift' }));

      expect(within(screen.getByRole('group', { name: 'Remove Night shift' })).getByText(/building falls back to Build, placed on this machine/)).toBeTruthy();
      fireEvent.click(screen.getByRole('button', { name: 'Remove role' }));
      expect(onSet).toHaveBeenCalledWith({ change: 'remove', id: 'night', revision: 3 }, expect.anything());
    });

    it("picks a model from the agent's own offers, and types a name after other…", async () => {
      const { onSet } = card({ offers: { 'claude-code': { models: ['opus', 'sonnet'], efforts: ['low', 'high'] } } });
      fireEvent.click(screen.getByRole('button', { name: 'Add a role' }));
      const form = within(screen.getByRole('form', { name: 'A new role' }));

      await choose("choice 1's model", 'sonnet');
      await choose("choice 1's effort", 'other…');
      fireEvent.change(form.getByLabelText("choice 1's effort name"), { target: { value: 'ultra' } });
      fireEvent.change(form.getByLabelText('Name'), { target: { value: 'Quick' } });
      fireEvent.click(form.getByRole('button', { name: 'Save' }));

      expect(onSet).toHaveBeenCalledWith(expect.objectContaining({
        id: 'quick', choices: [{ agent: 'claude-code', model: 'sonnet', effort: 'ultra' }],
      }), expect.anything());
    });

    it('makes an id that no role holds yet', () => {
      expect(roleIdFor('Build', 'build', ['build'])).toBe('build-2');
      expect(roleIdFor('夜审', 'review', [])).toBe('review');
    });
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
        only: 'desk',
        catalogue: {
          ...CATALOGUE,
          roles: [{ id: 'desk', state: 'set', seeded: false, name: { en: 'Desk', zh: '前台' }, takes: ['intake'], scopeKind: 'workspace', scopeId: 'studio', revision: 4, choices: [{ agent: 'claude-code' }] }],
        },
      });
      fireEvent.click(screen.getByRole('button', { name: 'Edit Desk' }));
      fireEvent.click(within(screen.getByRole('form', { name: 'Desk' })).getByRole('button', { name: 'Off' }));

      expect(onSet).toHaveBeenCalledWith(expect.objectContaining({ change: 'set', id: 'desk', revision: 4, choices: [] }), expect.anything());
    });

    it("clears a seeded role's staffing by SET_ACTOR only, and offers Clear staffing to an added role never", () => {
      const { onSet, onSetActor } = card();
      fireEvent.click(screen.getByRole('button', { name: 'Edit Build' }));
      fireEvent.click(within(screen.getByRole('form', { name: 'Build' })).getByRole('button', { name: 'Clear staffing' }));

      expect(onSetActor).toHaveBeenCalledWith('build', null, expect.anything());
      expect(onSet).not.toHaveBeenCalled();
    });

    it('offers an added role no Clear staffing', () => {
      card({ only: 'night' });
      fireEvent.click(screen.getByRole('button', { name: 'Edit Night shift' }));
      expect(within(screen.getByRole('form', { name: 'Night shift' })).queryByRole('button', { name: 'Clear staffing' })).toBeNull();
    });

    it('offers no Clear to a role already as before, and words the three acts apart', () => {
      card();
      fireEvent.click(screen.getByRole('button', { name: 'Edit Intake' }));

      expect(within(screen.getByRole('form', { name: 'Intake' })).queryByRole('button', { name: 'Clear staffing' })).toBeNull();
      expect(new Set([i18n.t('settings.ai.actors.off'), i18n.t('settings.ai.roles.clear'), i18n.t('settings.ai.roles.default')]).size).toBe(3);
      expect(i18n.t('settings.ai.roles.clear')).not.toBe(i18n.t('settings.ai.roles.default'));
    });

    it('says a refusal of Off or Clear under the editor, the editor still open', () => {
      card({ onSetActor: vi.fn((_actor, _choices, answered) => answered.refused('`build` has nothing to clear.')) });
      fireEvent.click(screen.getByRole('button', { name: 'Edit Build' }));
      const form = within(screen.getByRole('form', { name: 'Build' }));
      fireEvent.click(form.getByRole('button', { name: 'Clear staffing' }));

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
      expect(form.queryByLabelText('Name')).toBeNull();
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
      fireEvent.click(within(screen.getByRole('form', { name: 'Build: your order' })).getByRole('button', { name: 'Clear staffing' }));

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

  // MODELROLE1f: each role's last seven days from the machine log, one quiet line; a role with no start has none.
  const USE = {
    days: 7,
    roles: [{
      role: 'build', starts: 12, choices: [{ choice: '1', starts: 9 }, { choice: '2', starts: 3 }], fell: 3,
      why: [{ code: 'cooling', starts: 2 }, { code: 'signed-out', starts: 1 }],
    }, { role: 'intake', starts: 1, choices: [{ choice: 'before', starts: 1 }], fell: 0, why: [] }],
  };

  it("says a role's week: starts, the choices they landed on, and those that fell to a later one with why", () => {
    card({ catalogue: { ...CATALOGUE, use: USE } });

    expect(screen.getByText('Last 7 days: 12 starts · choice 1 ×9, choice 2 ×3 · 3 fell to a later choice (cooling ×2, signed out ×1)')).toBeTruthy();
    expect(screen.getByText('Last 7 days: 1 start · as before ×1')).toBeTruthy();
    expect(screen.getAllByText(/^Last 7 days/)).toHaveLength(2);
  });

  it('says the span a young log holds, and says nothing of a role without a start or of no history', () => {
    const { unmount } = render(
      <RolesCard catalogue={{ ...CATALOGUE, use: { days: 3, roles: [USE.roles[1]!] } }} staffing={STAFFING} agents={AGENTS} onSet={vi.fn()} onSetActor={vi.fn()} />,
    );
    expect(screen.getByText('Over the 3 days the log holds: 1 start · as before ×1')).toBeTruthy();
    unmount();

    card({ catalogue: { ...CATALOGUE, use: { days: 0, roles: [] } } });
    expect(screen.queryByText(/Last 7 days|days the log holds/)).toBeNull();
    expect(screen.queryByText(/ ×0/)).toBeNull();
  });

  it('says the week in 中文', async () => {
    await i18n.changeLanguage('zh');
    card({ catalogue: { ...CATALOGUE, use: USE } });

    expect(screen.getByText('最近 7 天: 12 次启动 · 第 1 个选择 ×9, 第 2 个选择 ×3 · 3 次落到后面的选择 (冷却中 ×2, 已退出登录 ×1)')).toBeTruthy();
  });

  it('speaks 中文, a role named in Chinese as it was written', async () => {
    await i18n.changeLanguage('zh');
    card();

    expect(screen.getByText('夜班')).toBeTruthy();
    expect(screen.getByRole('button', { name: '添加角色' })).toBeTruthy();
    expect(screen.getByText(/开发 · 在工作区 studio/)).toBeTruthy();
  });
});
