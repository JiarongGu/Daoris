import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { PushActs } from './NewRef';
import { PushRef } from './PushRef';

// Push a branch or a lightweight tag: a press that says what moves on origin and sends nothing until it is
// confirmed, carrying the commit the page showed; a tag's confirmation says the place's Dangerous value, which decides it at the door.

const COMMIT = 'a1b2c3d4e5f60718293a4b5c6d7e8f9012345678';

const acts = (over: Partial<PushActs> = {}): PushActs => ({
  offered: true, address: 'ssh://git@example.com/org/engine.git', dangerous: 'deny',
  press: vi.fn(async () => ({ act: 'p1', outcome: 'done', refused: false, said: 'act p1' })), ...over,
});

describe('the push press', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('sends nothing until it is confirmed, then pushes the branch at the commit shown', async () => {
    const push = acts();
    render(<PushRef kind="branch" name="work/fix" commit={COMMIT} push={push} />);

    await userEvent.click(screen.getByRole('button', { name: 'Push the branch work/fix to origin' }));
    expect(push.press).not.toHaveBeenCalled();
    expect(screen.getByText(/never forced/)).toHaveTextContent('origin (ssh://git@example.com/org/engine.git)');
    await userEvent.click(screen.getByRole('button', { name: 'Push' }));

    expect(push.press).toHaveBeenCalledWith({ kind: 'branch', name: 'work/fix', at: COMMIT });
    expect(await screen.findByRole('status')).toHaveTextContent('Pushed the branch work/fix to origin.');
  });

  it('says a tag is a Dangerous push and whether this place allows it', async () => {
    const { unmount } = render(<PushRef kind="tag" name="v1" commit={COMMIT} push={acts()} />);
    await userEvent.click(screen.getByRole('button', { name: 'Push the tag v1 to origin' }));
    expect(screen.getByText(/Dangerous/)).toHaveTextContent('denies Dangerous pushes, so Daoris will refuse it at the door');
    unmount();

    render(<PushRef kind="tag" name="v1" commit={COMMIT} push={acts({ dangerous: 'allow' })} />);
    await userEvent.click(screen.getByRole('button', { name: 'Push the tag v1 to origin' }));
    expect(screen.getByText(/Dangerous/)).toHaveTextContent('allows Dangerous pushes');
  });

  it("says the door's refusal where nothing was pushed, and a refusal in the driver's own words", async () => {
    const notDone = acts({ press: vi.fn(async () => ({ act: 'p2', outcome: 'not-done', refused: false, said: 'act p2', notes: 'Nothing was pushed: daoris: a push of a tag is refused.' })) });
    const { unmount } = render(<PushRef kind="tag" name="v1" commit={COMMIT} push={notDone} initial={{ step: 'confirm' }} />);
    await userEvent.click(screen.getByRole('button', { name: 'Push tag' }));
    expect(await screen.findByRole('status')).toHaveTextContent('daoris: a push of a tag is refused.');
    unmount();

    const refused = acts({ press: vi.fn(async () => ({ refused: true, said: 'This repository’s git access denies writes to origin, so nothing was pushed.' })) });
    render(<PushRef kind="branch" name="work/fix" commit={COMMIT} push={refused} initial={{ step: 'confirm' }} />);
    await userEvent.click(screen.getByRole('button', { name: 'Push' }));
    expect(await screen.findByRole('status')).toHaveTextContent('denies writes to origin');
  });

  it('is absent where the driver does not offer a push', () => {
    const { container } = render(<PushRef kind="branch" name="work/fix" commit={COMMIT} push={acts({ offered: false, reason: 'doors' })} />);
    expect(container).toBeEmptyDOMElement();
  });
});
