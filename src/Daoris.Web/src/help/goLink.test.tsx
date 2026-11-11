import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { Markdown } from '../work/conversation/Markdown';
import { GoOpener, goPlace } from './goLink';

describe('a go link Ask Daoris writes', () => {
  it('reads the place its fields name, a value\'s space decoded', () => {
    expect(goPlace('daoris:go?view=settings&domain=git')).toEqual({ view: 'settings', domain: 'git' });
    expect(goPlace('daoris:go?view=sessions&part=new&repository=engine&sentence=fix%20the%20build'))
      .toEqual({ view: 'sessions', part: 'new', repository: 'engine', sentence: 'fix the build' });
    expect(goPlace('daoris:go?view=quests&item=ask:a1b2c3d4')).toEqual({ view: 'quests', item: 'ask:a1b2c3d4' });
  });

  it('is no go link without its view, or at another address', () => {
    expect(goPlace('daoris:go?domain=git')).toBeNull();
    expect(goPlace('daoris:other?view=settings')).toBeNull();
    expect(goPlace('https://example.com/?view=settings')).toBeNull();
    expect(goPlace(undefined)).toBeNull();
  });

  it('opens its place in the window when pressed, and changes nothing else', () => {
    const open = vi.fn();
    render(
      <GoOpener.Provider value={open}>
        <Markdown text="Accounts are under [Settings → Git](daoris:go?view=settings&domain=git)." />
      </GoOpener.Provider>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Settings → Git' }));

    expect(open).toHaveBeenCalledWith({ view: 'settings', section: 'git' });
    expect(screen.queryByRole('link')).toBeNull();
  });

  it('is words alone where no place answers it or nothing opens it', () => {
    const open = vi.fn();
    const { container } = render(
      <GoOpener.Provider value={open}>
        <Markdown text="[Nowhere](daoris:go?view=nowhere)" />
      </GoOpener.Provider>,
    );
    expect(screen.queryByRole('button')).toBeNull();
    expect(container.textContent).toContain('Nowhere');

    render(<Markdown text="[Settings](daoris:go?view=settings)" />);
    expect(screen.queryByRole('button', { name: 'Settings' })).toBeNull();
    expect(screen.queryByRole('link', { name: 'Settings' })).toBeNull();
  });
});
