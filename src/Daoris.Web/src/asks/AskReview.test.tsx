import { type ReactElement, useState } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../locales/i18n';
import type { Ask } from '../data/api';
import type { Answered } from '../work/InlineConfirm';
import { NO_CARRY } from '../compose/carry';
import { AskComposer, type AskDraft } from './AskComposer';
import { AskPage } from './AskPage';
import { CLOSED, PROPOSED } from './fixtures';

// REVIEWENV1g (D154 point 3; the review environment design §1.5–§1.6): the person chooses the review of an ask's work in the
// composer and on its page; an intake's proposal is shown beside the choice and applies only on the person's press.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const choose = async (name: string, option: string) => {
  const user = userEvent.setup();
  screen.getByRole('combobox', { name }).focus();
  await user.keyboard('{Enter}');
  await user.click(await screen.findByRole('option', { name: option }));
};

function Composer({ onSubmit }: { onSubmit: (draft: AskDraft) => void }) {
  const [draft, setDraft] = useState<AskDraft>({ circle: '', sentence: 'Add the compare setting', to: '', ...NO_CARRY });
  return (
    <AskComposer
      draft={draft} onChange={setDraft} fixed="aurora" circles={['aurora']} receivers={['reports']}
      environments={['dev', 'local']} onSubmit={() => onSubmit(draft)} onCancel={() => {}}
    />
  );
}

const CHOSEN: Ask = {
  ...PROPOSED,
  reviewChoices: [{ choice: 'local', at: new Date(Date.now() - 3_600_000).toISOString(), words: 'run it locally against dev data' }],
};
const PROPOSING: Ask = {
  ...PROPOSED,
  reviewProposals: [{ choice: 'off', reason: 'Only the README changes, which has nothing to show.', at: '2026-10-09T09:00:00Z', quest: 'q1' }],
};

describe("the review choice for an ask's work", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it("leaves each repository's rule to decide by default, and sends a choice with the person's words", async () => {
    const onSubmit = vi.fn();
    const user = userEvent.setup();
    render(<Composer onSubmit={onSubmit} />);

    expect(screen.getByRole('combobox', { name: 'Review before it lands' })).toHaveTextContent("As each repository's rule says");
    expect(screen.queryByRole('textbox', { name: 'why, if you want to say' })).toBeNull();
    await choose('Review before it lands', 'Review it in local');
    await user.type(screen.getByRole('textbox', { name: 'why, if you want to say' }), 'run it locally');
    await user.click(screen.getByRole('button', { name: 'Ask' }));

    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ review: 'local', reviewWords: 'run it locally' }));
  });

  it("offers on and off where no shell says which environments are declared", async () => {
    render(
      <AskComposer
        draft={{ circle: '', sentence: '', to: '', ...NO_CARRY }} onChange={() => {}} fixed="aurora" circles={['aurora']}
        receivers={[]} onSubmit={() => {}} onCancel={() => {}}
      />,
    );
    const user = userEvent.setup();
    screen.getByRole('combobox', { name: 'Review before it lands' }).focus();
    await user.keyboard('{Enter}');

    expect((await screen.findAllByRole('option')).map((option) => option.textContent)).toEqual([
      "As each repository's rule says", 'Review it in the default environment', 'No review',
    ]);
  });

  it("says each repository's rule decides where nothing was chosen, and sets a choice on the ask's page", async () => {
    const onChooseReview = vi.fn();
    const user = userEvent.setup();
    render(<AskPage ask={PROPOSED} receivers={[]} questTitles={{}} onPublish={vi.fn()} onClose={vi.fn()} onOpenQuest={vi.fn()} environments={['local']} onChooseReview={onChooseReview} />);

    const section = screen.getByRole('region', { name: 'Review before it lands' });
    expect(section).toHaveTextContent("Nothing chosen here: each repository's rule decides whether its work is reviewed.");
    await choose('change the review choice', 'No review');
    await user.type(within(section).getByRole('textbox'), 'a typo');
    await user.click(within(section).getByRole('button', { name: 'Set' }));

    expect(onChooseReview).toHaveBeenCalledWith('off', 'a typo', expect.anything());
  });

  // ACCESSASK1 (the UI/UX design R3): a refused choice is said under its press with the choice and the words still as typed, and
  // an Apply refused under its proposal; neither is a toast.
  it('says a refused choice under Set, keeping what was typed, and an Apply refused under its proposal', async () => {
    const onChooseReview = vi.fn();
    const user = userEvent.setup();
    render(<AskPage ask={PROPOSING} receivers={[]} questTitles={{}} onPublish={vi.fn()} onClose={vi.fn()} onOpenQuest={vi.fn()} environments={['local']} onChooseReview={onChooseReview} />);
    const section = screen.getByRole('region', { name: 'Review before it lands' });

    await choose('change the review choice', 'Review it in local');
    await user.type(within(section).getByRole('textbox'), 'run it locally');
    await user.click(within(section).getByRole('button', { name: 'Set' }));
    act(() => (onChooseReview.mock.lastCall![2] as Answered).refused('The repository engine declares no environment local.'));

    const set = within(section).getByRole('button', { name: 'Set' }).parentElement!;
    expect(within(set).getByRole('alert')).toHaveTextContent('The repository engine declares no environment local.');
    expect(within(section).getByRole('textbox')).toHaveValue('run it locally');
    expect(within(section).getByRole('combobox', { name: 'change the review choice' })).toHaveTextContent('Review it in local');

    await user.click(within(section).getByRole('button', { name: 'Apply' }));
    expect(within(set).queryByRole('alert')).toBeNull();
    act(() => (onChooseReview.mock.lastCall![2] as Answered).refused('The ask is closed.'));
    const proposal = within(section).getByRole('button', { name: 'Apply' }).parentElement!;
    expect(within(proposal).getByRole('alert')).toHaveTextContent('The ask is closed.');

    // Landed, the choice's press starts again.
    await user.click(within(section).getByRole('button', { name: 'Set' }));
    act(() => (onChooseReview.mock.lastCall![2] as Answered).done());
    expect(within(section).queryByRole('alert')).toBeNull();
    expect(within(section).queryByRole('textbox')).toBeNull();
  });

  it('says the choice standing, with the words it was set on', () => {
    render(<AskPage ask={CHOSEN} receivers={[]} questTitles={{}} onPublish={vi.fn()} onClose={vi.fn()} onOpenQuest={vi.fn()} />);

    const section = screen.getByRole('region', { name: 'Review before it lands' });
    expect(section).toHaveTextContent('You chose a review in local for its work, 1h ago.');
    expect(section).toHaveTextContent('Your words: “run it locally against dev data”');
  });

  it("shows an intake's proposal with its reason, and applies it only on the person's press", async () => {
    const onChooseReview = vi.fn();
    const user = userEvent.setup();
    render(<AskPage ask={PROPOSING} receivers={[]} questTitles={{}} onPublish={vi.fn()} onClose={vi.fn()} onOpenQuest={vi.fn()} onChooseReview={onChooseReview} />);

    const section = screen.getByRole('region', { name: 'Review before it lands' });
    expect(section).toHaveTextContent('The intake proposed no review: Only the README changes, which has nothing to show.');
    expect(onChooseReview).not.toHaveBeenCalled();
    await user.click(within(section).getByRole('button', { name: 'Apply' }));

    expect(onChooseReview).toHaveBeenCalledWith('off', undefined, expect.anything());
  });

  it('offers no change on a closed ask', () => {
    render(<AskPage ask={{ ...CLOSED, reviewChoices: CHOSEN.reviewChoices }} receivers={[]} questTitles={{}} onPublish={vi.fn()} onClose={vi.fn()} onOpenQuest={vi.fn()} onChooseReview={vi.fn()} />);

    const section = screen.getByRole('region', { name: 'Review before it lands' });
    expect(within(section).queryByRole('button', { name: 'Set' })).toBeNull();
  });

  // REVIEWENV1j2 (D50): each screen that sets the choice names its terminal twin, quietly.
  it("names the terminal twin under the composer's review field, and on a live ask's page with its id", () => {
    render(<Composer onSubmit={vi.fn()} />);
    expect(document.body).toHaveTextContent('daoris-driver ask --review rule|on|<environment>|off "…"');
    expect(document.body).toHaveTextContent('--review-words "…"');
  });

  it("names the ask page's twin with the ask's id, on a live ask only", () => {
    const { unmount } = render(<AskPage ask={PROPOSED} receivers={[]} questTitles={{}} onPublish={vi.fn()} onClose={vi.fn()} onOpenQuest={vi.fn()} onChooseReview={vi.fn()} />);
    const section = screen.getByRole('region', { name: 'Review before it lands' });
    expect(section).toHaveTextContent(`daoris-driver ask --set-review ${PROPOSED.id} on|<environment>|off "…"`);
    unmount();

    render(<AskPage ask={{ ...CLOSED, reviewChoices: CHOSEN.reviewChoices }} receivers={[]} questTitles={{}} onPublish={vi.fn()} onClose={vi.fn()} onOpenQuest={vi.fn()} onChooseReview={vi.fn()} />);
    expect(screen.getByRole('region', { name: 'Review before it lands' })).not.toHaveTextContent('--set-review');
  });

  it('keeps the twins\' commands the same in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<AskPage ask={PROPOSED} receivers={[]} questTitles={{}} onPublish={vi.fn()} onClose={vi.fn()} onOpenQuest={vi.fn()} onChooseReview={vi.fn()} />);
    expect(screen.getByRole('region', { name: '落地前审阅' })).toHaveTextContent(`daoris-driver ask --set-review ${PROPOSED.id} on|<environment>|off "…"`);
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<AskPage ask={PROPOSING} receivers={[]} questTitles={{}} onPublish={vi.fn()} onClose={vi.fn()} onOpenQuest={vi.fn()} onChooseReview={vi.fn()} />);

    const section = screen.getByRole('region', { name: '落地前审阅' });
    expect(section).toHaveTextContent('受理提议不审阅');
    expect(within(section).getByRole('button', { name: '应用' })).toBeInTheDocument();
  });
});
