import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import { afterEach, describe, expect, it, vi } from 'vitest';

// ACCEPTALL1c (D167's 2026-10-15 amendment point 3): a part the person left out of its entry's Accept is recorded on its conversation,
// and its review reads it from the plan (`leftOutOf`, the branch its line of work landed on): it says so, and *Discard tree* leads,
// with *Accept* after it, still a press of its own for landing it later.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: () => {},
}));

import i18n from '../../locales/i18n';
import { DiffPane } from './DiffPane';

const FILES = [{ path: 'src/report/compare.ts', status: 'modified', added: 4, removed: 1, patch: '@@ -1 +1,2 @@\n-a\n+b' }];
const DIFF = { session: 's1a2b3c4', base: '0123456789abcdef', truncated: null, files: FILES, source: 'tree' };
const PLAN = { session: 's1a2b3c4', form: 'branch', target: 'feature/0fda18-fix-the-gap', source: 'workspace' };

function answer(plan: unknown) {
  invoke.mockImplementation(async (_module: string, type: string) => {
    if (type === 'SESSION_DIFF') return DIFF;
    if (type === 'HANDOFF_PLAN') return { session: 's1a2b3c4', branch: null };
    if (type === 'LANDING') return plan;
    return {};
  });
}

function pane() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <DiffPane session="s1a2b3c4" hasTree onSendBack={vi.fn()} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

/** Whether the first button comes before the second in the foot. */
const before = (first: HTMLElement, second: HTMLElement) => Boolean(first.compareDocumentPosition(second) & Node.DOCUMENT_POSITION_FOLLOWING);

const SAYS = {
  en: { says: /You left this part out when its line of work landed on/, accept: 'Accept', discard: 'Discard tree' },
  zh: { says: /这条工作线落在.*上时，你没有采纳这一段/, accept: '采纳', discard: '丢弃工作树' },
} as const;

describe.each(['en', 'zh'] as const)('the review of a part left out of its entry’s Accept, in %s', (language) => {
  afterEach(async () => {
    invoke.mockReset();
    await i18n.changeLanguage('en');
  });

  it('says it was left out, and leads with Discard tree, Accept after it', async () => {
    await i18n.changeLanguage(language);
    answer({ ...PLAN, leftOutOf: 'feature/0fda18-fix-the-gap' });
    pane();

    expect(await screen.findByText(SAYS[language].says)).toHaveTextContent('feature/0fda18-fix-the-gap');
    const discard = screen.getByRole('button', { name: SAYS[language].discard });
    const accept = screen.getByRole('button', { name: SAYS[language].accept });
    expect(before(discard, accept)).toBe(true);
    expect(discard.className).toContain('bg-accent');
    expect(accept.className).not.toContain('bg-accent');
  });

  it('leads with Accept as before where no note says it was left out', async () => {
    await i18n.changeLanguage(language);
    answer(PLAN);
    pane();

    const accept = await screen.findByRole('button', { name: SAYS[language].accept });
    const discard = screen.getByRole('button', { name: SAYS[language].discard });
    expect(before(accept, discard)).toBe(true);
    expect(accept.className).toContain('bg-accent');
    expect(screen.queryByText(SAYS[language].says)).toBeNull();
  });
});
