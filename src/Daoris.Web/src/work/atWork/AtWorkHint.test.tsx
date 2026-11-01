import { afterEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { en, zh } from '../../locales';
import { AtWorkView } from './AtWorkView';
import { HINTED, THREE } from './atWork.fixtures';
import { HINT_KEY, LANES_GUIDE } from './words';
import type { AtWorkHint } from './types';

// COWORKVIEW1e-iii: the hint, drawn as the driver's judge answered it, with the guide's lanes section behind it.

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage('en');
});

const STANDINGS: AtWorkHint[] = ['not-driven', 'no-lanes', 'one-at-a-time'];

describe('the hint', () => {
  it.each(STANDINGS)('draws %s in its own words, linked to the guide’s lanes section', (hint) => {
    const { container } = render(<AtWorkView held={HINTED(hint)} />);

    const drawn = container.querySelector(`[data-hint="${hint}"]`);
    expect(drawn).not.toBeNull();
    expect(drawn!.textContent).toContain(en[HINT_KEY[hint] as keyof typeof en].replaceAll('`', ''));
    const link = screen.getByRole('link', { name: 'About lanes' });
    expect(link.getAttribute('href')).toBe(LANES_GUIDE);
  });

  it('draws none where the answer carries none', () => {
    const { container } = render(<AtWorkView held={THREE} />);

    expect(container.querySelector('[data-hint]')).toBeNull();
    expect(screen.queryByRole('link', { name: 'About lanes' })).toBeNull();
  });

  it('is worded in both catalogues for every standing', () => {
    for (const hint of STANDINGS) {
      expect(en[HINT_KEY[hint] as keyof typeof en]).toBeTruthy();
      expect(zh[HINT_KEY[hint] as keyof typeof zh]).toBeTruthy();
    }
  });

  it('is said in Chinese in the Chinese catalogue', async () => {
    await i18n.changeLanguage('zh');
    const { container } = render(<AtWorkView held={HINTED('no-lanes')} />);

    expect(container.querySelector('[data-hint]')!.textContent).toContain('这个仓库没有声明泳道');
    expect(screen.getByRole('link', { name: '关于泳道' })).toBeTruthy();
  });
});
