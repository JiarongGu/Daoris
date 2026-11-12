import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// One Accept for an ask's lines of work, in both languages; a single run's Accept is its review page's.

import i18n from '../../locales/i18n';
import { EntryAccept, type EntryAcceptPlan } from './EntryAccept';

const PLAN: EntryAcceptPlan = {
  entry: 'ask:a3/engine', repository: 'engine', form: 'branch', target: 'feature/a3-fix-the-gap',
  parts: [
    { session: 's1', target: 'feature/a3-fix-the-gap', superseded: { by: 's2', says: 'CONFLICT (content): Merge conflict in app.cs.', conflicts: true } },
    { session: 's2', target: 'feature/a3-fix-the-gap', leftOut: [{ commit: '1a2b3c4d', subject: 'the fix', why: 'patch', on: '9f8e7d6c' }] },
  ],
};
const WEB: EntryAcceptPlan = { entry: 'ask:a3/web', repository: 'web', form: 'merge', target: 'main', parts: [{ session: 's9' }] };
const REFUSED = 'The branch mine/fix was made by someone else.';

function ask(over: Partial<Parameters<typeof EntryAccept>[0]> = {}) {
  const onLand = vi.fn();
  const onOpen = vi.fn();
  render(<EntryAccept offered={3} open plans={[PLAN, WEB]} onOpen={onOpen} onLand={onLand} {...over} />);
  return { onLand, onOpen };
}

describe("an ask's one Accept", () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('offers one press for every part with work, and opens the ask', async () => {
    const { onOpen } = ask({ open: false });

    await userEvent.click(screen.getByRole('button', { name: "Accept the ask's 3 parts…" }));

    expect(onOpen).toHaveBeenCalledWith(true);
  });

  it('names each repository, says where its parts land in order, what each leaves out and which part a later one superseded', () => {
    ask();

    const group = screen.getByRole('group', { name: "accept the ask's work" });
    expect(within(group).getByText('engine')).toBeInTheDocument();
    expect(within(group).getByText('web')).toBeInTheDocument();
    expect(within(group).getByText('Merges 1 part into main, one after another, in the order they ran.')).toBeInTheDocument();
    expect(within(group).getByText(/s1 looks superseded by s2/)).toHaveTextContent('It lands unless you leave it out.');
    expect(within(group).getByText('s2 leaves out 1 commit the line already holds.')).toBeInTheDocument();
  });

  it('lands a superseded part where the person chose nothing, and leaves it out where they chose to', async () => {
    const { onLand } = ask();

    await userEvent.click(screen.getByRole('button', { name: 'Accept all' }));
    expect(onLand).toHaveBeenLastCalledWith([], expect.anything());

    await userEvent.click(screen.getByRole('checkbox', { name: 'Leave s1 out' }));
    await userEvent.click(screen.getByRole('button', { name: 'Accept all' }));
    expect(onLand).toHaveBeenLastCalledWith(['s1'], expect.anything());
  });

  it('keeps the other repositories pressable where one is held by a gate or would be refused', () => {
    ask({
      plans: [
        { ...PLAN, parts: [PLAN.parts[0]!, { session: 's2', held: 'Its review waits on you.' }] },
        { ...WEB, parts: [{ session: 's9', problem: REFUSED }] },
      ],
    });

    expect(screen.getByText(/s2 is held/)).toHaveTextContent('s2 is held: Its review waits on you.');
    expect(screen.getByText(/held, so engine's work stays/)).toBeInTheDocument();
    expect(screen.getByText(/s9 would be refused now/)).toHaveTextContent(REFUSED);
    expect(screen.getByText(/would be refused, so web's work stays/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Accept all' })).toBeInTheDocument();
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    ask({ open: false });
    expect(screen.getByRole('button', { name: '采纳此需求的全部 3 段…' })).toBeInTheDocument();
  });
});
