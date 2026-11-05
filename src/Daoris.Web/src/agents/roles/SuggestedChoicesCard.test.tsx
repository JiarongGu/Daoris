import { fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { ActorRow, RoleCatalogue, RoleRow } from '../../data/wiring';
import i18n from '../../locales/i18n';
import { RolesCard } from './Roles';
import { DECLINED_KEY } from './SuggestedChoicesCard';

// MODELROLE1g (D205 point 7, the actors design §6.2): Daoris suggests a role's choices in Working roles' head at first run. One press
// sets every suggested role as one change over SET_ROLES (AGENTSPLACE1g2), the driver judging them whole; nothing is applied by itself;
// its words say it can spend more; it is gone once the roles hold orders of the person's own, or once declined.

const seed = (id: string, en: string, zh: string, work: string): RoleRow => ({
  id, state: 'seed', seeded: true, name: { en, zh }, takes: [work], scopeKind: 'machine', revision: 0,
});

const OPUS = { agent: 'claude-code', model: 'opus', effort: 'high' };

const CATALOGUE: RoleCatalogue = {
  roles: [
    seed('build', 'Build', '开发', 'build'), seed('opinion', 'Second opinion', '第二意见', 'review'), seed('intake', 'Intake', '受理', 'intake'),
  ],
  works: ['build', 'review', 'intake'],
  kept: true,
  suggestedChoices: [
    { role: 'build', choices: [{ agent: 'claude-code', model: 'sonnet', effort: 'high' }, OPUS] },
    { role: 'opinion', choices: [OPUS, { agent: 'codex-acp' }] },
  ],
  suggestedChoicesDated: '2026-10-31',
};

const before = (id: string): ActorRow => ({ id, state: 'before', choices: [{ agent: 'claude-code', from: 'before' }] });
const AT_FIRST_RUN = [before('build'), before('opinion'), before('intake')];
const AGENTS = [{ value: 'claude-code', label: 'Claude Code' }, { value: 'codex-acp', label: 'Codex' }];

type Props = Partial<Parameters<typeof RolesCard>[0]>;

const card = (props: Props = {}) => {
  const onSetAll = vi.fn((_presses, answered) => answered.done());
  render(<RolesCard catalogue={CATALOGUE} staffing={AT_FIRST_RUN} agents={AGENTS} onSet={() => {}} onSetAll={onSetAll} {...props} />);
  return { onSetAll };
};

describe('The suggested choices', () => {
  beforeEach(() => window.localStorage.removeItem(DECLINED_KEY));
  afterEach(() => i18n.changeLanguage('en'));

  it('shows at first run in words that say it can spend more, and applies nothing by itself', () => {
    const { onSetAll } = card();

    expect(screen.getByRole('region', { name: 'Suggested choices for your roles' })).toBeTruthy();
    expect(screen.getByText(/can spend more than your roles use now/)).toBeTruthy();
    expect(screen.getByText(/Build: claude-code · sonnet · high, then claude-code · opus · high/)).toBeTruthy();
    expect(onSetAll).not.toHaveBeenCalled();
  });

  it('presses once for every suggested role with the suggested choices, the roles it does not suggest for untouched', () => {
    const { onSetAll } = card();

    fireEvent.click(screen.getByRole('button', { name: 'Use these choices' }));

    expect(onSetAll).toHaveBeenCalledTimes(1);
    expect(onSetAll.mock.calls[0]![0]).toEqual([
      {
        change: 'set', id: 'build', revision: 0, role: { name: { en: 'Build', zh: '开发' }, takes: ['build'], scope: { kind: 'machine', id: null } },
        choices: [{ agent: 'claude-code', model: 'sonnet', effort: 'high' }, { agent: 'claude-code', model: 'opus', effort: 'high' }],
      },
      {
        change: 'set', id: 'opinion', revision: 0, role: { name: { en: 'Second opinion', zh: '第二意见' }, takes: ['review'], scope: { kind: 'machine', id: null } },
        choices: [{ agent: 'claude-code', model: 'opus', effort: 'high' }, { agent: 'codex-acp', model: null, effort: null }],
      },
    ]);
  });

  it('leaves out the choices of an agent this machine has not set up, and says so', () => {
    const { onSetAll } = card({ agents: [AGENTS[0]!] });

    expect(screen.getByText('Left out, not set up on this machine: codex-acp.')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Use these choices' }));
    expect(onSetAll.mock.calls[0]![0][1].choices).toEqual([{ agent: 'claude-code', model: 'opus', effort: 'high' }]);
  });

  it('is gone where the machine has set up no agent it suggests', () => {
    card({ agents: [] });
    expect(screen.queryByRole('region', { name: 'Suggested choices for your roles' })).toBeNull();
  });

  it('is gone once a role it would change is set', () => {
    card({ staffing: [{ id: 'build', state: 'set', choices: [{ agent: 'claude-code', from: '1' }] }, before('opinion')] });
    expect(screen.queryByRole('region', { name: 'Suggested choices for your roles' })).toBeNull();
  });

  it('is gone once declined, and stays away until the table changes', () => {
    card();
    fireEvent.click(screen.getByRole('button', { name: 'Not now' }));
    expect(screen.queryByRole('region', { name: 'Suggested choices for your roles' })).toBeNull();
    expect(window.localStorage.getItem(DECLINED_KEY)).toBe('2026-10-31');
  });

  it('says the driver\'s sentence under the refused press, which refused every role at once', () => {
    const onSetAll = vi.fn((_presses, answered) => answered.refused('the role changed since it was read'));
    card({ onSetAll });

    fireEvent.click(screen.getByRole('button', { name: 'Use these choices' }));

    expect(screen.getByText('the role changed since it was read')).toBeTruthy();
    expect(onSetAll).toHaveBeenCalledTimes(1);
    expect(screen.getByRole('button', { name: 'Use these choices' })).toHaveProperty('disabled', false);
  });

  it('reads in Chinese with the same warning', async () => {
    await i18n.changeLanguage('zh');
    card();

    expect(screen.getByText(/用量也可能比现在更多/)).toBeTruthy();
    expect(screen.getByRole('button', { name: '采用这些选择' })).toBeTruthy();
    expect(screen.getByRole('button', { name: '暂不' })).toBeTruthy();
    expect(screen.getByText(/开发：claude-code · sonnet · high，然后 claude-code · opus · high/)).toBeTruthy();
  });
});
