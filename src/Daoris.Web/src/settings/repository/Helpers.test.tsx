import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../../locales/i18n';
import { HELPER_DEFAULT, HelperField, type HelperRule, helperRowSays, helperSays, helperSummary, helperToast } from './Helpers';

// DOORS1b (D163 point 3, D164's amendment point 3): what a helper a repository's agent asks for does, set on its Setup's
// *Sessions* and on a workspace's Defaults as `daoris driver helpers` sets it from a terminal (D50). Each mode's sentence is the
// terminal's (`driver helpers`' words in the CLI's `driverconfig.ts`), the agents are named in the order they are tried as the
// driver's choice walks them (D163's MIXAGENT2a-ii note), and the field sends the twins' edit, which the driver judges.

const t = i18n.t.bind(i18n);

const OWN: HelperRule = { mode: 'on', agents: ['codex-acp', 'dsh'] };

const draw = (set: HelperRule | undefined, inherited: HelperRule = { mode: 'ask', agents: [] }, onChange = vi.fn()) => {
  render(
    <Tooltip.Provider>
      <HelperField name="web-app" set={set} inherited={inherited} onChange={onChange} />
    </Tooltip.Provider>,
  );
  return onChange;
};

describe('what a helpers rule says', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it("says each mode in the terminal's words, then who may be asked", () => {
    expect(helperSays(t, { mode: 'ask', agents: [] })).toEqual([
      'Each helper an agent asks for waits for your yes.',
      "No agent is named here, so a session may ask any: the one it names, else another maker's agent.",
    ]);
    expect(helperSays(t, OWN)).toEqual([
      'A helper an agent asks for starts at once, within the limits: two at once for a session, one turn each.',
      'Only `codex-acp`, else `dsh`, may help, tried in that order unless the session names one of them.',
    ]);
    // Off offers none, so who may be asked is not said.
    expect(helperSays(t, { mode: 'off', agents: ['dsh'] })).toEqual(['A helper is not offered to a session here: only your own press asks one.']);
  });

  it('says where it was set, the default included, and folds to its mode', () => {
    // HELPERDEFAULT1: the default starts Daoris's own agents at once, those set up and signed in here.
    expect(helperRowSays(t, HELPER_DEFAULT, { source: 'default' })).toBe(
      'A helper an agent asks for starts at once, within the limits: two at once for a session, one turn each. Only Daoris\'s own '
      + "agents are asked, those it has set up and signed in on this machine: the one the session names, else another maker's; one "
      + "it names that is not among them is refused with the reason. Daoris's default: nothing is set.");
    expect(helperRowSays(t, { mode: 'ask', agents: [] }, { source: 'workspace', workspace: 'work' })).toBe(
      'Each helper an agent asks for waits for your yes. No agent is named here, so a session may ask any: the one it names, else '
      + "another maker's agent. From the workspace work.");
    expect(helperRowSays(t, { mode: 'off', agents: [] }, { source: 'workspace', workspace: 'work' }))
      .toBe('A helper is not offered to a session here: only your own press asks one. From the workspace work.');
    expect(helperRowSays(t, { mode: 'off', agents: [] }, { source: 'repository' }))
      .toBe('A helper is not offered to a session here: only your own press asks one. Set for this repository.');
    expect(helperSummary(t, { mode: 'ask', agents: [] })).toBe('helpers wait for your yes');
    expect(helperSummary(t, OWN)).toBe('helpers start at once');
    expect(helperSummary(t, { mode: 'off', agents: [] })).toBe('no helpers offered');
  });

  it('toasts what a change did', () => {
    expect(helperToast(t, 'web-app', { set: { mode: 'ask', agents: [] } })).toBe('A helper an agent in web-app asks for now waits for your yes.');
    expect(helperToast(t, 'web-app', { set: { mode: 'on', agents: ['dsh'] } }))
      .toBe('A helper an agent in web-app asks for now starts at once, within the limits.');
    expect(helperToast(t, 'web-app', { set: { mode: 'off' } })).toBe('Helpers are not offered to sessions in web-app now.');
    expect(helperToast(t, 'web-app', { clear: true })).toBe('web-app takes its helpers rule from what stands above it again.');
  });

  it('says it in Chinese, each agent kept as written', async () => {
    await i18n.changeLanguage('zh');

    expect(helperSays(t, OWN)).toEqual([
      '智能体请的助手在限额内立即开始：一个会话同时最多两位，每位一轮。',
      '只有 `codex-acp`，其次 `dsh` 可以帮忙；会话没有点名其中一个时，按这个顺序尝试。',
    ]);
    expect(helperRowSays(t, { mode: 'off', agents: [] }, { source: 'repository' })).toBe('这里的会话没有助手可请：只有你自己按下才会请一位。为这个仓库设定。');
    expect(helperRowSays(t, HELPER_DEFAULT, { source: 'default' })).toContain('只请 Daoris 自己的智能体');
    expect(helperSummary(t, { mode: 'ask', agents: [] })).toBe('助手需你同意');
    expect(helperToast(t, 'web-app', { clear: true })).toBe('web-app 重新沿用上一级的助手规则。');
  });

  /** Its terminal twin is one command with its alternatives joined by `|`, never translated, as its neighbours' (OPINIONTWIN1). */
  it('says its terminal twin as its neighbours do, one command with its alternatives joined by |, in both languages', () => {
    for (const key of ['settings.helpers.twin.repository', 'settings.helpers.twin.workspace']) {
      const english = i18n.getFixedT('en')(key, { repository: 'engine', workspace: 'aurora' });
      const chinese = i18n.getFixedT('zh')(key, { repository: 'engine', workspace: 'aurora' });

      expect(chinese).toBe(english);
      expect(english.match(/`/g)).toHaveLength(2);
      expect(english).toMatch(/--mode ask\|on\|off --agents <adapter,adapter>\|--clear`$/);
    }
    expect(i18n.getFixedT('en')('settings.helpers.twin.repository', { repository: 'engine' }))
      .toBe('`daoris driver helpers engine --mode ask|on|off --agents <adapter,adapter>|--clear`');
    expect(i18n.getFixedT('en')('settings.helpers.twin.workspace', { workspace: 'aurora' }))
      .toBe('`daoris driver helpers --workspace aurora --mode ask|on|off --agents <adapter,adapter>|--clear`');
  });
});

describe("a helpers rule's field", () => {
  it('starts from what stands above where nothing of its own is set, and sends the mode and the agents in order', async () => {
    const onChange = draw(undefined, { mode: 'off', agents: ['dsh'] });
    const user = userEvent.setup();

    expect(screen.getByRole('radio', { name: 'Not offered' })).toBeChecked();
    const agents = screen.getByRole('textbox', { name: "The agents web-app's sessions may ask, in the order they are tried" });
    expect(agents).toHaveValue('dsh');
    expect(screen.getByRole('button', { name: 'Save' })).toBeEnabled();
    await user.click(screen.getByRole('radio', { name: 'Start at once' }));
    await user.clear(agents);
    await user.type(agents, 'codex-acp, dsh');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(onChange).toHaveBeenLastCalledWith({ set: { mode: 'on', agents: ['codex-acp', 'dsh'] } });
  });

  it('opens on the rule set here, holds Save until something changes, and no agents named is the empty list', async () => {
    const onChange = draw(OWN);
    const user = userEvent.setup();

    expect(screen.getByRole('radio', { name: 'Start at once' })).toBeChecked();
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();
    await user.clear(screen.getByRole('textbox', { name: "The agents web-app's sessions may ask, in the order they are tried" }));
    await user.click(screen.getByRole('radio', { name: 'Ask me' }));
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(onChange).toHaveBeenLastCalledWith({ set: { mode: 'ask', agents: [] } });
  });
});
