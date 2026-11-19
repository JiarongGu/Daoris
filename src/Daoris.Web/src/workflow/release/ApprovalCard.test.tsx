import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import '../../locales/i18n';
import { ApprovalCard } from './ApprovalCard';
import { approvalOf, canApproveHere } from './releaseTarget';
import { APPROVAL_PAGE, APPROVAL_WORDS } from '../runFixtures';

// The approval card: where the person holds the approver and the plugin can approve, *Approve* / *Reject* on the card under their own
// sign-in, the card naming the target, the build and its source branch; otherwise *Open* in Daoris's browser. Where an agent holds the
// approver, the plugin's command is its tool and the card offers no press.

const WORDS = 'Waiting on the approver: approve Production of Release-90, Portal (#42), carrying cccccccc from `main`; asked of Release Approvers.';
const URL = 'https://dev.azure.com/acme/Proj/_releaseProgress?releaseId=90&_a=release-pipeline-progress';

describe('the approval card', () => {
  it('names the target, the build and its branch, and approves under the holder\'s own press where the plugin can', () => {
    const onDecide = vi.fn().mockResolvedValue({ done: true });
    render(<ApprovalCard words={WORDS} url={URL} canApprove onDecide={onDecide} onOpen={vi.fn()} />);

    expect(screen.getByText(/Portal \(#42\), carrying cccccccc from/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));
    expect(onDecide).toHaveBeenCalledWith('approve', '');
    expect(screen.getByRole('button', { name: 'Reject' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Open' })).toBeNull();
  });

  it('opens the system\'s page in Daoris\'s browser where the plugin cannot approve', () => {
    const onOpen = vi.fn();
    render(<ApprovalCard words={WORDS} url={URL} canApprove={false} onDecide={vi.fn()} onOpen={onOpen} />);

    expect(screen.queryByRole('button', { name: 'Approve' })).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Open' }));
    expect(onOpen).toHaveBeenCalledWith(URL);
  });

  it('reads the release and the stage the approval asks of from the wait, and offers the press where the person holds the approver', () => {
    expect(approvalOf(WORDS, URL)).toEqual({ release: '90', stage: 'Production' });
    expect(approvalOf('Waiting on the deployer: start Test of Release-90 in Azure DevOps.', URL)).toBeNull();
    // Where an agent holds the approver, the card opens the page instead.
    expect(canApproveHere([{ name: 'approve-release', tool: false }], { kind: 'person' })).toBe(true);
    expect(canApproveHere([{ name: 'approve-release', tool: true }], { kind: 'person' })).toBe(true);
    expect(canApproveHere([{ name: 'approve-release', tool: true }], { kind: 'agent', name: 'builder-1' })).toBe(false);
    expect(canApproveHere([{ name: 'discover-releases', tool: true }], { kind: 'person' })).toBe(false);
  });

  it('reads them from a wait as an installed run kept it: the plugin’s words signed with its way, and the release’s summary page', () => {
    expect(approvalOf(APPROVAL_WORDS, APPROVAL_PAGE)).toEqual({ release: '90', stage: 'Prod' });
  });
});
