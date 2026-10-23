import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { StartWiring } from '../../map/wiring';
import { AskDaorisAbout, IntakeAbout } from './ActorAbout';

// MODELROLE1d (D205 §7): what Intake's and Ask Daoris's rows say under their order, their cards folded into the rows. Moved from
// the AI features card's tests (AGT6, INT4b, HELP1): tests follow their code.

const intakeStart = (extra: Partial<StartWiring> = {}): StartWiring => ({
  job: 'intake', workspace: 'default', adapter: 'claude-code-acp', owner: 'claude-code', product: 'Claude Code',
  profile: 'account-1', profileFrom: 'machine', version: '0.9.1', versionFrom: 'unset',
  commanded: false, refusal: null, ...extra,
});

const nameOf = (owner: string, profile?: string | null) => (profile ? `${profile} of ${owner}` : `the own account of ${owner}`);

describe("Intake's row", () => {
  it('says an intake that is off is answered by declarations', () => {
    render(<IntakeAbout agent={null} starts={[intakeStart()]} nameOf={nameOf} />);

    expect(screen.getByText(/answered from the workspace's declarations/)).toBeInTheDocument();
    expect(screen.queryByRole('listitem')).toBeNull();
  });

  /** Which account runs it is the driver's answer, per circle, named the way the roster names it. */
  it('says which account an intake in each circle runs as, and which setting chose it', () => {
    render(
      <IntakeAbout
        agent="claude-code-acp"
        starts={[intakeStart({ workspace: 'aurora', profile: 'account-2', profileFrom: 'workspace' }), intakeStart()]}
        nameOf={nameOf}
      />,
    );

    expect(screen.getByText(/opens a session on claude-code-acp/)).toBeInTheDocument();
    const aurora = screen.getByRole('listitem', { name: 'an intake in aurora' });
    expect(within(aurora).getByText('account-2 of claude-code')).toBeInTheDocument();
    expect(within(aurora).getByText("this workspace's default")).toBeInTheDocument();
    const circle = screen.getByRole('listitem', { name: 'an intake in default' });
    expect(within(circle).getByText("this machine's default")).toBeInTheDocument();
  });

  /** UX5 U53: the tool's own account by the roster's name for it, as every card on the page says it. */
  it("names the tool's own account as the roster does when no account is set anywhere", () => {
    render(<IntakeAbout agent="claude-code-acp" starts={[intakeStart({ profile: null, profileFrom: 'unset' })]} nameOf={nameOf} />);

    expect(screen.getByText('the own account of claude-code')).toBeInTheDocument();
  });

  /** D46 §3: a held intake says why in the driver's sentence, whole: it names what to run. */
  it('shows an intake the driver would hold, in its own words', () => {
    const refusal = "unknown adapter 'gone' — one of: claude-code, stub. An adapter is added deliberately, never guessed.";
    render(<IntakeAbout agent="gone" starts={[intakeStart({ adapter: 'gone', refusal })]} nameOf={nameOf} />);

    const circle = screen.getByRole('listitem', { name: 'an intake in default' });
    expect(within(circle).getByText('blocked')).toBeInTheDocument();
    expect(within(circle).getByText(refusal)).toBeInTheDocument();
  });
});

describe("Ask Daoris's row", () => {
  it('says what Ask Daoris does off, on an agent, and with full access', () => {
    const { rerender } = render(<AskDaorisAbout agent={null} />);
    expect(screen.getByText(/offers starters from what this machine lacks/)).toBeInTheDocument();

    rerender(<AskDaorisAbout agent="codex-acp" />);
    expect(screen.getByText(/runs on codex-acp: it reads and proposes/)).toBeInTheDocument();

    rerender(<AskDaorisAbout agent="codex-acp" full />);
    expect(screen.getByText(/runs on codex-acp with full access/)).toBeInTheDocument();
  });
});
