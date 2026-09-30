import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { StartWiringList } from './StartWiring';
import type { StartWiring } from './wiring';

// MAP1b (D67 §3): what a start in each workspace would run on, and which setting chose each part —
// the driver's answer, drawn. The account's name is the roster's to give, so it arrives as a function.

const start = (extra: Partial<StartWiring> = {}): StartWiring => ({
  job: 'work', workspace: 'aurora', adapter: 'claude-code', owner: 'claude-code', product: 'Claude Code',
  profile: 'account-2', profileFrom: 'workspace', version: '2.1.270', versionFrom: 'machine',
  commanded: false, refusal: null, ...extra,
});

const nameOf = (owner: string, profile?: string | null) => (profile ? `${profile} of ${owner}` : `the own account of ${owner}`);

describe('what a start runs on', () => {
  it('names the agent, the account and the version, and which setting chose each', () => {
    render(<StartWiringList starts={[start()]} nameOf={nameOf} />);
    const row = screen.getByRole('listitem', { name: 'a start in aurora' });

    expect(within(row).getByText('ready')).toBeInTheDocument();
    expect(within(row).getByText('Claude Code')).toBeInTheDocument();
    expect(within(row).getByText('account-2 of claude-code')).toBeInTheDocument();
    expect(within(row).getByText("this workspace's default")).toBeInTheDocument();
    expect(within(row).getByText('2.1.270')).toBeInTheDocument();
    expect(within(row).getByText('pinned on this machine')).toBeInTheDocument();
  });

  /**
   * Nothing set is a real answer: the tool's own account, named as the roster names it on the same
   * page (UX5 U53, where it read "the agent's own sign-in"), and whatever PATH has.
   */
  it('names the tool\'s own account as the roster does, and PATH, when nothing is set', () => {
    render(<StartWiringList
      starts={[start({ profile: null, profileFrom: 'unset', versionFrom: 'unset', version: null })]}
      nameOf={nameOf}
    />);

    expect(screen.getByText('the own account of claude-code')).toBeInTheDocument();
    expect(screen.getByText('from PATH')).toBeInTheDocument();
    // Absent is never zero: a version nobody could ask is "not known", not blank.
    expect(screen.getByText('not known')).toBeInTheDocument();
  });

  it('says when driver.json names the command outright', () => {
    render(<StartWiringList starts={[start({ commanded: true, versionFrom: 'unset' })]} nameOf={nameOf} />);

    expect(screen.getByText('the command driver.json names')).toBeInTheDocument();
  });

  /** D46 §3: the driver's sentence names what to run, so it is shown whole and untranslated. */
  it('shows a held start with the driver\'s own sentence', () => {
    const refusal = '`claude-code` is pinned to 9.9.9 on this machine, and nothing is installed at that version.';
    render(<StartWiringList starts={[start({ refusal })]} nameOf={nameOf} />);

    expect(screen.getByText('blocked')).toBeInTheDocument();
    expect(screen.queryByText('ready')).toBeNull();
    expect(screen.getByText(refusal)).toBeInTheDocument();
  });

  it('lists every workspace it is given, each on its own row', () => {
    render(<StartWiringList starts={[start(), start({ workspace: 'default' })]} nameOf={nameOf} />);

    expect(screen.getAllByRole('listitem').map((row) => row.getAttribute('aria-label')))
      .toEqual(['a start in aurora', 'a start in default']);
    // One job is no job to tell apart: the rows do not name it.
    expect(screen.queryByText('sessions')).toBeNull();
  });

  /**
   * AGT6: once an agent is named for the intake, a circle has two jobs, and each row says which it
   * is — two rows reading "a start in aurora" with different agents would be one fact contradicting
   * itself.
   */
  it('names each row\'s job once the intake is one of them', () => {
    render(<StartWiringList
      starts={[start(), start({ job: 'intake', adapter: 'claude-code-acp' })]}
      nameOf={nameOf}
    />);

    const work = screen.getByRole('listitem', { name: 'a start in aurora' });
    const intake = screen.getByRole('listitem', { name: 'an intake in aurora' });
    expect(within(work).getByText('sessions')).toBeInTheDocument();
    expect(within(intake).getByText('intake')).toBeInTheDocument();
    expect(within(intake).getByText('claude-code-acp')).toBeInTheDocument();
  });
});
