import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { KnowledgeModes, KnowledgeStrip } from './KnowledgeModes';

// Knowledge's list head (UX6i, D150 §2.2): a two-way choice, Search · Convergence, and the strip's stand-in for it. A
// molecule: the mode in front arrives, and every press goes out.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);
const choice = () => screen.getByRole('radiogroup', { name: 'Knowledge' });

describe("Knowledge's list head", () => {
  it('is a two-way choice, Search then Convergence, with the mode in front chosen', async () => {
    const onMode = vi.fn();
    render(<KnowledgeModes mode="search" onMode={onMode} />);
    expect(within(choice()).getAllByRole('radio').map((radio) => [radio.textContent, radio.getAttribute('aria-checked')]))
      .toEqual([['Search', 'true'], ['Convergence', 'false']]);

    await userEvent.click(within(choice()).getByRole('radio', { name: 'Convergence' }));
    expect(onMode).toHaveBeenCalledWith('convergence');
  });

  it('moves by the arrows, as every choice of a few does, and is one stop for Tab', async () => {
    const onMode = vi.fn();
    render(<KnowledgeModes mode="convergence" onMode={onMode} />);
    const chosen = within(choice()).getByRole('radio', { name: 'Convergence' });
    expect(chosen).toHaveAttribute('tabindex', '0');
    expect(within(choice()).getByRole('radio', { name: 'Search' })).toHaveAttribute('tabindex', '-1');
    chosen.focus();
    await userEvent.keyboard('{ArrowLeft}');
    expect(onMode).toHaveBeenCalledWith('search');
  });

  it('says both modes in 中文 by the names their places had', async () => {
    const { default: i18n } = await import('../i18n');
    await i18n.changeLanguage('zh');
    try {
      render(<KnowledgeModes mode="search" onMode={vi.fn()} />);
      expect(within(screen.getByRole('radiogroup', { name: '知识' })).getAllByRole('radio').map((radio) => radio.textContent))
        .toEqual(['搜索', '同归']);
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});

describe("Knowledge's strip", () => {
  it('stands for the choice: each mode by its glyph, named, the one in front marked, a press told', async () => {
    const onMode = vi.fn();
    render(<KnowledgeStrip mode="convergence" onMode={onMode} />);
    expect(screen.getByRole('button', { name: 'Convergence' })).toHaveAttribute('aria-current', 'true');
    expect(screen.getByRole('button', { name: 'Search' })).not.toHaveAttribute('aria-current');

    await userEvent.click(screen.getByRole('button', { name: 'Search' }));
    expect(onMode).toHaveBeenCalledWith('search');
  });
});
