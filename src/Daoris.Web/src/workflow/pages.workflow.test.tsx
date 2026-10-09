import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import { AskPage } from '../asks/AskPage';
import { PUBLISHED } from '../asks/fixtures';
import { QuestPage } from '../quests/QuestPage';
import { WORK } from '../work/reviewFixtures';
import { IN_REVIEW, PULL_REQUEST_OPEN, SEVERAL } from './runFixtures';

// WORKFLOW1c (the workflow design §7): a quest's page and an ask's carry their work's run in one line among their head's facts,
// *Workflow: waits for your look*, whose door opens the run; an ask's work in several repositories says each run apart.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

describe("a quest's page", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says where its work stands in its workflow among its facts, and its door opens the run', async () => {
    const onOpen = vi.fn();
    render(
      <QuestPage quest={WORK} onRespond={vi.fn()} onDismiss={vi.fn()} onOpenQuest={vi.fn()} workflow={{ runs: [IN_REVIEW], onOpen }} />,
    );

    const line = screen.getByRole('button', { name: 'Workflow: waits for your look' });
    expect(line.closest('header')).not.toBeNull();
    // Among the head's facts: the facts said first, then the run's line.
    expect(line.closest('p')).toHaveTextContent(/^to reports · .* · Workflow: waits for your look$/);
    await userEvent.click(line);
    expect(onOpen).toHaveBeenCalledWith(IN_REVIEW);
  });

  it('says nothing of a workflow where no run was read, as in a browser', () => {
    render(<QuestPage quest={WORK} onRespond={vi.fn()} onDismiss={vi.fn()} onOpenQuest={vi.fn()} />);
    expect(screen.queryByText(/^Workflow:/)).toBeNull();
  });

  it('is worded in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<QuestPage quest={WORK} onRespond={vi.fn()} onDismiss={vi.fn()} onOpenQuest={vi.fn()} workflow={{ runs: [PULL_REQUEST_OPEN], onOpen: vi.fn() }} />);
    expect(screen.getByRole('button', { name: '工作流：已落地；拉取请求已开启' })).toBeInTheDocument();
  });
});

describe("an ask's page", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  const page = (workflow?: Parameters<typeof AskPage>[0]['workflow']) => render(
    <AskPage
      ask={PUBLISHED} receivers={['engine']} questTitles={{}} onPublish={vi.fn()} onClose={vi.fn()} onOpenQuest={vi.fn()}
      workflow={workflow}
    />,
  );

  it("says each of its runs apart in its head, each a door into its own", async () => {
    const onOpen = vi.fn();
    page({ runs: SEVERAL.runs, onOpen });

    await userEvent.click(screen.getByRole('button', { name: 'game: landed; pull request open' }));
    expect(onOpen).toHaveBeenCalledWith(SEVERAL.runs[1]);
    expect(screen.getByRole('button', { name: 'engine: waits for your look' })).toBeInTheDocument();
  });

  it('says nothing where its work has no run yet', () => {
    page({ runs: [] });
    expect(screen.queryByText(/waits for/)).toBeNull();
  });
});
