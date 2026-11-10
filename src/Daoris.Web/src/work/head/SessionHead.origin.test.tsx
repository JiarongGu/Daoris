import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import i18n from '../../locales/i18n';
import type { Quest, Session } from '../../data/api';
import { aQuest } from '../../quests/aQuest';
import { HELP_REPOSITORY } from '../../data/api';
import { SessionHead } from './SessionHead';

// Where a session's work came from and what it started with, by its kind: the head's line and *Details*' two rows.

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4',
  repository: 'engine',
  door: 'claude-code',
  state: 'working',
  kind: 'driven',
  workspace: 'aurora',
  created: '2026-09-21T09:46:00Z',
  updated: '2026-09-21T11:56:00Z',
  ...over,
});

const quest = (over: Partial<Quest> = {}): Quest => aQuest({
  id: '7a82cc', title: 'Expose a streaming budget', body: 'World streaming needs a per-frame cap.', status: 'Taken', ...over,
});

const details = () => fireEvent.click(screen.getByRole('button', { name: /^Details|^详情/ }));
const row = (label: string) => screen.getByText(label, { selector: 'dt' }).nextElementSibling as HTMLElement;

afterEach(() => i18n.changeLanguage('en'));

describe('where a session started from, and what it started with', () => {
  it('a driven session with requirements shows the quest as a press and its requirements whole', () => {
    const onOpenQuest = vi.fn();
    const held = quest({ requirements: [
      { quote: 'Cap hydration per frame', check: 'a test', evidence: [] },
      { quote: 'Say it in the log', check: 'a read', evidence: [] },
    ] });
    render(<SessionHead session={session({ quest: '7a82cc' })} quest={held} onOpenQuest={onOpenQuest} headed />);
    fireEvent.click(screen.getByRole('button', { name: 'Expose a streaming budget' }));
    expect(onOpenQuest).toHaveBeenCalledWith(expect.objectContaining({ id: '7a82cc' }));
    details();
    const started = row('Started with');
    expect(within(started).getByText("The quest's requirements")).toBeInTheDocument();
    expect(within(started).getByText('Cap hydration per frame')).toBeInTheDocument();
    expect(within(started).getByText('Say it in the log')).toBeInTheDocument();
  });

  it('a driven session whose quest has no requirements shows the quest’s words and says so', () => {
    render(<SessionHead session={session({ quest: '7a82cc' })} quest={quest({ requirements: [] })} headed />);
    details();
    const started = row('Started with');
    expect(within(started).getByText("The quest's words, which has no requirements")).toBeInTheDocument();
    expect(within(started).getByText('World streaming needs a per-frame cap.')).toBeInTheDocument();
  });

  it('a driven session also names the ask its line began in, a press that opens it', () => {
    const onAnswerAsk = vi.fn();
    render(
      <SessionHead
        session={session({ quest: '7a82cc' })} quest={quest()} ask={{ id: 'a1b2c3', words: 'Map the tags\nby organisation' }}
        onAnswerAsk={onAnswerAsk} headed
      />,
    );
    fireEvent.click(screen.getByRole('button', { name: 'Map the tags' }));
    expect(onAnswerAsk).toHaveBeenCalledWith('a1b2c3');
  });

  it('an intake shows its ask as a press and the ask’s words whole', () => {
    const onAnswerAsk = vi.fn();
    render(
      <SessionHead
        session={session({ quest: null, ask: 'a1b2c3', repository: 'intake', kind: 'chat' })}
        ask={{ id: 'a1b2c3', words: 'Map the tags by organisation' }} onAnswerAsk={onAnswerAsk} headed
      />,
    );
    details();
    fireEvent.click(within(row('Started from')).getByRole('button', { name: 'Map the tags by organisation' }));
    expect(onAnswerAsk).toHaveBeenCalledWith('a1b2c3');
    expect(within(row('Started with')).getByText("The ask's words")).toBeInTheDocument();
  });

  it('a conversation shows its repository and its first message', () => {
    render(<SessionHead session={session({ quest: null, kind: 'chat' })} opening="Why does the hydrator drop frames?" headed />);
    details();
    expect(within(row('Started from')).getByText('engine')).toBeInTheDocument();
    expect(within(row('Started with')).getByText('Why does the hydrator drop frames?')).toBeInTheDocument();
  });

  it('Ask Daoris shows its room and workspace, and claims no first message as a requirement', () => {
    render(<SessionHead session={session({ quest: null, kind: 'chat', repository: HELP_REPOSITORY })} opening="How do I add a repository?" headed />);
    details();
    const from = row('Started from');
    expect(within(from).getByText("Ask Daoris's room in")).toBeInTheDocument();
    expect(within(from).getByText('aurora')).toBeInTheDocument();
  });

  it('a hand-over shows the earlier session as a press that opens it', () => {
    const earlier = session({ id: 'a1b2c3d4', quest: null, kind: 'chat' });
    const onOpenSession = vi.fn();
    render(
      <SessionHead
        session={session({ quest: null, kind: 'chat', from: 'a1b2c3d4', fromWhy: 'near' })}
        continues={{ session: earlier, title: 'Why the hydrator drops frames' }} onOpenSession={onOpenSession} headed
      />,
    );
    fireEvent.click(screen.getByRole('button', { name: 'Why the hydrator drops frames' }));
    expect(onOpenSession).toHaveBeenCalledWith(earlier);
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<SessionHead session={session({ quest: '7a82cc' })} quest={quest({ requirements: [] })} headed />);
    details();
    expect(row('起点')).toBeInTheDocument();
    expect(within(row('起始要求')).getByText('委托的原话（它没有要求）')).toBeInTheDocument();
  });
});
