import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { code } from '../test/code';
import { LANGUAGES } from './fixtures';
import { WorkspaceSetup, type WorkspaceSetupProps } from './WorkspaceSetup';

// A workspace's Setup as a molecule (UX6g, D150 §4.3): its defaults, its remote and its rules, each from props, and every
// press going out. Where Settings → Workspace's defaults and wiring, and Permissions' workspace rows, went (§3.1).

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

/** Everything at Daoris's default: no line, merged into its line, no language, read; local only, no rules of its own. */
const at = (over: Partial<WorkspaceSetupProps> = {}) => {
  const acts = {
    line: vi.fn(), landing: vi.fn(), language: vi.fn(), read: vi.fn(), wire: vi.fn(), unwire: vi.fn(), add: vi.fn(), remove: vi.fn(),
  };
  const props: WorkspaceSetupProps = {
    workspace: 'aurora',
    defaults: {
      line: { onSet: acts.line },
      landing: { landers: ['github-pull-request'], onSet: acts.landing },
      language: { table: LANGUAGES, onSet: acts.language },
      read: { onSet: acts.read },
    },
    remote: {
      wiring: { remote: null, fromEnvironment: false, onWire: acts.wire, onUnwire: acts.unwire },
      rules: { lists: { allow: [], ask: [], deny: [] }, onAdd: acts.add, onRemove: acts.remove },
    },
    ...over,
  };
  render(<WorkspaceSetup {...props} />);
  return { acts, props };
};

const section = (name: string) => screen.getByRole('button', { name });

describe("a workspace's Setup", () => {
  /** §1 rule 4: what Daoris decides folds, each value in its line marked as Daoris's. */
  it("folds to what Daoris decides, each value marked as Daoris's default", () => {
    at();

    expect(section('Defaults')).toHaveAttribute('aria-expanded', 'false');
    expect(section('Defaults')).toHaveAccessibleDescription(
      "each checkout's own line (Daoris's default) · lands into its line (Daoris's default) · no session language (Daoris's default) · "
      + "read by agents outside it (Daoris's default)");
    expect(section('Remote and reach')).toHaveAttribute('aria-expanded', 'false');
    expect(section('Remote and reach')).toHaveAccessibleDescription('local only · no rules of its own');
  });

  it('opens a section that holds a value the workspace sets, with its Clear', async () => {
    const { acts } = at({
      defaults: {
        line: { set: 'develop', onSet: vi.fn() },
        landing: { set: { form: 'branch', pattern: 'feature/{quest}-{slug}', plugin: 'github-pull-request' }, landers: [], onSet: vi.fn() },
        language: { set: 'zh', table: LANGUAGES, onSet: vi.fn() },
        read: { set: false, onSet: (read) => acts.read(read) },
      },
    });

    expect(section('Defaults')).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByText(code('develop'))).toBeInTheDocument();
    expect(screen.getByText('Simplified Chinese (简体中文), set for this workspace.')).toBeInTheDocument();
    expect(screen.getByText(/Read by no agent outside it/)).toBeInTheDocument();
    // Each value the workspace sets carries its Clear, which hands it back to Daoris's.
    expect(screen.getAllByRole('button', { name: 'Clear' })).toHaveLength(4);
    await userEvent.click(screen.getAllByRole('button', { name: 'Clear' })[3]!);
    expect(acts.read).toHaveBeenLastCalledWith(undefined);
  });

  /** Each row's hint is its terminal twin (D41 §4, D50): `daoris driver <verb> --workspace <name>`. */
  it('names each row\'s terminal twin', async () => {
    at();
    await userEvent.click(section('Defaults'));

    expect(screen.getByText(code('daoris driver line --workspace aurora <branch>|--clear'))).toBeInTheDocument();
    expect(screen.getByText(code('daoris driver landing --workspace aurora merge|branch <pattern>|--clear'))).toBeInTheDocument();
    expect(screen.getByText(code('daoris driver language --workspace aurora en|zh|--clear'))).toBeInTheDocument();
    expect(screen.getByText(code('daoris driver across --workspace aurora read on|off|--clear'))).toBeInTheDocument();
  });

  it('sets a default where none is: its line, how its work lands, its language and its reading', async () => {
    const { acts } = at();
    const user = userEvent.setup();
    await user.click(section('Defaults'));

    const sets = () => screen.getAllByRole('button', { name: 'Set for this workspace' });
    expect(sets()).toHaveLength(4);
    await user.click(sets()[0]!);
    await user.type(screen.getByRole('textbox', { name: 'The line for aurora' }), 'develop{Enter}');
    expect(acts.line).toHaveBeenLastCalledWith('develop');

    await user.click(sets()[0]!);
    const rule = screen.getByRole('radiogroup', { name: 'How work in aurora lands' });
    await user.click(within(rule).getByRole('radio', { name: 'Branch' }));
    await user.click(within(rule.closest('form')!).getByRole('button', { name: 'Save' }));
    expect(acts.landing).toHaveBeenLastCalledWith({ form: 'branch', pattern: 'feature/{quest}-{slug}' });
  });

  it('switches its reading off, as the terminal does', async () => {
    const { acts } = at();
    const user = userEvent.setup();
    await user.click(section('Defaults'));

    await user.click(screen.getAllByRole('button', { name: 'Set for this workspace' })[3]!);
    await user.click(within(screen.getByRole('radiogroup', { name: 'Reading across in aurora' })).getByRole('radio', { name: 'Off' }));
    expect(acts.read).toHaveBeenLastCalledWith(false);
  });

  it('leaves out a default the driver did not answer, rather than offer a save it would refuse', () => {
    at({ defaults: { line: { onSet: vi.fn() }, landing: null, language: null, read: null } });

    expect(section('Defaults')).toHaveAccessibleDescription("each checkout's own line (Daoris's default)");
  });

  /** D48 §5: wiring is done once per machine per deployment, so the form is one press away, and the key never lingers. */
  it('wires the workspace from a form one press away, and lets the key go once it has landed', async () => {
    const { acts } = at({ open: 'remote' });
    const user = userEvent.setup();

    expect(screen.getByText('Local only: nothing is wired for this workspace on this machine.')).toBeInTheDocument();
    expect(screen.queryByLabelText('Key')).toBeNull();
    await user.click(screen.getByRole('button', { name: 'Wire to a remote…' }));
    await user.type(screen.getByLabelText('Deployment'), 'https://aurora.example.com');
    await user.type(screen.getByLabelText('Key'), 'dk_aurorakey0000');
    await user.click(screen.getByRole('button', { name: 'Wire' }));

    expect(acts.wire).toHaveBeenCalledWith('https://aurora.example.com', 'dk_aurorakey0000', expect.any(Function));
    // Once the wiring has landed, the form closes and the key goes with it.
    acts.wire.mock.calls[0]![2]();
    expect(await screen.findByRole('button', { name: 'Wire to a remote…' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Wire to a remote…' }));
    expect(screen.getByLabelText('Key')).toHaveValue('');
  });

  /** A key goes in and never comes out: what is drawn is the prefix the wiring answered. */
  it('shows where it syncs with only its key\'s audit prefix, and unwires', async () => {
    const { acts } = at({
      remote: {
        wiring: { remote: { url: 'https://aurora.example.com', key: 'dk_abcd1234…' }, fromEnvironment: false, onWire: vi.fn(), onUnwire: vi.fn(() => acts.unwire()) },
        rules: null,
      },
    });

    expect(section('Remote and reach')).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByText('https://aurora.example.com')).toBeInTheDocument();
    expect(screen.getByText('dk_abcd1234…')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Unwire' }));
    expect(acts.unwire).toHaveBeenCalled();
  });

  it('says when the environment, not the file, names the remote', () => {
    at({
      open: 'remote',
      remote: { wiring: { remote: null, fromEnvironment: true, onWire: vi.fn(), onUnwire: vi.fn() }, rules: null },
    });

    expect(screen.getByText(/environment names this machine's remote/i)).toBeInTheDocument();
  });

  /** The header's *Wire to a remote…* opens Setup at its remote with the form open. */
  it('starts with the form open where the header asked to wire it', () => {
    at({ wiring: true });

    expect(section('Remote and reach')).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByLabelText('Deployment')).toBeInTheDocument();
  });

  /** PERM1 (D72): Claude Code's rules for the workspace, the same file `daoris agent rules … --workspace` edits. */
  it("lists, adds and removes Claude Code's rules for the workspace", async () => {
    const { acts } = at({
      remote: {
        wiring: null,
        rules: { lists: { allow: ['Bash(make:*)'], ask: [], deny: [] }, onAdd: (...args) => acts.add(...args), onRemove: (rule) => acts.remove(rule) },
      },
    });
    const user = userEvent.setup();

    // A rule of its own is the person's, so the section opens on it.
    expect(section('Remote and reach')).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByText(code('daoris agent rules allow|ask|deny|remove "<rule>" --workspace aurora'))).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'remove Bash(make:*)' }));
    expect(acts.remove).toHaveBeenLastCalledWith('Bash(make:*)');

    await user.click(screen.getByRole('button', { name: 'Add a rule' }));
    await user.type(screen.getByRole('textbox', { name: 'the rule' }), 'Bash(npm run test:*){Enter}');
    expect(acts.add).toHaveBeenLastCalledWith('allow', 'Bash(npm run test:*)', expect.any(Function));
  });

  it('says what still holds where the workspace adds no rule of its own', () => {
    at({ open: 'remote' });
    expect(screen.getByText("None of its own. This machine's still hold.")).toBeInTheDocument();
  });
});
