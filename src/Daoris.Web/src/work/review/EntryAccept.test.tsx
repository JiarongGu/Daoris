import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// ACCEPTALL1 (D167's 2026-10-15 amendment): one Accept for a line of work's parts and one for an ask's. The ask says where the
// parts land, in the order they ran, what each leaves out, which part a later one superseded, with the person's choice to leave it
// out, and what holds a part; a gate holding a part holds its whole line of work. In both languages.

import i18n from '../../locales/i18n';
import { EntryAccept, type EntryAcceptPlan } from './EntryAccept';

const PLAN: EntryAcceptPlan = {
  entry: 'ask:a3/engine', repository: 'engine', form: 'branch', target: 'feature/a3-fix-the-gap',
  parts: [
    { session: 's1', target: 'feature/a3-fix-the-gap', superseded: { by: 's2', says: 'CONFLICT (content): Merge conflict in app.cs.', conflicts: true } },
    { session: 's2', target: 'feature/a3-fix-the-gap', leftOut: [{ commit: '1a2b3c4d', subject: 'the fix', why: 'patch', on: '9f8e7d6c' }] },
  ],
};
const NUMBERS: Record<string, number> = { s1: 1, s2: 2, s3: 3 };
const numberOf = (session: string) => NUMBERS[session] ?? null;

function ask(over: Partial<Parameters<typeof EntryAccept>[0]> = {}) {
  const onLand = vi.fn();
  const onOpen = vi.fn();
  render(<EntryAccept scope="entry" offered={2} open plans={[PLAN]} numberOf={numberOf} onOpen={onOpen} onLand={onLand} {...over} />);
  return { onLand, onOpen };
}

describe("a line of work's one Accept", () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('offers one press for every part with work, and opens the ask', async () => {
    const { onOpen } = ask({ open: false });

    await userEvent.click(screen.getByRole('button', { name: 'Accept all 2 parts…' }));

    expect(onOpen).toHaveBeenCalledWith(true);
  });

  it('says where the parts land in order, what each leaves out, and which part a later one superseded', () => {
    ask();

    const group = screen.getByRole('group', { name: 'accept every part of this line of work' });
    expect(within(group).getByText('Lands 2 parts on feature/a3-fix-the-gap, one after another, in the order they ran.')).toBeInTheDocument();
    expect(within(group).getByText(/part 1 looks superseded by part 2/)).toHaveTextContent(
      'part 1 looks superseded by part 2: replayed after its work, it conflicts with it (CONFLICT (content): Merge conflict in app.cs.). It lands unless you leave it out.');
    expect(within(group).getByText('part 2 leaves out 1 commit the line already holds.')).toBeInTheDocument();
  });

  it('lands a superseded part where the person chose nothing', async () => {
    const { onLand } = ask();

    await userEvent.click(screen.getByRole('button', { name: 'Accept all' }));

    expect(onLand).toHaveBeenCalledWith([], expect.anything());
  });

  it('leaves a superseded part out where the person chose to', async () => {
    const { onLand } = ask();

    await userEvent.click(screen.getByRole('checkbox', { name: 'Leave part 1 out' }));
    expect(screen.getByText('Lands 1 part on feature/a3-fix-the-gap, one after another, in the order they ran.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Accept all' }));

    expect(onLand).toHaveBeenCalledWith(['s1'], expect.anything());
  });

  it('holds the whole line of work where a gate holds one part, and offers no press', () => {
    ask({ plans: [{ ...PLAN, parts: [PLAN.parts[0]!, { session: 's2', held: 'Its review waits on you.' }] }] });

    const group = screen.getByRole('group', { name: 'accept every part of this line of work' });
    expect(within(group).getByText(/part 2 is held/)).toHaveTextContent('part 2 is held: Its review waits on you.');
    expect(within(group).getByText(/what holds one part holds the whole line of work/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Accept all' })).toBeNull();
  });

  // STANDSREFUSE1b: a part's problem (a standing branch the press would refuse) is said, and holds the press.
  const REFUSED = 'The branch mine/fix was made by someone else.';

  it("says a part's refusal before the press and offers no move where the entry would be refused", () => {
    ask({ plans: [{ ...PLAN, parts: [PLAN.parts[0]!, { session: 's2', problem: REFUSED }] }] });

    const group = screen.getByRole('group', { name: 'accept every part of this line of work' });
    expect(within(group).getByText(/part 2 would be refused now/)).toHaveTextContent(`part 2 would be refused now: ${REFUSED}`);
    expect(within(group).getByText(/part that would be refused now holds the whole line of work/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Accept all' })).toBeNull();
  });

  it('lets a refused part that the person leaves out go', async () => {
    const { onLand } = ask({
      plans: [{ ...PLAN, parts: [{ ...PLAN.parts[0]!, problem: REFUSED }, PLAN.parts[1]!] }],
    });

    expect(screen.queryByRole('button', { name: 'Accept all' })).toBeNull();
    await userEvent.click(screen.getByRole('checkbox', { name: 'Leave part 1 out' }));
    await userEvent.click(screen.getByRole('button', { name: 'Accept all' }));

    expect(onLand).toHaveBeenCalledWith(['s1'], expect.anything());
  });

  it("keeps an ask's other repositories pressable where one entry would be refused", () => {
    ask({
      scope: 'ask',
      offered: 3,
      plans: [PLAN, { entry: 'ask:a3/web', repository: 'web', form: 'merge', target: 'main', parts: [{ session: 's9', problem: REFUSED }] }],
    });

    expect(screen.getByText(/s9 would be refused now/)).toBeInTheDocument();
    expect(screen.getByText(/would be refused, so web's work stays/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Accept all' })).toBeInTheDocument();
  });

  it("says a part's refusal in Chinese", async () => {
    await i18n.changeLanguage('zh');
    ask({ plans: [{ ...PLAN, parts: [{ session: 's2', problem: REFUSED }] }] });

    expect(screen.getByText(/第 2 段现在会被拒绝/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '全部采纳' })).toBeNull();
  });

  it('says that a part sharing a tree lands with the part whose tree it is, and a part on a branch of its own lands as it', () => {
    ask({ plans: [{ ...PLAN, parts: [{ session: 's1', target: 'mine/fix', own: true }, { session: 's3', sameTreeAs: 's1' }] }] });

    expect(screen.getByText('part 1 is on a branch of its own, mine/fix, and lands as it.')).toBeInTheDocument();
    expect(screen.getByText("part 3 shares part 1's tree, and lands with it.")).toBeInTheDocument();
  });

  it("names each repository in an ask's, whose press lands every entry and leaves a held one to itself", () => {
    ask({
      scope: 'ask',
      offered: 3,
      plans: [
        PLAN,
        { entry: 'ask:a3/web', repository: 'web', form: 'merge', target: 'main', parts: [{ session: 's9', held: 'Its review waits on you.' }] },
      ],
    });

    const group = screen.getByRole('group', { name: "accept the ask's work" });
    expect(within(group).getByText('engine')).toBeInTheDocument();
    expect(within(group).getByText('web')).toBeInTheDocument();
    expect(within(group).getByText('Merges 1 part into main, one after another, in the order they ran.')).toBeInTheDocument();
    expect(within(group).getByText(/held, so web's work stays/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Accept all' })).toBeInTheDocument();
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    ask({ open: false });
    expect(screen.getByRole('button', { name: '采纳全部 2 段…' })).toBeInTheDocument();
  });

  it('says the ask in Chinese', async () => {
    await i18n.changeLanguage('zh');
    ask();

    const group = screen.getByRole('group', { name: '采纳这条工作线的每一段' });
    expect(within(group).getByText('依次落在 feature/a3-fix-the-gap 上 2 段，按它们运行的顺序。')).toBeInTheDocument();
    expect(within(group).getByRole('checkbox', { name: '不采纳第 1 段' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '全部采纳' })).toBeInTheDocument();
  });
});
