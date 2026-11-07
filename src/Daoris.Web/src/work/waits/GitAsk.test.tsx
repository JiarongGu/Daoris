import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import '../../locales/i18n';
import { waitsOf } from './waits';
import { GitAsk, gitAsksFor, type GitAskRow } from './GitAsk';
import type { Session } from '../../data/api';

// A git go-ahead drawn on its session's head and foot, from the same pushed rows the band
// draws, answered Allow or Refuse through the one door the frame builds.

const ASK: GitAskRow = {
  id: '0123456789abcdef0123456789abcdef', since: new Date(Date.now() - 60_000).toISOString(),
  gitAsk: { repository: 'engine', remote: 'origin', host: 'github.com', class: 'write', session: 's1', gone: false },
};

describe("a session's git go-ahead", () => {
  it('is one of its waits, its own alone, where a door answers it', () => {
    const session = { id: 's1', state: 'working' } as Session;
    const other: GitAskRow = { ...ASK, id: 'f'.repeat(32), gitAsk: { ...ASK.gitAsk, session: 's2' } };
    expect(gitAsksFor([ASK, other], 's1')).toEqual([ASK]);
    expect(waitsOf({ session, gitAsks: { waits: [ASK, other], onAnswer: vi.fn() } })).toEqual([{ kind: 'gitAsk', id: ASK.id }]);
    expect(waitsOf({ session, gitAsks: { waits: [ASK] } })).toEqual([]);
  });

  it('says who asks for what and answers Allow or Refuse, a refusal under the presses', async () => {
    const onAnswer = vi.fn((_ask: GitAskRow, allow: boolean, answered: { done: () => void; refused: (said: string) => void }) => {
      if (allow) answered.done();
      else answered.refused('the go-ahead has gone');
    });
    render(<GitAsk ask={ASK} onAnswer={onAnswer} />);

    expect(screen.getByText('s1 asks to push to origin')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Allow' }));
    expect(onAnswer).toHaveBeenLastCalledWith(ASK, true, expect.anything());
    await userEvent.click(screen.getByRole('button', { name: 'Refuse' }));
    expect(await screen.findByText('the go-ahead has gone')).toBeInTheDocument();
  });
});
