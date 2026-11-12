import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import { LANGUAGES } from '../fixtures';
import { WorkspaceSetup, type WorkspaceSetupProps } from './WorkspaceSetup';

// A workspace's Setup carries the growth switch as its Defaults row, *New repositories and rooms*: a new workspace stands at `mode`, Daoris's default,
// and a press goes out as the edit's value. The person's alone, so the row offers no clear and no agent press.

const at = (set: 'mode' | 'asked' | 'off' | undefined, opened = true, onChange = vi.fn()) => {
  const props: WorkspaceSetupProps = {
    workspace: 'aurora',
    defaults: { line: { onSet: vi.fn() }, language: { table: LANGUAGES, onSet: vi.fn() }, read: { onSet: vi.fn() }, growth: { ...(set ? { set } : {}), onChange } },
    remote: { rules: { lists: { allow: [], ask: [], deny: [] }, onAdd: vi.fn(), onRemove: vi.fn() } },
  };
  render(<Tooltip.Provider><WorkspaceSetup {...props} {...(opened ? { open: 'defaults' as const } : {})} /></Tooltip.Provider>);
  return onChange;
};

const rowOf = () => screen.getByText('New repositories and rooms', { selector: 'span' }).closest('[data-setting-row]') as HTMLElement;

afterEach(async () => { await i18n.changeLanguage('en'); });

describe("a workspace's growth row", () => {
  it('stands at mode for a new workspace, as Daoris’s default, and sends the value pressed', async () => {
    const onChange = at(undefined);

    expect(within(rowOf()).getByRole('radio', { name: 'As the mode says' })).toBeChecked();
    expect(within(rowOf()).getByText(/Daoris’s default: nothing is set\./)).toBeInTheDocument();
    expect(within(rowOf()).queryByRole('button', { name: /clear/i })).toBeNull();

    await userEvent.click(within(rowOf()).getByRole('radio', { name: 'Off' }));

    expect(onChange).toHaveBeenCalledWith('off');
  });

  it('shows a value set for the workspace as its own, and folds to it marked as set here', () => {
    at('asked', false);

    expect(screen.getByRole('button', { name: 'Defaults' })).toHaveAttribute('aria-expanded', 'true');
    expect(within(rowOf()).getByRole('radio', { name: 'Ask me' })).toBeChecked();
    expect(within(rowOf()).getByText(/waits for your Confirm in Daoris’s window, even in full access\. From the workspace aurora\./)).toBeInTheDocument();
  });

  it("folds into the Defaults line, marked as Daoris's default, and says it in 中文", async () => {
    at(undefined, false);

    expect(screen.getByRole('button', { name: 'Defaults' })).toHaveAccessibleDescription(expect.stringContaining("new places as the mode says (Daoris's default)"));
    await userEvent.click(screen.getByRole('button', { name: 'Defaults' }));
    await i18n.changeLanguage('zh');
    expect(screen.getByText('新仓库与新房间', { selector: 'span' })).toBeInTheDocument();
    const zh = screen.getByText('新仓库与新房间', { selector: 'span' }).closest('[data-setting-row]') as HTMLElement;
    expect(within(zh).getByRole('radio', { name: '按模式' })).toBeChecked();
  });
});
