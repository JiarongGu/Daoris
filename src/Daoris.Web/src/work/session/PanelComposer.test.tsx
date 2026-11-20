import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../../locales/i18n';
import { Composer } from '../conversation/Composer';
import { PanelComposer } from './PanelComposer';

// The composer at the panel's foot: the session's box over a line naming the model a default resolves to and when the agent reads
// the words. The box keeps every press the session's composer offers.

const model = { id: 'model', name: 'Model', category: 'model', current: 'opus', choices: [{ value: 'opus', name: 'Opus' }, { value: 'sonnet', name: 'Sonnet' }] };

function panelBox(overrides: Partial<Parameters<typeof Composer>[0]> = {}) {
  const props = {
    live: true, onSend: vi.fn(), onFinish: vi.fn(), onStopTurn: vi.fn(), onRemove: vi.fn(async () => ({ done: true })),
    onEdit: vi.fn(async () => ({ done: true })), onLevel: vi.fn(async () => ({ done: true })), onOption: vi.fn(),
    ...overrides,
  };
  render(
    <Tooltip.Provider>
      <PanelComposer reads="next" chip="Claude · opus-4.1">
        <Composer {...props} />
      </PanelComposer>
    </Tooltip.Provider>,
  );
  return props;
}

describe('PanelComposer', () => {
  it('names the agent and its model and says when the agent reads the words', () => {
    const { rerender } = render(<PanelComposer reads="next" chip="Claude · opus-4.1"><span /></PanelComposer>);
    expect(screen.getByText('It reads your words after the current step.')).toBeTruthy();
    expect(screen.getByText('Claude · opus-4.1')).toBeTruthy();
    rerender(<PanelComposer reads="back" chip="Claude · opus-4.1"><span /></PanelComposer>);
    expect(screen.getByText('It reads your words when the session goes on.')).toBeTruthy();
  });

  it('sends the words with Enter, files beside them, and finishes the conversation', () => {
    const props = panelBox();
    expect(screen.getByRole('button', { name: 'Attach files' })).toBeTruthy();
    const box = screen.getByRole('textbox', { name: 'Message' });
    fireEvent.change(box, { target: { value: 'Keep the burst at 20.' } });
    fireEvent.keyDown(box, { key: 'Enter' });
    expect(props.onSend.mock.calls[0]?.slice(0, 2)).toEqual(['Keep the burst at 20.', []]);
    fireEvent.click(screen.getByRole('button', { name: 'Finish' }));
    expect(props.onFinish).toHaveBeenCalled();
  });

  it('offers @ from the session tree while a mention is written', async () => {
    const onMentioning = vi.fn();
    panelBox({ mentions: { files: ['src/burst.ts', 'README.md'], unlisted: 0, refusal: null }, onMentioning });
    await userEvent.type(screen.getByRole('textbox', { name: 'Message' }), '@bur');
    expect(onMentioning).toHaveBeenCalledWith(true);
    expect(screen.getByRole('option', { name: 'src/burst.ts' })).toBeTruthy();
  });

  it('edits, removes and levels a waiting word, and stops the turn keeping what waits', async () => {
    const props = panelBox({
      taking: true, stoppable: true, levels: { nextStep: true, steers: true, stops: true },
      queued: [{ id: 'w1', text: 'Then run the tests.', files: [] }],
    });
    expect(screen.getByRole('button', { name: 'Edit the waiting message: Then run the tests.' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'When it reads: Then run the tests.' })).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Remove the waiting message: Then run the tests.' }));
    await vi.waitFor(() => expect(props.onRemove).toHaveBeenCalledWith('w1'));
    fireEvent.click(screen.getByRole('button', { name: 'Stop turn' }));
    expect(props.onStopTurn).toHaveBeenCalled();
  });

  it('offers the model and effort the agent offers', () => {
    panelBox({ offered: [model] });
    expect(screen.getByRole('group', { name: /./ }).textContent).toContain('Opus');
  });
});
