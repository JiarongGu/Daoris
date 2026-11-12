import type { ReactNode } from 'react';
import { standingOf, useDriver } from '../../bridge/shell';
import type { StarterDoor } from '../starters';
import { goDoorOf } from './goDoorOf';
import { receiptsBeneath } from './receiptsBeneath';

/**
 * What the conversation's presses made, drawn under the turn that pressed them: the run's press record the driver tells every window
 * (`standingConfirmation.helpPresses`), the receipts of this conversation's own, each going to the place it made through the window's door.
 */
export function useHelpReceipts(session: string | null, onGo: ((door: StarterDoor) => void) | undefined): (ask: { at: string }, until: string | undefined) => ReactNode {
  const presses = standingOf(useDriver().data?.standingConfirmation)?.helpPresses;
  return receiptsBeneath(presses, session ?? '', onGo ? (scope, id) => onGo(goDoorOf(scope, id)) : undefined);
}
