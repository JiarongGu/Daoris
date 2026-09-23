import { type ReactElement, useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { NO_CARRY } from '../compose/carry';
import { AskCard } from './AskCard';
import { AskComposer, type AskDraft } from './AskComposer';
import { AskRecord } from './AskRecord';
import { CLOSED, NAMED, PROPOSED, PUBLISHED, REFUSED, UNKNOWN_TIER, UNMATCHED } from './fixtures';

// An ask's three molecules (INT4c), props in and states out: no service and no shell, so every state
// the record can be in is an ordinary assertion (components §2).

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const record = (ask = PROPOSED, over: Partial<Parameters<typeof AskRecord>[0]> = {}) => {
  const props = {
    ask, receivers: ['engine', 'game', 'lantern'], questTitles: {},
    onPublish: vi.fn(), onClose: vi.fn(), onOpenQuest: vi.fn(), onDismiss: vi.fn(), ...over,
  };
  render(<AskRecord {...props} />);
  return { ...props, drawer: screen.getByRole('dialog') };
};

describe('AskCard', () => {
  it('leads with its state and its first line, and says what answered and what it proposed', () => {
    render(<AskCard ask={PROPOSED} onOpen={() => {}} />);

    expect(screen.getByText('proposed')).toBeInTheDocument();
    expect(screen.getByText('The chunk streamer stalls on a cold cache — cap its hydration per frame.')).toBeInTheDocument();
    expect(screen.getByText(/by declarations/)).toBeInTheDocument();
    expect(screen.getByText(/proposed engine, game/)).toBeInTheDocument();
  });

  it('names the quests an ask became, and opens on a press or a key', () => {
    const onOpen = vi.fn();
    render(<AskCard ask={PUBLISHED} onOpen={onOpen} />);

    expect(screen.getByText(/became #9a8b7c, #1f2e3d/)).toBeInTheDocument();
    fireEvent.keyDown(screen.getByRole('button'), { key: 'Enter' });
    expect(onOpen).toHaveBeenCalledWith(PUBLISHED);
  });
});

describe('AskRecord', () => {
  /** The sentence the tier writes about itself is what a person reads: which tier answered, never implied. */
  it('says which tier answered, in the words for it, beside the ask\'s own words whole', () => {
    const { drawer } = record(PROPOSED);
    expect(within(drawer).getByText('by declarations only; no intake harness ran')).toBeInTheDocument();
    expect(within(drawer).getByText(PROPOSED.sentence, { normalizer: (s) => s })).toBeInTheDocument();
  });

  it('shows a tier it has no word for as the service wrote it', () => {
    const { drawer } = record(UNKNOWN_TIER);
    expect(within(drawer).getByText('intake-session')).toBeInTheDocument();
  });

  it('offers each proposal as a publish, and publishes to the one pressed', async () => {
    const { drawer, onPublish } = record(PROPOSED);

    expect(within(drawer).getByText(/chunk, stream, frame/)).toBeInTheDocument();
    await userEvent.click(within(drawer).getByRole('button', { name: 'publish to engine' }));

    expect(onPublish).toHaveBeenCalledWith('engine');
  });

  it('says when no declarations share its words, and still offers any receiver in its circle', async () => {
    const { drawer, onPublish } = record(UNMATCHED);

    expect(within(drawer).getByText(/No repository's declarations share its words/)).toBeInTheDocument();
    const user = userEvent.setup();
    within(drawer).getByRole('combobox', { name: 'publish to another' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('option', { name: 'lantern' }));
    await user.click(within(drawer).getByRole('button', { name: 'publish' }));

    expect(onPublish).toHaveBeenCalledWith('lantern');
  });

  it('lists the quests it became and opens the one chosen, by its title where the page holds it', async () => {
    const { drawer, onOpenQuest } = record(PUBLISHED, { questTitles: { '9a8b7c6d5e4f': 'Cap the hydration' } });

    await userEvent.click(within(drawer).getByRole('button', { name: /Cap the hydration/ }));
    expect(onOpenQuest).toHaveBeenCalledWith('9a8b7c6d5e4f');
  });

  it('names a quest the page does not hold yet, and offers no door that would open nothing', () => {
    // Just after a publish, the answer names the quest before the list has asked again for it.
    const { drawer } = record(PUBLISHED, { questTitles: { '9a8b7c6d5e4f': 'Cap the hydration' } });

    expect(within(drawer).getByText('#1f2e3d')).toBeInTheDocument();
    expect(within(drawer).queryByRole('button', { name: /#1f2e3d/ })).toBeNull();
  });

  it('carries the named receiver\'s refusal verbatim, and keeps its proposal to go on with', () => {
    const { drawer } = record(REFUSED);

    expect(within(drawer).getByText(REFUSED.note!)).toBeInTheDocument();
    expect(within(drawer).getByRole('button', { name: 'publish to engine' })).toBeInTheDocument();
  });

  it('closes with a reason, and not without one', async () => {
    const { drawer, onClose } = record(PROPOSED);

    await userEvent.click(within(drawer).getByRole('button', { name: 'close the ask' }));
    const confirm = within(drawer).getByRole('button', { name: 'close with this reason' });
    expect(confirm).toBeDisabled();
    await userEvent.type(within(drawer).getByLabelText('why — what became of it'), 'Answered elsewhere.');
    await userEvent.click(confirm);

    expect(onClose).toHaveBeenCalledWith('Answered elsewhere.');
  });

  /** A closed ask becomes nothing more (INT4a): its reason stays, and no verb is offered. */
  it('offers nothing to do on a closed ask, and says why it closed', () => {
    const { drawer } = record(CLOSED);

    expect(within(drawer).getByText('Answered in the design review instead.')).toBeInTheDocument();
    expect(within(drawer).queryByRole('button', { name: /publish/ })).toBeNull();
    expect(within(drawer).queryByRole('button', { name: 'close the ask' })).toBeNull();
  });

  it('names its files and never the path this machine keeps them at', () => {
    const { drawer } = record(PROPOSED);

    expect(within(drawer).getByText('trace.log')).toBeInTheDocument();
    expect(drawer.textContent).not.toContain('somewhere');
    expect(within(drawer).getByRole('link', { name: /tickets\.example/ })).toHaveAttribute('href', 'https://tickets.example/T-42');
  });

  it('says a named receiver answered, and offers publishing it further', () => {
    const { drawer } = record(NAMED);

    expect(within(drawer).getByText('the asker named the receiver')).toBeInTheDocument();
    expect(within(drawer).getByRole('combobox', { name: 'publish to another' })).toBeInTheDocument();
  });
});

describe('AskComposer', () => {
  function Holder({ fixed, circles, onSubmit = () => {} }: {
    fixed: string | null; circles: string[]; onSubmit?: (draft: AskDraft) => void;
  }) {
    const [draft, setDraft] = useState<AskDraft>({ circle: '', sentence: '', to: '', ...NO_CARRY });
    return (
      <AskComposer
        draft={draft} onChange={setDraft} fixed={fixed} circles={circles} receivers={['engine', 'game']}
        onSubmit={() => onSubmit(draft)} onCancel={() => {}}
      />
    );
  }

  it('is made in the circle the page is scoped to, and asks for its words before it will send', async () => {
    const onSubmit = vi.fn();
    render(<Holder fixed="aurora" circles={['aurora']} onSubmit={onSubmit} />);
    const drawer = screen.getByRole('dialog');

    expect(within(drawer).getByText('Asked in aurora')).toBeInTheDocument();
    expect(within(drawer).queryByRole('combobox', { name: 'circle' })).toBeNull();
    const send = within(drawer).getByRole('button', { name: 'ask' });
    expect(send).toBeDisabled();

    await userEvent.type(within(drawer).getByLabelText('what is wanted, and why'), 'Cap the hydration.');
    await userEvent.click(send);
    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ sentence: 'Cap the hydration.', to: '' }));
  });

  /** "Every circle" with several held has no single circle: the person says which, and it is not assumed. */
  it('asks which circle when the page is scoped to none of several', async () => {
    render(<Holder fixed={null} circles={['aurora', 'tools']} />);
    const drawer = screen.getByRole('dialog');

    await userEvent.type(within(drawer).getByLabelText('what is wanted, and why'), 'Cap the hydration.');
    expect(within(drawer).getByRole('button', { name: 'ask' })).toBeDisabled();

    const user = userEvent.setup();
    within(drawer).getByRole('combobox', { name: 'circle' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('option', { name: 'tools' }));

    expect(within(drawer).getByRole('button', { name: 'ask' })).toBeEnabled();
  });

  it('carries links and files like the quest composer, from the same fields', async () => {
    render(<Holder fixed="aurora" circles={['aurora']} />);
    const drawer = screen.getByRole('dialog');

    await userEvent.upload(within(drawer).getByLabelText('choose files…'), new File(['x'], 'trace.log'));
    expect(within(drawer).getByText('trace.log')).toBeInTheDocument();
    expect(within(drawer).getByText(/links — a ticket/)).toBeInTheDocument();
  });
});
