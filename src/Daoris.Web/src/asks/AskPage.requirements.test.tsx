import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { Ask } from '../data/api';
import i18n from '../locales/i18n';
import { AskPage } from './AskPage';
import { PUBLISHED } from './fixtures';

// An ask's page lists each requirement the person gave it, as a quest's page does: their words quoted, the check beside.

const page = (ask: Ask) => render(
  <Tooltip.Provider>
    <AskPage ask={ask} receivers={['engine']} questTitles={{}} onPublish={vi.fn()} onClose={vi.fn()} onDelete={vi.fn()} onOpenQuest={vi.fn()} />
  </Tooltip.Provider>,
);

const REQUIRED: Ask = {
  ...PUBLISHED,
  requirements: [
    { quote: 'the stall on load', check: 'a cold start reaches the menu', evidence: [] },
    { quote: 'no new setting', check: 'the settings page is unchanged', evidence: [] },
  ],
};

describe('an ask’s requirements on its page', () => {
  afterEach(async () => { cleanup(); await i18n.changeLanguage('en'); });

  it.each([
    ['en', 'What you required'],
    ['zh', '你的要求'],
  ])('lists each one numbered with its check, in %s, and says nothing of a done to answer', async (language, title) => {
    await i18n.changeLanguage(language);
    page(REQUIRED);

    const section = screen.getByRole('region', { name: title });
    const items = within(section).getAllByRole('listitem');
    expect(items).toHaveLength(2);
    expect(items[0]).toHaveTextContent('the stall on load');
    expect(items[0]).toHaveTextContent('a cold start reaches the menu');
    expect(items[1]).toHaveTextContent('no new setting');
    expect(section.textContent).not.toContain(i18n.t('quests.requirements.toAnswer'));
  });

  it('draws nothing for an ask with none, which is every older record', () => {
    page(PUBLISHED);
    expect(screen.queryByRole('region', { name: 'What you required' })).toBeNull();
  });
});
