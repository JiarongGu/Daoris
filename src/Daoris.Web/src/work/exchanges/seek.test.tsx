import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render } from '@testing-library/react';
import '../../locales/i18n';
import { WaitingOnLine } from '../atWork/AtWorkView';
import type { AtWorkA } from '../atWork/types';
import { LEAD_WAITING_ON_HELPER } from '../atWork/atWork.fixtures';
import { lineRowsOf } from './rows';
import { HELPER } from './rows.fixtures';
import { LineRow } from './LineRows';
import { toExchangeRow } from './seek';

afterEach(cleanup);

// The head's *Waiting on* door scrolls to the exchange's row in the timeline, its newest, and opens no view.

describe("the head's waiting-on door into the timeline", () => {
  it('names the first request the line waits on, and its press carries that request to the timeline', () => {
    const answer = (LEAD_WAITING_ON_HELPER as { answer: AtWorkA }).answer;
    const first = answer.exchanges!.find((exchange) => exchange.holds)!;
    const scrolled = vi.fn();
    Element.prototype.scrollIntoView = scrolled;
    render(<>{lineRowsOf({ exchanges: [{ ...HELPER, id: first.id }] }).map((row) => <LineRow key={row.key} row={row} attended="s1" docked={[]} onToDock={vi.fn()} />)}</>);
    const head = render(<WaitingOnLine answer={answer} onOpen={toExchangeRow} />);
    fireEvent.click(head.getByRole('button', { name: 'Show its row' }));

    expect(scrolled).toHaveBeenCalledOnce();
    // The exchange's newest row: its coming back, never its asking.
    expect(scrolled.mock.contexts[0]).toBe(document.querySelector(`[data-exchange-row="${first.id}"][data-side="back"]`));
  });

  it('brings nothing where the exchange has no row', () => {
    const scrolled = vi.fn();
    Element.prototype.scrollIntoView = scrolled;
    toExchangeRow('helper:none');
    expect(scrolled).not.toHaveBeenCalled();
  });
});
