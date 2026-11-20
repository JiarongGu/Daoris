import { describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import { drawnFromJson, graphFlow } from './graphFlow';
import { STEP_ROLES, namedRole, roleOf, roleSaid } from './stepRoles';

// Every step names its role:the role it names, else the older `role` option an agent step named it by, else its kind's
// default the palette carries; Daoris's own roles are worded in both catalogues, and a catalogue role's id is said as written.

const t = i18n.t.bind(i18n);

describe('a step\'s role', () => {
  it('reads the role a step names, then the older option, then its kind\'s', () => {
    expect(namedRole({ role: 'tester', options: { role: 'exact' } })).toBe('tester');
    expect(namedRole({ options: { role: 'exact' } })).toBe('exact');
    expect(namedRole({ options: { role: '' } })).toBeNull();
    expect(roleOf({}, 'acceptor')).toBe('acceptor');
    expect(roleOf({ role: 'approver' }, 'acceptor')).toBe('approver');
    expect(roleOf({}, null)).toBeNull();
  });

  it('words each of Daoris\'s roles in both catalogues, and a catalogue role as written', async () => {
    const said = (language: string) => STEP_ROLES.map((role) => roleSaid(i18n.getFixedT(language), i18n, role));
    expect(said('en')).toEqual(['Design', 'Second opinion', 'Tester', 'Acceptor', 'Asker', 'Approver', 'Release manager', 'Operations', 'Config changer', 'Credentials keeper', 'Owner']);
    expect(said('zh')).toEqual(['设计', '第二意见', '试用人', '采纳人', '委托方', '审批人', '发布负责人', '运维', '配置变更人', '凭据管理人', '所有者']);
    expect(roleSaid(t, i18n, 'night-builder')).toBe('night-builder');
  });

  it('is drawn on the chart where the step names it', () => {
    const drawn = drawnFromJson(t, i18n, {
      entries: ['work'],
      nodes: [{ id: 'work', kind: 'claimed' }, { id: 'look', kind: 'look', role: 'tester' }],
      wires: [{ id: 'w', kind: 'after', from: 'work', to: 'look' }],
    });
    const flow = graphFlow(t, drawn, { label: 'graph' });
    const look = flow.nodes.find((node) => node.id === 'look');
    expect(look && 'step' in look ? (look as { step?: { role?: string } }).step?.role : undefined).toBe('tester');
    const work = flow.nodes.find((node) => node.id === 'work');
    expect(work && 'step' in work ? (work as { step?: { role?: string } }).step?.role : undefined).toBeUndefined();
  });
});
