import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { DeciderLine, RoleCatalogue, RoleChangePress, RoleRow, StructurePlaceAsk } from '../../data/wiring';
import i18n from '../../locales/i18n';
import type { PlaceRoleEditor } from './PlaceRoles';
import { WhoDecidesHere } from './WhoDecidesHere';

// Who decides here words the driver's resolution for the place, a line per kind, and offers a supervisor for each kind no supervisor
// placed here decides, set over SET_ROLE with nothing ticked.

const appointed = (id: string, name: RoleRow['name'], scope: Pick<RoleRow, 'scopeKind' | 'scopeId'>, hold: RoleRow['hold']): RoleRow =>
  ({ id, state: 'set', seeded: false, name, takes: [], revision: 1, ...scope, hold });

const SUPERVISOR = appointed('supervisor', { en: 'Supervisor', zh: '主管' }, { scopeKind: 'machine' },
  { kind: 'supervisor', acts: ['question.answer', 'course.go-on', 'limit.move'], holder: 'terminal', window: 5 });
const GATEKEEPER = appointed('gatekeeper', 'Gatekeeper', { scopeKind: 'workspace', scopeId: 'games' },
  { kind: 'supervisor', acts: ['allow.once'], holder: 'terminal', window: 5, decides: ['allow'] });
const LANDER = appointed('lander', 'Lander', { scopeKind: 'workspace', scopeId: 'games' },
  { kind: 'supervisor', acts: [], holder: 'terminal', window: 5, decides: ['landing'] });

const machine = (kind: string, granted: string[]): DeciderLine => ({ kind, role: 'supervisor', scope: 'machine', holder: 'terminal', granted });
const GAMES: DeciderLine[] = [
  machine('question', ['question.answer']), machine('course', ['course.go-on']), machine('limit', ['limit.move']),
  { kind: 'allow', role: 'gatekeeper', scope: 'workspace:games', holder: 'terminal', granted: ['allow.once'] },
  machine('files', []),
  { kind: 'landing', role: 'lander', scope: 'workspace:games', holder: 'terminal', granted: [] },
];
const HELD = "The repositories' workspaces do not read (the store is locked), so the decisions in a workspace or a repository are yours until they do.";

const CATALOGUE: RoleCatalogue = {
  roles: [SUPERVISOR, GATEKEEPER, LANDER], works: ['build'], kept: true,
  deciders: {
    places: [{ place: 'machine', kinds: GAMES.map((line) => machine(line.kind, [])) }, { place: 'workspace:games', kinds: GAMES }],
    elsewhere: ['question', 'course', 'limit', 'allow', 'files', 'landing'].map((kind) => ({ kind, granted: [], held: HELD })),
  },
};

const draw = (place: StructurePlaceAsk, onSet = vi.fn()) => {
  const editor: PlaceRoleEditor = { agents: [], offers: {}, staffing: [], catalogue: CATALOGUE, read: 'ok', busy: false, onSet };
  render(<WhoDecidesHere place={place} editor={editor} />);
  return onSet;
};

describe('Who decides here', () => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it("names each kind's supervisor and where it is set, or you with why, as the driver resolves the place", () => {
    draw({ workspace: 'games' });

    expect(screen.getByText('Questions: Supervisor (this machine)')).toBeTruthy();
    expect(screen.getByText('Allows: Gatekeeper (workspace games)')).toBeTruthy();
    expect(screen.getByText('Landings: you (Lander may not accept)')).toBeTruthy();
    expect(screen.getByText("The rule file's allows: you (Supervisor may not allow)")).toBeTruthy();
  });

  it('reads a repository no place names as the driver says, the decisions yours where its map does not read', () => {
    draw({ workspace: 'games', repository: 'atlas' });

    expect(screen.getByText(`Questions: you (${HELD})`)).toBeTruthy();
  });

  it('adds a supervisor for a kind no supervisor here decides, at this place, with nothing ticked', async () => {
    const onSet = draw({ workspace: 'games' });

    screen.getByRole('combobox', { name: 'Add a supervisor for…' }).focus();
    await userEvent.keyboard('{Enter}');
    expect(screen.queryByRole('option', { name: 'Allows' })).toBeNull();
    await userEvent.click(await screen.findByRole('option', { name: 'Questions' }));
    expect(screen.getByRole('checkbox', { name: 'answer a question' })).not.toBeChecked();
    fireEvent.change(screen.getByRole('textbox', { name: 'Name' }), { target: { value: 'Answerer' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));

    const [press] = onSet.mock.calls[0] as [RoleChangePress];
    expect(press).toMatchObject({
      change: 'set', id: 'answerer',
      role: { takes: [], scope: { kind: 'workspace', id: 'games' }, filled: 'appointed', kind: 'supervisor', authority: [], holder: 'rules', window: 5, decides: ['question'] },
    });
    expect(press.choices).toBeUndefined();
  });

  it('speaks 中文', async () => {
    await i18n.changeLanguage('zh');
    draw({ workspace: 'games' });

    expect(screen.getByText('这里由谁决定')).toBeTruthy();
    expect(screen.getByText('允许：Gatekeeper（工作区 games）')).toBeTruthy();
    expect(screen.getByText('问题：主管（本机）')).toBeTruthy();
    expect(screen.getByText('落地：你（Lander 不可采纳）')).toBeTruthy();
  });
});
