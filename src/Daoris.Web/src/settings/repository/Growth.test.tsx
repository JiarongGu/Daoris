import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../../locales/i18n';
import { GrowthSetting, growthRowSays, growthSays, growthSummary, growthToast, growthValue } from './Growth';

// The growth switch, *New repositories and rooms*, set on a workspace's Setup and a repository's as `daoris settings growth` sets it from a terminal:
// each value's sentence is the terminal's (the driver's `GrowthRules.Says`), and the row sends the edit's value, which the driver judges.

const t = i18n.t.bind(i18n);

const draw = (value: string | undefined, source: 'repository' | 'workspace' | 'default' = 'default', onChange = vi.fn()) => {
  render(
    <Tooltip.Provider>
      <GrowthSetting name="engine" twin="`daoris settings growth aurora --repository engine mode|asked|off`" value={value} source={source} workspace="aurora" onChange={onChange} />
    </Tooltip.Provider>,
  );
  return onChange;
};

describe('what the growth switch says', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it("says each value in the terminal's words, where it was set, and folds an unknown one to the default", () => {
    expect(growthSays(t, 'mode')).toBe('An agent’s press that opens a repository or a room runs as the access mode says: at once in full access, on your Confirm in restricted mode.');
    expect(growthSays(t, 'asked')).toBe('An agent’s press that opens a repository or a room waits for your Confirm in Daoris’s window, even in full access.');
    expect(growthSays(t, 'off')).toBe('An agent’s press that opens a repository or a room is refused, even in full access; it works inside what exists.');
    expect(growthValue('sometimes')).toBe('mode');
    expect(growthSays(t, 'sometimes')).toBe(growthSays(t, 'mode'));
    expect(growthRowSays(t, 'off', { source: 'workspace', workspace: 'aurora' })).toContain('From the workspace aurora.');
    expect(growthRowSays(t, 'asked', { source: 'repository' })).toContain('Set for this repository.');
    expect(growthRowSays(t, 'mode', { source: 'default' })).toContain('Daoris’s default: nothing is set.');
    expect(['mode', 'asked', 'off'].map((each) => growthSummary(t, each))).toEqual(['new places as the mode says', 'new places asked each time', 'new places refused']);
  });

  it('toasts what a change did', () => {
    expect(growthToast(t, 'engine', 'off')).toBe('An agent’s press that opens a repository or a room in engine is now refused, even in full access.');
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    expect(growthSays(t, 'off')).toBe('智能体开设仓库或房间的操作会被拒绝，即使在完全访问下也是如此；它只在已有的地方工作。');
    render(<Tooltip.Provider><GrowthSetting name="engine" twin="t" value="asked" onChange={vi.fn()} /></Tooltip.Provider>);
    expect(screen.getByText('新仓库与新房间')).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: '问我' })).toBeChecked();
  });
});

describe('the growth row', () => {
  it('is titled New repositories and rooms and shows the value standing and where it is set', () => {
    draw('asked', 'workspace');

    expect(screen.getByText('New repositories and rooms')).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'Ask me' })).toBeChecked();
    expect(screen.getByText(/waits for your Confirm in Daoris’s window, even in full access\. From the workspace aurora\./)).toBeInTheDocument();
  });

  it("sends the value pressed, starting from what stands, and nothing for the value that stands", async () => {
    const onChange = draw(undefined);
    expect(screen.getByRole('radio', { name: 'As the mode says' })).toBeChecked();

    await userEvent.click(screen.getByRole('radio', { name: 'Off' }));
    await userEvent.click(screen.getByRole('radio', { name: 'As the mode says' }));

    expect(onChange).toHaveBeenCalledTimes(1);
    expect(onChange).toHaveBeenCalledWith('off');
  });

  it('offers no clear and no agent press: the switch is the person’s alone', () => {
    draw('off', 'repository');

    expect(screen.queryByRole('button', { name: /clear/i })).toBeNull();
  });
});
