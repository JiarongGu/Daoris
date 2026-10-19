import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import { afterEach, describe, expect, it, vi } from 'vitest';

// ACCEPTALL1g (D167's 2026-10-15 amendment point 2, D145 §3): where Accept moves on a branch that already stands (an entry's or a
// chain's, which an earlier landing made), the review says so, from the plan's `stands`, instead of promising a new branch.

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

const SAYS = {
  en: {
    stands: 'Accepting adds this work to feature/0fda18-fix-the-gap, a branch that already stands, moving it on for you to push to grow its pull request. Nothing is merged.',
    standsPlugin: 'Accepting adds this work to feature/0fda18-fix-the-gap, a branch that already stands, moving it on; then the plugin example.lands pushes it to grow its pull request. Nothing is merged.',
    made: 'Accepting puts this work on a new branch, feature/0fda18-fix-the-gap, for you to push and open a pull request from. Nothing is merged.',
    own: 'Accepting lands feature/x, the session\'s own branch, as it stands, for you to push and open a pull request from. No other branch is made, and nothing is merged.',
    ownPlugin: 'Accepting lands feature/x, the session\'s own branch, as it stands; then the plugin example.lands pushes it and opens the pull request. No other branch is made, and nothing is merged.',
    newBranch: /new branch/,
  },
  zh: {
    stands: '采纳会把这份工作加到已有的分支 feature/0fda18-fix-the-gap 上，使它前移，由你推送以扩充它的拉取请求。不会合并任何内容。',
    standsPlugin: '采纳会把这份工作加到已有的分支 feature/0fda18-fix-the-gap 上，使它前移，随后由插件 example.lands 推送，扩充它的拉取请求。不会合并任何内容。',
    made: '采纳会把这份工作放到新分支 feature/0fda18-fix-the-gap 上，由你推送并从它发起拉取请求。不会合并任何内容。',
    own: '采纳会按原样落地会话自己的分支 feature/x，由你推送并从它发起拉取请求。不会另建分支，也不会合并任何内容。',
    ownPlugin: '采纳会按原样落地会话自己的分支 feature/x，随后由插件 example.lands 推送并发起拉取请求。不会另建分支，也不会合并任何内容。',
    newBranch: /新分支/,
  },
} as const;

/** The foot's line saying where Accept puts the work. */
const where = (text: string) => screen.findByText((_content, element) => element?.tagName === 'P' && element.textContent === text);

describe.each(['en', 'zh'] as const)('where the review says Accept puts the work, in %s', (language) => {
  afterEach(async () => {
    invoke.mockReset();
    await i18n.changeLanguage('en');
  });

  it('says a branch that already stands moves on, and promises no new branch', async () => {
    await i18n.changeLanguage(language);
    answer({ ...PLAN, stands: true });
    pane();

    expect(await where(SAYS[language].stands)).toBeInTheDocument();
    expect(screen.queryByText(SAYS[language].newBranch)).toBeNull();
  });

  it('says so with the plugin that pushes it', async () => {
    await i18n.changeLanguage(language);
    answer({ ...PLAN, stands: true, plugin: 'example.lands' });
    pane();

    expect(await where(SAYS[language].standsPlugin)).toBeInTheDocument();
  });

  it('says a new branch as before where none stands, or a shell older than the field answers none', async () => {
    await i18n.changeLanguage(language);
    answer({ ...PLAN, stands: false });
    pane();

    expect(await where(SAYS[language].made)).toBeInTheDocument();
  });

  /**
   * STANDSREFUSE1 (D167's ACCEPTALL1g note): a standing branch the press would refuse (one Daoris did not make, one moved since,
   * one checked out elsewhere) is said by the plan before the press, in the press's words, and *Accept*, which could only be
   * refused, is not offered as a press.
   */
  it('says the refusal a standing branch would meet before the press, and holds Accept', async () => {
    await i18n.changeLanguage(language);
    const refusal = '`feature/0fda18-fix-the-gap` is already a branch in `engine`, and Daoris does not move a branch it did not make. '
      + 'Rename or delete it there, or change the pattern with `daoris driver landing`.';
    answer({ ...PLAN, stands: true, problem: refusal });
    pane();

    expect(await screen.findByText(/Daoris does not move a branch it did not make/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: language === 'en' ? 'Accept' : '采纳' })).toBeDisabled();
  });

  /** TREEFOLLOWWEB1 (D164's TREEFOLLOW1 note): a tree its agent moved onto a branch of its own lands that branch as it stands. */
  it('says the session’s own branch lands as it stands, and promises no new branch', async () => {
    await i18n.changeLanguage(language);
    answer({ ...PLAN, target: 'feature/x', own: true });
    pane();

    expect(await where(SAYS[language].own)).toBeInTheDocument();
    expect(screen.queryByText(SAYS[language].newBranch)).toBeNull();
  });

  it('says so with the plugin that pushes it', async () => {
    await i18n.changeLanguage(language);
    answer({ ...PLAN, target: 'feature/x', own: true, plugin: 'example.lands' });
    pane();

    expect(await where(SAYS[language].ownPlugin)).toBeInTheDocument();
  });
});
