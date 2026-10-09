import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { composeStories } from '@storybook/react-vite';
import { useState } from 'react';
import i18n from '../i18n';
import { Button, Icon, Menu } from '../ui';
import { type Answered, InlineConfirm } from './InlineConfirm';
import * as stories from './InlineConfirm.stories';

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

/** Records in a list, each with its own *Delete…*, whose delete takes its record away once it lands. */
function Records() {
  const [records, setRecords] = useState(['First', 'Second', 'Third']);
  const [asking, setAsking] = useState<string | null>(null);
  return (
    <ul aria-label="Quests">
      {records.map((record) => (
        <li key={record} aria-label={record}>
          {record}
          {asking !== record && <Button variant="danger" onClick={() => setAsking(record)}>Delete…</Button>}
          {asking === record && (
            <InlineConfirm
              label="delete this quest"
              says={SAYS}
              meanIt="Delete quest"
              onConfirm={(told) => {
                setRecords((was) => was.filter((each) => each !== record));
                told.done();
              }}
              onClose={() => setAsking(null)}
            />
          )}
        </li>
      ))}
    </ul>
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

  /**
   * UXFIX2d (seen on the install at 1546 px, dark): a workspace's *Clear history…* near the page's foot opened its ask with
   * the focus on its explanation, which scrolled only that line into view; its list and both presses stayed below the fold.
   * Opening brings the whole ask into view, nearest edge first, then gives the explanation the focus without scrolling again.
   */
  describe('opens whole in view (UXFIX2d)', () => {
    afterEach(() => { vi.restoreAllMocks(); });

    /** The ask's own scroll into view, and the explanation's focus, each as it was called and in which order. */
    function watch() {
      const scroll = vi.spyOn(Element.prototype, 'scrollIntoView');
      const focus = vi.spyOn(HTMLElement.prototype, 'focus');
      return {
        scrolled: (node: Element) => scroll.mock.calls.filter((_, i) => scroll.mock.instances[i] === node).map(([how]) => how),
        focused: (node: Element) => focus.mock.calls.filter((_, i) => focus.mock.instances[i] === node).map(([how]) => how),
        // Absent, a scroll comes after and a focus before everything, so the order fails rather than passes.
        order: (node: Element) => ({
          scroll: scroll.mock.invocationCallOrder.find((_, i) => scroll.mock.instances[i] === node) ?? Infinity,
          focus: focus.mock.invocationCallOrder.find((_, i) => focus.mock.instances[i] === node) ?? -Infinity,
        }),
      };
    }

    it('scrolls the whole ask into view on opening, then focuses its explanation without scrolling again', async () => {
      const seen = watch();
      render(<Page />);
      await userEvent.click(screen.getByRole('button', { name: 'Delete…' }));
      const ask = screen.getByRole('group', { name: 'delete this quest' });
      const told = within(ask).getByText(SAYS);
      await waitFor(() => expect(told).toHaveFocus());

      expect(seen.scrolled(ask)).toEqual([{ block: 'nearest' }]);
      expect(seen.focused(told)).toEqual([{ preventScroll: true }]);
      expect(seen.order(ask).scroll).toBeLessThan(seen.order(told).focus);
    });

    it('waits for a menu to close before it scrolls, and scrolls the ask, not the explanation', async () => {
      const user = userEvent.setup();
      render(<MenuPage />);
      screen.getByRole('button', { name: 'More actions' }).focus();
      await user.keyboard('{Enter}');
      const seen = watch();
      await user.click(screen.getByRole('menuitem', { name: 'Delete…' }));
      const ask = await screen.findByRole('group', { name: 'delete this quest' });
      const told = within(ask).getByText(SAYS);
      await waitFor(() => expect(told).toHaveFocus());

      expect(seen.scrolled(ask)).toEqual([{ block: 'nearest' }]);
      expect(seen.scrolled(told)).toEqual([]);
      expect(seen.focused(told)).toEqual([{ preventScroll: true }]);
      expect(seen.order(ask).scroll).toBeLessThan(seen.order(told).focus);
      // The menu gave the focus back to its trigger first, and the ask still takes it as what opened it.
      await user.keyboard('{Escape}');
      expect(screen.getByRole('button', { name: 'More actions' })).toHaveFocus();
    });
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

  /**
   * UXFIX2c (the second-opinion review, `InlineConfirm.tsx:175`): the press disabled both presses and moved the focus, and
   * nothing said the act had started. A status there from the first draw says it, politely; the move keeps its name and its
   * place. Nothing busy holds the status, since a reader may hold back what a busy element says until it is no longer busy,
   * which is after the act answered and the status went quiet: the waiting presses and the status are the wait's signs.
   */
  it('says its act started, politely, inside nothing busy, the move keeping its name', async () => {
    let answered: Answered | undefined;
    const user = userEvent.setup();
    render(<Page onConfirm={(told) => { answered = told; }} />);
    await user.click(screen.getByRole('button', { name: 'Delete…' }));
    const ask = screen.getByRole('group', { name: 'delete this quest' });
    // There before it speaks, so a reader hears it when it does.
    const status = within(ask).getByRole('status');
    expect(status).toBeEmptyDOMElement();

    await user.click(within(ask).getByRole('button', { name: 'Delete quest' }));
    expect(status).toHaveTextContent('working…');
    expect(status).toHaveAttribute('aria-live', 'polite');
    expect(status.closest('[aria-busy="true"]')).toBeNull();
    expect(within(ask).getByRole('button', { name: 'Delete quest' })).toBeDisabled();
    expect(within(ask).getByRole('button', { name: 'Never mind' })).toBeDisabled();

    act(() => answered!.refused('No.'));
    expect(status).toBeEmptyDOMElement();
  });

  it('says nothing started while only the page’s own act waits', () => {
    render(<InlineConfirm label="decline" says={SAYS} meanIt="Decline" busy onConfirm={() => {}} onClose={() => {}} />);
    expect(screen.getByRole('status')).toBeEmptyDOMElement();
  });

  /**
   * UXFIX2c (`InlineConfirm.tsx:163`): landed, the ask gave the focus back only to the press that opened it, which a page
   * that does not offer a press twice had taken away, so the focus fell to the page's body. It finds the press drawn again,
   * as a cancel does.
   */
  it('gives the focus back to the press drawn again once its act lands', async () => {
    const user = userEvent.setup();
    render(<Page onConfirm={(told) => told.done()} />);
    await user.click(screen.getByRole('button', { name: 'Delete…' }));
    await user.click(screen.getByRole('button', { name: 'Delete quest' }));
    expect(screen.queryByRole('group', { name: 'delete this quest' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Delete…' })).toHaveFocus();
  });

  it('gives the focus to the list it sat in where its act removed its own record, never the page’s body', async () => {
    const user = userEvent.setup();
    render(<Records />);
    const second = within(screen.getByRole('listitem', { name: 'Second' }));
    await user.click(second.getByRole('button', { name: 'Delete…' }));
    await user.click(second.getByRole('button', { name: 'Delete quest' }));

    expect(screen.queryByRole('listitem', { name: 'Second' })).toBeNull();
    // Not a neighbour's *Delete…*, which only shares the name.
    expect(screen.getByRole('list', { name: 'Quests' })).toHaveFocus();
    expect(document.activeElement).not.toBe(document.body);
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

  // IME1: a reason typed through an input method drops its composition on Escape, and the ask is still there.
  it('stays open on an Escape an input method is composing with in its field, and goes on a plain one', () => {
    const onClose = vi.fn();
    render(
      <InlineConfirm label="decline" says={SAYS} meanIt="Decline" onConfirm={() => {}} onClose={onClose}>
        <input aria-label="Why" />
      </InlineConfirm>,
    );
    const field = screen.getByRole('textbox', { name: 'Why' });

    expect(fireEvent.keyDown(field, { key: 'Escape', isComposing: true })).toBe(true);
    expect(fireEvent.keyDown(field, { key: 'Escape', keyCode: 229 })).toBe(true);
    expect(onClose).not.toHaveBeenCalled();

    fireEvent.keyDown(field, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledTimes(1);
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

  it('says never mind, and that its act started, in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<InlineConfirm label="删除这个委托" says="无法撤销。" meanIt="确认删除委托" onConfirm={() => {}} onClose={() => {}} />);
    const ask = screen.getByRole('group', { name: '删除这个委托' });
    expect(within(ask).getAllByRole('button').map((button) => button.textContent)).toEqual(['确认删除委托', '取消']);
    await userEvent.click(within(ask).getByRole('button', { name: '确认删除委托' }));
    expect(within(ask).getByRole('status')).toHaveTextContent('处理中…');
  });

  /**
   * UXFIX2c (`InlineConfirm.tsx:16`): a refusal kept its line breaks but could not break an unbroken token (a long id, a raw
   * error), so the token widened its flex or grid item past the main area's 400 px floor. It shrinks with its column and
   * breaks anywhere; the stories draw it at the floor in both themes.
   */
  it('wraps a refusal’s unbroken token inside the main area’s floor', () => {
    const { RefusedUnbrokenAtTheFloor, RefusedUnbrokenAtTheFloorDark } = composeStories(stories);
    for (const Story of [RefusedUnbrokenAtTheFloor, RefusedUnbrokenAtTheFloorDark]) {
      const { unmount } = render(<Story />);
      expect(screen.getByRole('alert')).toHaveClass('min-w-0', 'wrap-anywhere');
      unmount();
    }
  });
});
