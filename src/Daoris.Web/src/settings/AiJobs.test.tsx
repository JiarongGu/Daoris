import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import { AiJobs, type IntakeJob, type SearchTier } from './AiJobs';
import type { StartWiring } from '../map/wiring';

// AGT6: Daoris's own AI on the Settings page — each job it may use a model for, the tier that answers
// it now, and how to change it (D24, `model-decoupling`). The tier is the service's own words; the
// intake's control is this machine's, and absent where there is no machine to set (D50, D47 §4).

const LEXICAL: SearchTier = {
  tier: 'lexical only', semantic: false,
  note: 'Set DAORIS_EMBED_MODEL to enable semantic recall — it is what finds two repositories that reached the same conclusion in different words.',
};

const intakeStart = (extra: Partial<StartWiring> = {}): StartWiring => ({
  job: 'intake', workspace: 'default', adapter: 'claude-code-acp', owner: 'claude-code', product: 'Claude Code',
  profile: 'account-1', profileFrom: 'machine', version: '0.9.1', versionFrom: 'unset',
  commanded: false, refusal: null, ...extra,
});

const intake = (extra: Partial<IntakeJob> = {}): IntakeJob => ({
  adapter: null,
  agents: [
    { value: 'claude-code', label: 'Claude Code — claude-code' },
    { value: 'claude-code-acp', label: 'Claude Code — claude-code-acp' },
  ],
  starts: [],
  nameOf: (owner, profile) => (profile ? `${profile} of ${owner}` : `the own account of ${owner}`),
  onChange: () => {},
  ...extra,
});

const show = (props: Parameters<typeof AiJobs>[0]) =>
  render(<Tooltip.Provider><AiJobs {...props} /></Tooltip.Provider>);

describe('AI features', () => {
  /** D24: which tier answers is said in the service's own words, beside what changes it. */
  it('states the search tier and its note verbatim, and how the model is chosen', () => {
    show({ search: LEXICAL });

    expect(screen.getByText('lexical only')).toBeInTheDocument();
    expect(screen.getByText(LEXICAL.note!)).toBeInTheDocument();
    // How to change it: the service's environment, read when it starts — never a control that
    // pretends to set it.
    // Each variable is code, as every other row's terminal door is (UX5 U55: they were body text).
    expect(screen.getByText('DAORIS_EMBED_MODEL').tagName).toBe('CODE');
    expect(screen.getByText('DAORIS_EMBED_URL').tagName).toBe('CODE');
    expect(screen.getByText(/restart/)).toBeInTheDocument();
  });

  it('claims no tier before the service has said one', () => {
    show({});

    expect(screen.queryByText('lexical only')).toBeNull();
    expect(screen.getByText('not yet known')).toBeInTheDocument();
  });

  /** A browser may be told the tier, and nothing of a machine (D47 §4): the intake is absent. */
  it('has no intake where it is given none — a browser', () => {
    show({ search: LEXICAL });

    expect(screen.queryByText('Intake')).toBeNull();
    expect(screen.queryByRole('combobox')).toBeNull();
  });

  it('says an intake that is off is answered by declarations, with the terminal twin beside it', () => {
    show({ search: LEXICAL, intake: intake() });

    expect(screen.getByRole('combobox', { name: 'the intake agent' })).toHaveTextContent('Off — declarations only');
    expect(screen.getByText(/answered from the workspace's declarations/)).toBeInTheDocument();
    expect(screen.getByText(/daoris driver intake <agent>\|off/)).toBeInTheDocument();
  });

  /**
   * D50: the screen's half of `daoris driver intake`. Off is a choice, sent as none — the same
   * absence the terminal's `off` writes.
   */
  it('names an agent for the intake, and turns it off again, through the one callback', async () => {
    const onChange = vi.fn();
    const { rerender } = show({ search: LEXICAL, intake: intake({ onChange }) });

    const user = userEvent.setup();
    screen.getByRole('combobox', { name: 'the intake agent' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('option', { name: 'Claude Code — claude-code-acp' }));
    expect(onChange).toHaveBeenLastCalledWith('claude-code-acp');

    rerender(<Tooltip.Provider><AiJobs search={LEXICAL} intake={intake({ adapter: 'claude-code-acp', onChange })} /></Tooltip.Provider>);
    const again = userEvent.setup();
    screen.getByRole('combobox', { name: 'the intake agent' }).focus();
    await again.keyboard('{Enter}');
    await again.click(await screen.findByRole('option', { name: 'Off — declarations only' }));
    expect(onChange).toHaveBeenLastCalledWith(null);
  });

  /** HELP1 (D89): Ask Daoris's own agent, a third job, off until named — and absent where it is given none. */
  it('names an agent for Ask Daoris on its own control, off by default', async () => {
    const onChange = vi.fn();
    show({ search: LEXICAL, helper: { adapter: null, agents: [{ value: 'codex-acp', label: 'Codex — codex-acp' }], onChange } });

    expect(screen.getByRole('combobox', { name: 'the Ask Daoris agent' })).toHaveTextContent('Off — starters only');
    expect(screen.getByText(/offers starters from what this machine lacks/)).toBeInTheDocument();
    const user = userEvent.setup();
    screen.getByRole('combobox', { name: 'the Ask Daoris agent' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('option', { name: 'Codex — codex-acp' }));
    expect(onChange).toHaveBeenLastCalledWith('codex-acp');
  });

  it('has no Ask Daoris row where it is given none', () => {
    show({ search: LEXICAL });
    expect(screen.queryByRole('combobox', { name: 'the Ask Daoris agent' })).toBeNull();
  });

  /** Which account runs it is the driver's answer, per circle, named the way the roster names it. */
  it('says which account an intake in each circle runs as, and which setting chose it', () => {
    show({
      search: LEXICAL,
      intake: intake({
        adapter: 'claude-code-acp',
        starts: [intakeStart({ workspace: 'aurora', profile: 'account-2', profileFrom: 'workspace' }), intakeStart()],
      }),
    });

    expect(screen.getByText(/opens a session on claude-code-acp/)).toBeInTheDocument();
    const aurora = screen.getByRole('listitem', { name: 'an intake in aurora' });
    expect(within(aurora).getByText('account-2 of claude-code')).toBeInTheDocument();
    expect(within(aurora).getByText("this workspace's default")).toBeInTheDocument();
    const circle = screen.getByRole('listitem', { name: 'an intake in default' });
    expect(within(circle).getByText("this machine's default")).toBeInTheDocument();
  });

  /** UX5 U53: the tool's own account by the roster's name for it, as every card on the page says it. */
  it('names the tool\'s own account as the roster does when no account is set anywhere', () => {
    show({ intake: intake({ adapter: 'claude-code-acp', starts: [intakeStart({ profile: null, profileFrom: 'unset' })] }) });

    expect(screen.getByText('the own account of claude-code')).toBeInTheDocument();
  });

  /** D46 §3: a held intake says why in the driver's sentence, whole — it names what to run. */
  it('shows an intake the driver would hold, in its own words', () => {
    const refusal = "unknown adapter 'gone' — one of: claude-code, stub. An adapter is added deliberately, never guessed.";
    show({ intake: intake({ adapter: 'gone', starts: [intakeStart({ adapter: 'gone', refusal })] }) });

    const circle = screen.getByRole('listitem', { name: 'an intake in default' });
    expect(within(circle).getByText('blocked')).toBeInTheDocument();
    expect(within(circle).getByText(refusal)).toBeInTheDocument();
  });

  /**
   * An agent named from the terminal that this screen would not offer — a plugin's door since
   * removed, a name typed by hand — is still what is in effect, so the control shows it rather than
   * reading "off" over a machine that is not.
   */
  it('shows an agent the list does not hold as what is in effect, rather than as off', () => {
    show({ intake: intake({ adapter: 'stub' }) });

    expect(screen.getByRole('combobox', { name: 'the intake agent' })).toHaveTextContent('stub');
  });
});
