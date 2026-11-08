import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../locales/i18n';
import { HitList } from './HitList';

const draw = (indexed?: number) => render(
  <Tooltip.Provider>
    <HitList
      query="" onQuery={vi.fn()} localOnly onLocalOnly={vi.fn()} answer={{ state: 'idle' }} marked="" semantic={false}
      chosen={null} onChoose={vi.fn()} onConverge={vi.fn()} indexed={indexed}
    />
  </Tooltip.Provider>,
);

describe("Search's list before anything is typed", () => {
  it('says how many entries it holds and what to do, instead of standing empty', () => {
    draw(40);

    expect(screen.getByText('40 entries are indexed. Type two letters or more to search them.')).toBeInTheDocument();
  });

  it('says it in Chinese too', async () => {
    await i18n.changeLanguage('zh');
    try {
      draw(40);
      expect(screen.getByText(i18n.t('search.idle', { count: 40 }))).toBeInTheDocument();
      expect(i18n.t('search.idle', { count: 40 })).toContain('40');
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  it('says only what to do where nothing is indexed or the count is not known', () => {
    draw();

    expect(screen.getByText('Type two letters or more to search.')).toBeInTheDocument();
  });
});
