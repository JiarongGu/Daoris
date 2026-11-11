import { afterEach, describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import i18n from '../../locales/i18n';
import type { Session } from '../../data/api';
import { aQuest } from '../../quests/aQuest';
import { SessionRow } from './SessionRow';

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: '7a82cc', repository: 'engine', door: 'claude-code', state: 'working', kind: 'driven',
  created: '2026-09-21T09:46:00Z', updated: '2026-09-21T11:56:00Z', ...over,
});

afterEach(() => i18n.changeLanguage('en'));

describe('a line holding several quests', () => {
  it('names the quest its row’s session holds, so a duplicate is told apart', () => {
    render(<SessionRow session={session()} quest={aQuest({ id: '7a82cc', title: 'Map the tags by organisation' })} parts={3} quests={2} />);
    expect(screen.getByText(/holds Map the tags by organisation/)).toBeInTheDocument();
  });

  it('names it by id where the frame does not hold the quest', () => {
    render(<SessionRow session={session()} parts={2} quests={2} />);
    expect(screen.getByText(/holds #7a82cc/)).toBeInTheDocument();
  });

  it('says nothing of it where the line holds one quest', () => {
    render(<SessionRow session={session()} parts={2} quests={1} />);
    expect(screen.queryByText(/holds/)).toBeNull();
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<SessionRow session={session()} parts={2} quests={2} />);
    expect(screen.getByText(/持有 #7a82cc/)).toBeInTheDocument();
  });
});
