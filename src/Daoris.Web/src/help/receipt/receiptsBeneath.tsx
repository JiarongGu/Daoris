import type { ReactNode } from 'react';
import type { HelpPressNow } from '../../person/standing';
import { PressReceiptLine, type ReceiptScope } from './PressReceiptLine';

/** The presses of one conversation that made something, from a turn's words until the next turn's. */
export function receiptsOfTurn(presses: readonly HelpPressNow[] | undefined, session: string, from: string, until: string | undefined): HelpPressNow[] {
  const start = Date.parse(from);
  const end = until === undefined ? Number.POSITIVE_INFINITY : Date.parse(until);
  return (presses ?? []).filter((each) => {
    const at = Date.parse(each.at);
    return each.session === session && each.receipt !== undefined && at >= start && at < end;
  });
}

/**
 * What a conversation drew under each turn: the receipts of the presses it made in that turn, one line each, or nothing for a turn that opened
 * no place. The conversation's `beneath` slot is given this beside the receipt of what it looked at beyond its focus.
 */
export function receiptsBeneath(
  presses: readonly HelpPressNow[] | undefined, session: string, onOpen: ((scope: ReceiptScope, id: string) => void) | undefined,
): (ask: { at: string }, until: string | undefined) => ReactNode {
  return (ask, until) => {
    const held = receiptsOfTurn(presses, session, ask.at, until);
    if (held.length === 0) return null;
    return (
      <div data-press-receipts="" className="grid min-w-0 gap-0.5 border-l-2 border-line pl-2">
        {held.map((each) => <PressReceiptLine key={each.id} receipt={each.receipt!} {...(onOpen ? { onOpen } : {})} />)}
      </div>
    );
  };
}
