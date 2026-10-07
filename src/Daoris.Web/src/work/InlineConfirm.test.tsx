import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import i18n from '../i18n';
import { Button, Icon, Menu } from '../ui';
import { type Answered, InlineConfirm } from './InlineConfirm';

// The one inline confirmation for a destructive act (UXFIX2; the second-opinion review's cross-cutting note), as a molecule:
// inline, never a modal; its explanation takes the focus on opening and describes the move; the move first, then *Never
// mind*; Escape and *Never mind* put it down and give the focus back to what opened it; the press keeps it open and waiting
// until its act answers, a refusal said inside it word for word, and it closes only on success or a cancel. ACCTEDIT1's
// `answered.done/refused` is the contract.

const SAYS = 'This removes the quest from the ledger. It cannot be undone.';

/** A page holding one ask the way the real ones do: its first press is not offered twice while it asks. */
function Page({ onConfirm = () => {}, opener = 'removed' }: {
  onConfirm?: (answered: Answered) => void;
  /** Whether the first press stays drawn while it asks (*Stop…*) or goes (*Delete…*, *Discard branch…*). */
  opener?: 'removed' | 'kept';
}) {
  const [asking, setAsking] = useState(false);
  return (
    <div>
      <Button>Before</Button>
      {(opener === 'kept' || !asking) && <Button variant="danger" onClick={() => setAsking(true)}>Delete…</Button>}
      {asking && (
        <InlineConfirm label="delete this quest" says={SAYS} meanIt="Delete quest" onConfirm={onConfirm} onClose={() => setAsking(false)} />
      )}
    </div>
  );
}

/** The same ask opened from a ⋯ menu, whose item goes with the menu: the focus comes back to the menu's trigger. */
function MenuPage() {
  const [asking, setAsking] = useState(false);
  return (
    <div>
      <Menu.Root>
        <Menu.Trigger asChild>
          <Button variant="ghost" aria-label="More actions"><Icon name="more" size={15} /></Button>
        </Menu.Trigger>
        <Menu.Content>
          <Menu.Acts acts={[{ id: 'delete', label: 'Delete…', onSelect: () => setAsking(true) }]} />
        </Menu.Content>
      </Menu.Root>
      {asking && (
        <InlineConfirm label="delete this quest" says={SAYS} meanIt="Delete quest" onConfirm={() => {}} onClose={() => setAsking(false)} />
      )}
    </div>
  );
}

describe('an inline confirmation', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('is a group in the page, never a dialog, with the move first and then never mind', async () => {
    render(<Page />);
    await userEvent.click(screen.getByRole('button', { name: 'Delete…' }));
    const ask = screen.getByRole('group', { name: 'delete this quest' });
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(within(ask).getAllByRole('button').map((button) => button.textContent)).toEqual(['Delete quest', 'Never mind']);
  });

  it('moves the focus to its explanation on opening, and the explanation describes the move', async () => {
    render(<Page />);
    await userEvent.click(screen.getByRole('button', { name: 'Delete…' }));
    const ask = screen.getByRole('group', { name: 'delete this quest' });
    await waitFor(() => expect(within(ask).getByText(SAYS)).toHaveFocus());
    expect(within(ask).getByRole('button', { name: 'Delete quest' })).toHaveAccessibleDescription(SAYS);
  });

  it('is put down by Never mind, and gives the focus back to the press that opened it, drawn again', async () => {
    const user = userEvent.setup();
    render(<Page />);
    await user.click(screen.getByRole('button', { name: 'Delete…' }));
    await user.click(screen.getByRole('button', { name: 'Never mind' }));
    expect(screen.queryByRole('group', { name: 'delete this quest' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Delete…' })).toHaveFocus();
  });

  it('is put down by Escape, and gives the focus back to a press that stayed drawn', async () => {
    const user = userEvent.setup();
    render(<Page opener="kept" />);
    await user.click(screen.getByRole('button', { name: 'Delete…' }));
    await waitFor(() => expect(screen.getByText(SAYS)).toHaveFocus());
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('group', { name: 'delete this quest' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Delete…' })).toHaveFocus();
  });

  it('gives the focus back to the menu’s trigger when a menu’s item opened it', async () => {
    const user = userEvent.setup();
    render(<MenuPage />);
    screen.getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    await user.click(screen.getByRole('menuitem', { name: 'Delete…' }));
    const ask = await screen.findByRole('group', { name: 'delete this quest' });
    await waitFor(() => expect(within(ask).getByText(SAYS)).toHaveFocus());
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('group', { name: 'delete this quest' })).toBeNull();
    expect(screen.getByRole('button', { name: 'More actions' })).toHaveFocus();
  });

  it('stays open and waits while its act is on its way, then closes once it lands', async () => {
    let answered: Answered | undefined;
    const user = userEvent.setup();
    render(<Page onConfirm={(told) => { answered = told; }} />);
    await user.click(screen.getByRole('button', { name: 'Delete…' }));
    await user.click(screen.getByRole('button', { name: 'Delete quest' }));

    const ask = screen.getByRole('group', { name: 'delete this quest' });
    expect(within(ask).getByRole('button', { name: 'Delete quest' })).toBeDisabled();
    expect(within(ask).getByRole('button', { name: 'Never mind' })).toBeDisabled();
    // Escape waits too, so a refusal is never said to nobody.
    await user.keyboard('{Escape}');
    expect(screen.getByRole('group', { name: 'delete this quest' })).toBeInTheDocument();

    act(() => answered!.done());
    await waitFor(() => expect(screen.queryByRole('group', { name: 'delete this quest' })).toBeNull());
  });

  it('says a refusal inside itself, word for word, stays open, and lets the move be pressed again', async () => {
    let answered: Answered | undefined;
    const confirm = vi.fn((told: Answered) => { answered = told; });
    const user = userEvent.setup();
    render(<Page onConfirm={confirm} />);
    await user.click(screen.getByRole('button', { name: 'Delete…' }));
    await user.click(screen.getByRole('button', { name: 'Delete quest' }));
    act(() => answered!.refused('Quest #abc123 was taken on another machine, so it was not deleted. Look again.'));

    const ask = screen.getByRole('group', { name: 'delete this quest' });
    const alert = await within(ask).findByRole('alert');
    expect(alert).toHaveTextContent('Quest #abc123 was taken on another machine, so it was not deleted. Look again.');
    const move = within(ask).getByRole('button', { name: 'Delete quest' });
    expect(move).toBeEnabled();
    expect(move).toHaveAccessibleDescription(`${SAYS} Quest #abc123 was taken on another machine, so it was not deleted. Look again.`);

    // Pressed again, the last refusal goes while the new answer is on its way.
    await user.click(move);
    expect(confirm).toHaveBeenCalledTimes(2);
    expect(within(ask).queryByRole('alert')).toBeNull();
  });

  it('closes on a cancel after a refusal, and an answer to the ask put down changes nothing', async () => {
    let answered: Answered | undefined;
    const close = vi.fn();
    const user = userEvent.setup();
    const { unmount } = render(
      <InlineConfirm label="delete this quest" says={SAYS} meanIt="Delete quest" onConfirm={(told) => { answered = told; }} onClose={close} />,
    );
    await user.click(screen.getByRole('button', { name: 'Delete quest' }));
    act(() => answered!.refused('No.'));
    await user.click(await screen.findByRole('button', { name: 'Never mind' }));
    expect(close).toHaveBeenCalledOnce();

    unmount();
    answered!.done();
    expect(close).toHaveBeenCalledOnce();
  });

  it('still lets go once its act lands where the page drew it away while it waited', async () => {
    let answered: Answered | undefined;
    const close = vi.fn();
    const { unmount } = render(
      <InlineConfirm label="stop this session" says={SAYS} meanIt="Stop session" onConfirm={(told) => { answered = told; }} onClose={close} />,
    );
    await userEvent.click(screen.getByRole('button', { name: 'Stop session' }));
    unmount();
    answered!.refused('No one to say it to.');
    answered!.done();
    expect(close).toHaveBeenCalledOnce();
  });

  it('waits on the page’s own busy, and on what the move needs first', () => {
    const { rerender } = render(
      <InlineConfirm label="decline" says={SAYS} meanIt="Decline" busy onConfirm={() => {}} onClose={() => {}} />,
    );
    expect(screen.getByRole('button', { name: 'Decline' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Never mind' })).toBeDisabled();
    rerender(<InlineConfirm label="decline" says={SAYS} meanIt="Decline" ready={false} onConfirm={() => {}} onClose={() => {}} />);
    expect(screen.getByRole('button', { name: 'Decline' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Never mind' })).toBeEnabled();
  });

  it('holds a rich body, all of it describing the move', () => {
    render(
      <InlineConfirm
        block
        label="clear from this machine"
        says={(
          <>
            <p>Clears #9a8b7c.</p>
            <ul aria-label="What goes"><li>4 closed quests</li></ul>
          </>
        )}
        meanIt="Clear 4"
        onConfirm={() => {}}
        onClose={() => {}}
      />,
    );
    expect(screen.getByRole('list', { name: 'What goes' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Clear 4' })).toHaveAccessibleDescription('Clears #9a8b7c. 4 closed quests');
  });

  it('offers only Close where there is nothing to confirm', async () => {
    const close = vi.fn();
    render(<InlineConfirm block label="clear from this machine" says="Nothing here can be cleared now." onClose={close} />);
    const ask = screen.getByRole('group', { name: 'clear from this machine' });
    expect(within(ask).getAllByRole('button').map((button) => button.textContent)).toEqual(['Close']);
    await userEvent.click(within(ask).getByRole('button', { name: 'Close' }));
    expect(close).toHaveBeenCalledOnce();
  });

  it('says never mind in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<InlineConfirm label="删除这个委托" says="无法撤销。" meanIt="确认删除委托" onConfirm={() => {}} onClose={() => {}} />);
    expect(within(screen.getByRole('group', { name: '删除这个委托' })).getAllByRole('button').map((button) => button.textContent))
      .toEqual(['确认删除委托', '取消']);
  });
});
