import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../i18n';
import { CodeBlock, WRAP_BELOW } from './CodeBlock';
import { Markdown } from './Markdown';

/**
 * ASKHIST1c: a fenced block's long lines in a narrow pane. They scrolled sideways, cut off at Ask Daoris's dock. A named
 * *Wrap* control sits beside *Copy*; until the person presses it, the block wraps where its own pane is narrow (a container
 * query on the block, never the window), and Copy keeps the original text whichever way it is shown.
 */

const LONG = 'curl -sS https://example.atlassian.net/rest/api/2/issue/TK-2205?fields=summary,status,assignee,comment&expand=renderedFields\necho done';

/** The block's width as the page lays it out; jsdom lays nothing out, so the measure is handed in. */
function laidOutAt(width: number) {
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
    return { width: this.tagName === 'FIGURE' ? width : 0, height: 0, top: 0, left: 0, right: width, bottom: 0, x: 0, y: 0, toJSON: () => ({}) };
  });
}

describe.each(['en', 'zh'])('a code block’s long lines in %s', (language) => {
  afterEach(async () => {
    cleanup();
    vi.restoreAllMocks();
    await i18n.changeLanguage('en');
  });

  it('names a Wrap control beside Copy, and wraps by its own pane’s width until the person chooses', async () => {
    await i18n.changeLanguage(language);
    render(<CodeBlock code={LONG} language="bash" />);

    const caption = screen.getByRole('figure').querySelector('figcaption')!;
    const wrap = within(caption).getByRole('button', { name: i18n.t('work.code.wrap') });
    expect(within(caption).getByRole('button', { name: i18n.t('work.code.copy') })).toBeInTheDocument();
    // Unmeasured, the pane decides by a container query on the block itself, never by the window's width.
    expect(screen.getByRole('figure')).toHaveClass('@container/code');
    const pre = screen.getByRole('figure').querySelector('pre')!;
    expect(pre.className).toContain(`@max-[${WRAP_BELOW}]/code:whitespace-pre-wrap`);
    expect(pre.className).toContain(`@max-[${WRAP_BELOW}]/code:wrap-anywhere`);
    expect(pre.className).not.toMatch(/@media|max-md:/);

    // Pressed, it wraps whatever the width; pressed again, its lines scroll in its own box.
    await userEvent.click(wrap);
    expect(wrap).toHaveAttribute('aria-pressed', 'true');
    expect(pre).toHaveClass('whitespace-pre-wrap', 'wrap-anywhere');
    await userEvent.click(wrap);
    expect(wrap).toHaveAttribute('aria-pressed', 'false');
    expect(pre).toHaveClass('overflow-x-auto');
    expect(pre).not.toHaveClass('whitespace-pre-wrap');
  });

  it('starts wrapped in a narrow pane and unwrapped in a wide one, saying which it is', async () => {
    await i18n.changeLanguage(language);
    laidOutAt(400);
    render(<CodeBlock code={LONG} language="bash" />);
    const narrow = screen.getByRole('button', { name: i18n.t('work.code.wrap') });
    expect(narrow).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('figure').querySelector('pre')).toHaveClass('whitespace-pre-wrap', 'wrap-anywhere');

    cleanup();
    laidOutAt(1000);
    render(<CodeBlock code={LONG} language="bash" />);
    expect(screen.getByRole('button', { name: i18n.t('work.code.wrap') })).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('figure').querySelector('pre')).toHaveClass('overflow-x-auto');
  });

  it('copies the original text, its line breaks and all, however it is shown', async () => {
    await i18n.changeLanguage(language);
    render(<CodeBlock code={LONG} language="bash" />);
    await userEvent.click(screen.getByRole('button', { name: i18n.t('work.code.wrap') }));

    // After user-event's own clipboard, which each of its calls puts in place.
    const writeText = vi.fn(async () => {});
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    fireEvent.click(screen.getByRole('button', { name: i18n.t('work.code.copy') }));
    expect(writeText).toHaveBeenCalledWith(LONG);
  });
});

/** A table keeps its columns and scrolls in its own box, beside a fenced block that wraps. */
describe('an answer’s table beside its code', () => {
  afterEach(() => cleanup());

  it('keeps a table scrolling in its own box', () => {
    render(<Markdown text={`| key | value |\n| --- | --- |\n| TK-2205 | ${'x'.repeat(200)} |\n\n\`\`\`bash\n${LONG}\n\`\`\``} />);
    expect(screen.getByRole('table').parentElement).toHaveClass('overflow-x-auto');
    expect(screen.getByRole('figure')).toHaveClass('@container/code');
  });
});
