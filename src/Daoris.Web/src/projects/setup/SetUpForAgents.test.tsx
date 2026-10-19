import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import '../../locales/i18n';
import { code } from '../../test/code';
import { NEWBIE_SETUP } from '../fixtures';
import { SetUpForAgents, type SetUpForAgentsProps } from './SetUpForAgents';

// A repository's *Set up for agents* (LAYOUT8, D117 §6.1, §6.5; D170 point 1): the plan first, `--plan`'s twin, then the press.
// Props only: the view above reads the plan and presses.

const show = (props: Partial<SetUpForAgentsProps> = {}) => {
  const doors = { onLook: vi.fn(), onPress: vi.fn(), onClose: vi.fn() };
  render(
    <SetUpForAgents repository="newbie" plan={null} reading={false} pressing={false} pressed={null} problem={null} {...doors} {...props} />,
  );
  return doors;
};

describe('setting a repository up for agents', () => {
  it('reads nothing until the person asks to see the plan', async () => {
    const doors = show();

    expect(screen.getByRole('heading', { name: 'Set up for agents' })).toBeInTheDocument();
    expect(screen.getByText(/writes nothing into it/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Show the plan' }));

    expect(doors.onLook).toHaveBeenCalledOnce();
    expect(screen.queryByRole('button', { name: 'Set it up' })).toBeNull();
  });

  it('shows what was read, the rule and the quest before the press, and presses it', async () => {
    const doors = show({ plan: NEWBIE_SETUP });

    const facts = screen.getByLabelText('What a setup of newbie would ask');
    expect(within(facts).getByText(code('main'))).toBeInTheDocument();
    expect(within(facts).getByText(code('0123456789ab'))).toBeInTheDocument();
    expect(within(facts).getByText('the whole setup')).toBeInTheDocument();
    expect(within(facts).getByText(code('claude-code-acp'))).toBeInTheDocument();
    expect(within(facts).getByText(/merged into its line/)).toBeInTheDocument();
    expect(within(screen.getByRole('list', { name: 'The rule it adds' })).getAllByRole('listitem').map((item) => item.textContent))
      .toEqual(NEWBIE_SETUP.rules);
    expect(screen.getByText(NEWBIE_SETUP.title!)).toBeInTheDocument();
    expect(screen.getByText(/take up the shared doctrine/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Set it up' }));
    expect(doors.onPress).toHaveBeenCalledOnce();
  });

  it('says every refusal at once in the driver\'s words, offers no press, and looks again', async () => {
    const doors = show({
      plan: {
        ...NEWBIE_SETUP, pressable: false,
        refusals: [
          { code: 'not-driven', sentence: '`newbie` is not driven here: drive it with `daoris driver drive newbie`.' },
          { code: 'no-own-tree', sentence: '`newbie`\'s sessions run in its checkout: `daoris driver trees newbie on`.' },
        ],
      },
    });

    const refused = screen.getByRole('list', { name: 'It cannot be set up yet' });
    expect(within(refused).getAllByRole('listitem')).toHaveLength(2);
    expect(within(refused).getByText(code('daoris driver drive newbie'))).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Set it up' })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Look again' }));
    expect(doors.onLook).toHaveBeenCalledOnce();
  });

  it('says what the press published, and that its own session takes it', () => {
    show({
      plan: NEWBIE_SETUP,
      pressed: {
        published: true, message: 'Asked as `#a1b2c3d4` in `default`.', askId: 'a1b2c3d4', questId: 'q1a2b3c4',
        added: NEWBIE_SETUP.rules, plan: NEWBIE_SETUP,
      },
    });

    const said = screen.getByRole('status');
    expect(within(said).getByText(code('#a1b2c3d4'))).toBeInTheDocument();
    expect(within(said).getByText(code('#q1a2b3c4'))).toBeInTheDocument();
    expect(within(said).getByText(/its own session takes it at the next look/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Set it up' })).toBeNull();
  });

  it('speaks 中文 in its chrome and leaves the quest and the rules as written', async () => {
    const { default: i18n } = await import('../../locales/i18n');
    await i18n.changeLanguage('zh');
    try {
      show({ plan: NEWBIE_SETUP });
      expect(screen.getByRole('heading', { name: '为智能体配置' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: '配置' })).toBeInTheDocument();
      expect(screen.getByText(NEWBIE_SETUP.title!)).toBeInTheDocument();
      expect(screen.getByText('Bash(daoris --version)')).toBeInTheDocument();
    } finally {
      cleanup();
      await i18n.changeLanguage('en');
    }
  });
});
