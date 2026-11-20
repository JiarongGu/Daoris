import { afterEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import type { Session } from '../../data/api';
import { HeadChips } from './HeadChips';
import { PanelHead } from './PanelHead';
import { HEAD } from './session.fixtures';

// The panel's head wears the chips the record carries, last on its where-and-who line: a supervisor's count, a later choice, a
// full-access start; and nothing where none holds.

const FULL = { permissions: 'unrestricted', routing: 'intake', landing: 'rule', goAheads: 'never', questions: 'claude-code' };
const SOME = { permissions: 'unrestricted', routing: 'person', landing: 'rule', goAheads: 'named', questions: 'claude-code' };
const HELD = { count: 'answers-alone', n: 2, at: 2, held: true, text: 'answers alone in a row 2 of 2' };

const head = (session: Partial<Session>, supervision: typeof HELD | null = null) => render(
  <Tooltip.Provider>
    <PanelHead head={HEAD} now={Date.parse(HEAD.since)} chips={<HeadChips session={session} supervision={supervision} />} />
  </Tooltip.Provider>,
);
const line = () => screen.getByRole('banner').querySelector('p')!;

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage('en');
});

describe("the panel head's chips", () => {
  it('wears full access only where every pause the record holds is open', () => {
    head({ access: FULL });
    expect(within(line()).getByText('Full access').closest('[data-access="full"]')).not.toBeNull();
    cleanup();
    head({ access: SOME });
    expect(line().querySelector('[data-head-chips]')).toBeNull();
  });

  it('wears choice n before the access chip, and your pick for a pick', () => {
    head({ access: FULL, choice: '2' });
    const chip = within(line()).getByText('choice 2');
    expect(chip.closest('[data-choice="2"]')).not.toBeNull();
    expect(chip.compareDocumentPosition(within(line()).getByText('Full access')) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    cleanup();
    head({ choice: 'pick' });
    expect(within(line()).getByText('your pick')).toBeInTheDocument();
  });

  it('wears the count a rule holds the line at, held, first of its chips', () => {
    head({ choice: '2' }, HELD);
    const chip = within(line()).getByText('answers alone in a row 2 of 2');
    expect(chip.getAttribute('data-supervision-chip')).toBe('held');
    expect(chip.compareDocumentPosition(within(line()).getByText('choice 2')) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('says its chips in 中文', async () => {
    await i18n.changeLanguage('zh');
    head({ access: FULL, choice: '2' });
    expect(within(line()).getByText('选择 2')).toBeInTheDocument();
  });
});
