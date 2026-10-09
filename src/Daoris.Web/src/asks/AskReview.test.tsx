import { type ReactElement, useState } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import type { Ask } from '../api';
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

    expect(onChooseReview).toHaveBeenCalledWith('off', 'a typo');
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

    expect(onChooseReview).toHaveBeenCalledWith('off');
  });

  it('offers no change on a closed ask', () => {
    render(<AskPage ask={{ ...CLOSED, reviewChoices: CHOSEN.reviewChoices }} receivers={[]} questTitles={{}} onPublish={vi.fn()} onClose={vi.fn()} onOpenQuest={vi.fn()} onChooseReview={vi.fn()} />);

    const section = screen.getByRole('region', { name: 'Review before it lands' });
    expect(within(section).queryByRole('button', { name: 'Set' })).toBeNull();
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<AskPage ask={PROPOSING} receivers={[]} questTitles={{}} onPublish={vi.fn()} onClose={vi.fn()} onOpenQuest={vi.fn()} onChooseReview={vi.fn()} />);

    const section = screen.getByRole('region', { name: '落地前审阅' });
    expect(section).toHaveTextContent('受理提议不审阅');
    expect(within(section).getByRole('button', { name: '应用' })).toBeInTheDocument();
  });
});
