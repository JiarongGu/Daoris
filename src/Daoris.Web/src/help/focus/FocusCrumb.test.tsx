import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { MACHINE, type Place } from '../focus';
import { FocusCrumb } from './FocusCrumb';

const games: Place = { kind: 'workspace', workspace: 'Games' };
const engine: Place = { kind: 'repository', workspace: 'Games', repository: 'engine' };

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage('en');
});

const crumb = () => screen.getByRole('navigation', { name: /^(Focus|聚焦)$/ });
const said = () => (crumb().textContent ?? '').replace(/\s+/g, ' ').trim();

describe('the conversation head’s focus', () => {
  it('names the focus as a breadcrumb whose workspace and repository are doors, and this machine none', async () => {
    const onOpen = vi.fn();
    render(<FocusCrumb focus={engine} next={null} window="Games" home="Games" onOpen={onOpen} />);
    expect(said()).toBe('This machine › Games › engine');
    expect(screen.queryByRole('button', { name: 'This machine' })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Games' }));
    expect(onOpen).toHaveBeenCalledWith(games);
    await userEvent.click(screen.getByRole('button', { name: 'engine' }));
    expect(onOpen).toHaveBeenLastCalledWith(engine);
    expect(screen.getByRole('button', { name: 'engine' })).toHaveAttribute('aria-current', 'location');
  });

  it('marks a focus outside the window’s workspace, and a conversation started in another', () => {
    render(<FocusCrumb focus={engine} next={null} window="Work" home="Games" onOpen={() => {}} />);
    expect(said()).toBe('This machine › Games › engine ◇ this window: Work · started in Games');
  });

  it('says a change waiting, and a focus no longer on this machine, in words', () => {
    render(<FocusCrumb focus={games} next={engine} window="Games" home="Games" onOpen={() => {}} />);
    expect(said()).toBe('This machine › Games · engine next message');
    cleanup();
    render(<FocusCrumb focus={engine} next={null} gone window="Games" home="Games" onOpen={() => {}} />);
    expect(said()).toBe('This machine › Games › engine ⚠ no longer on this machine');
  });

  it('names this machine alone, and marks nothing in a window on every workspace', () => {
    render(<FocusCrumb focus={MACHINE} next={null} window={null} home="Games" onOpen={() => {}} />);
    expect(said()).toBe('This machine');
  });

  it('speaks Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<FocusCrumb focus={engine} next={games} window="Work" home="Games" onOpen={() => {}} />);
    expect(said()).toBe('本机 › Games › engine ◇ 此窗口：Work · 始于 Games · 下一条消息起 Games');
  });
});
