import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import { ConversationView } from './ConversationView';
import { type SessionEvent, toTurns } from './conversation';

// A line a host draws above the person's words that carry its mark, found by the word's id on the record.

let seq = 0;
const ev = (over: Partial<SessionEvent>): SessionEvent => ({ seq: ++seq, at: '2026-09-25T00:00:00Z', kind: 'message', ...over });

describe('a line above the words that carry a mark', () => {
  it('is drawn above the person’s words whose id it is keyed by, and nowhere else', () => {
    const events = [
      ev({ kind: 'user', origin: 'person', id: 'w1', text: 'What blocks the release?' }),
      ev({ kind: 'message', text: 'The landing queue.' }),
      ev({ kind: 'turn', stopReason: 'end_turn' }),
      ev({ kind: 'user', origin: 'person', id: 'w2', text: 'And in its tests?' }),
      ev({ kind: 'message', text: 'They pass.' }),
      ev({ kind: 'turn', stopReason: 'end_turn' }),
    ];
    render(
      <Tooltip.Provider>
        <ConversationView turns={toTurns(events).turns} dividers={{ w2: <p>the divider</p> }} />
      </Tooltip.Provider>,
    );
    const divider = screen.getByText('the divider');
    expect(screen.getAllByText('the divider')).toHaveLength(1);
    const before = screen.getByText('The landing queue.');
    const after = screen.getByText('And in its tests?');
    expect(before.compareDocumentPosition(divider) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(divider.compareDocumentPosition(after) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });
});
