import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../locales/i18n';
import { NO_CARRY } from '../compose/carry';
import { AskComposer } from './AskComposer';

const drawn = () => render(
  <Tooltip.Provider>
    <AskComposer
      draft={{ circle: '', sentence: '', to: '', ...NO_CARRY }} onChange={() => {}} fixed="aurora" circles={['aurora']}
      receivers={[]} onSubmit={() => {}} onCancel={() => {}}
    />
  </Tooltip.Provider>,
);

describe('the Ask drawer', () => {
  it('sets its subtitle in the body face, not an id’s monospace', () => {
    drawn();

    expect(screen.getByText(/kept by the service until it becomes quests/).className).not.toContain('font-mono');
  });

  it('puts the terminal twin at the foot of the form, after the last field', () => {
    drawn();

    const twin = screen.getByText((_, element) => element?.tagName === 'P' && /^At a terminal,/.test(element.textContent ?? ''));
    const last = screen.getByRole('button', { name: /Choose files/ });
    expect(last.compareDocumentPosition(twin) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });
});
