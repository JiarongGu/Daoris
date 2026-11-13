// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest';
import { CARD_MARK, seekReviewCard } from './seekCard';

describe('seeking the Accept card', () => {
  it('leads to the newest card drawn and says so', () => {
    const root = document.createElement('div');
    root.innerHTML = `<div ${CARD_MARK}="1" tabindex="-1"></div><div ${CARD_MARK}="2" tabindex="-1"></div>`;
    document.body.append(root);
    const [older, newest] = [...root.querySelectorAll<HTMLElement>(`[${CARD_MARK}]`)];
    older!.scrollIntoView = vi.fn();
    newest!.scrollIntoView = vi.fn();

    expect(seekReviewCard(root)).toBe(true);
    expect(newest!.scrollIntoView).toHaveBeenCalled();
    expect(older!.scrollIntoView).not.toHaveBeenCalled();
    expect(document.activeElement).toBe(newest);
    root.remove();
  });

  it('says false where no card is drawn, so the caller leads elsewhere', () => {
    expect(seekReviewCard(document.createElement('div'))).toBe(false);
  });
});
