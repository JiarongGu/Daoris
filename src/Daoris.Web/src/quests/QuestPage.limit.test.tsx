import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../locales/i18n';
import { FINISHED_HERE as FINISHED, TAKEN } from './fixtures';
import { QuestPage } from './QuestPage';

// LIMITRESUME1d2 (D206 §3.7): a quest whose session a limit cut off says on its page when it goes on by itself, and how, in the
// head's own sentence (`limitWaitLines`), joined the reader's way.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);
const LIMITED = { ...FINISHED, state: 'failed' as const, limit: true };

const page = (limitWait: readonly string[] | null) => render(
  <QuestPage quest={TAKEN} session={LIMITED} limitWait={limitWait} onRespond={vi.fn()} onDismiss={vi.fn()} onOpenQuest={vi.fn()} />,
);

describe('a quest whose session waits for a limit', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says when its session goes on by itself, and how, under its session', () => {
    page(['Waiting for a limit to reset: it resumes by itself at Oct 24, 11:02, on work.', 'It goes on in its own conversation.']);

    const section = within(screen.getByRole('region', { name: 'Session' }));
    expect(section.getByText('limited')).toBeInTheDocument();
    expect(section.getByText(
      'Waiting for a limit to reset: it resumes by itself at Oct 24, 11:02, on work. It goes on in its own conversation.',
    )).toBeInTheDocument();
  });

  it('says it in Chinese, its sentences joined with no space', async () => {
    await i18n.changeLanguage('zh');
    page(['正在等待额度重置：将于 10月24日 11:02 在 work 上自动继续。', '它将在原对话中继续。']);

    expect(screen.getByText('正在等待额度重置：将于 10月24日 11:02 在 work 上自动继续。它将在原对话中继续。')).toBeInTheDocument();
    expect(screen.getByText('额度用尽')).toBeInTheDocument();
  });

  it('says nothing more where no wait is handed', () => {
    page(null);
    expect(screen.queryByText(/Waiting for a limit/)).toBeNull();
  });
});
