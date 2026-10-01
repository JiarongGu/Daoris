import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { CJK_FINDING, CONVERGENT, FINDINGS, IDENTICAL } from './fixtures';
import { FindingList, type FindingsAnswer } from './FindingList';
import { findingId } from './records';

// Convergence's list as a molecule (FRAME1f, D118 §2): the similarity and the answer arrive, and every move goes out.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const answered = (over: Partial<Extract<FindingsAnswer, { state: 'answered' }>> = {}): FindingsAnswer => ({
  state: 'answered', findings: FINDINGS, at: 0.75, more: false, comparing: false, ...over,
});

function list(over: Partial<Parameters<typeof FindingList>[0]> = {}) {
  const props = { threshold: 0.75, onThreshold: vi.fn(), semantic: false, answer: answered(), chosen: null, onChoose: vi.fn(), ...over };
  render(<FindingList {...props} />);
  return props;
}

describe("Convergence's list", () => {
  it('is the similarity, then the findings, each a row of its list', () => {
    list();

    expect(screen.getByRole('slider')).toHaveValue('0.75');
    expect(screen.getByText('0.75')).toBeInTheDocument();
    expect(screen.getByText('3 findings')).toBeInTheDocument();
    for (const item of screen.getAllByRole('listitem')) expect(item).toHaveAttribute('data-list-row');
    // The similarity stays in reach as the findings scroll under it.
    expect(screen.getByRole('slider').closest('.sticky')).not.toBeNull();
  });

  /** A row is its entries, by their titles, each once: two copies of one document share a name. */
  it("titles a row by its entries, and says its likeness, how alike, and who reached it", () => {
    list();

    const identical = within(screen.getByRole('listitem', { name: 'streaming' }));
    expect(identical.getByText('The same document, pasted')).toBeInTheDocument();
    expect(identical.getByText('1.000')).toBeInTheDocument();
    expect(identical.getByText('engine ↔ game ↔ studio-tools')).toBeInTheDocument();
    // A likeness in different words, the finding no text comparison can make, wears the accent.
    expect(within(screen.getByRole('listitem', { name: 'D12 — streaming budget per frame · world-streaming' }))
      .getByText('Same lesson, different words')).toHaveClass('text-accent');
  });

  it('chooses a finding by its entries, and wears the choice on its row', async () => {
    const props = list({ chosen: findingId(CONVERGENT) });

    await userEvent.click(within(screen.getByRole('listitem', { name: 'streaming' })).getByRole('button'));
    expect(props.onChoose).toHaveBeenCalledWith(findingId(IDENTICAL));
    expect(within(screen.getByRole('listitem', { name: 'D12 — streaming budget per frame · world-streaming' })).getByRole('button'))
      .toHaveAttribute('aria-current', 'true');
  });

  /** UX5 U42: an empty answer lowers by a tenth, named for the value it is for. */
  it('offers a lower similarity on an empty answer, by a tenth', async () => {
    const props = list({ answer: answered({ findings: [], at: 0.72 }) });

    expect(screen.getByText('Nothing converges at 0.72 or above')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Lower to 0.62' }));
    expect(props.onThreshold).toHaveBeenCalledWith(0.62);
  });

  /** While a moved similarity's findings are on their way, the last are held, dimmed, and no empty state shows. */
  it('holds the last findings while a moved similarity is compared', () => {
    list({ answer: answered({ comparing: true }) });

    expect(screen.getByText('comparing…')).toBeInTheDocument();
    expect(screen.getAllByRole('listitem')[0]!.closest('ul')).toHaveClass('opacity-60');
  });

  /** The tier's note is said beneath the similarity, in this deployment's words. */
  it("says the tier's note beneath the similarity", () => {
    list({ semantic: true });
    expect(screen.getByText(/Worth sweeping rather than trusting one number/)).toBeInTheDocument();
  });

  it('shows a 中文 finding as it is', () => {
    list({ answer: answered({ findings: [CJK_FINDING] }) });
    expect(screen.getByRole('listitem', { name: '场景加载的顺序 · world-streaming' })).toHaveTextContent('渲染管线 ↔ game');
  });
});
