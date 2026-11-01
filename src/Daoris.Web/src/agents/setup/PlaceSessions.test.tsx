import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import { SETUP_DEFAULTS, SETUP_OWN } from '../../projects/fixtures';
import type { RepositorySetupProps } from '../../projects/setup/RepositorySetup';
import { PlaceRepositorySetupView } from './PlaceSessions';

// AGENTSPLACE1f (the Agents-place design §3): a repository place's Sessions hold today's rows moved whole from Repositories' Setup tab, each
// with *Set for this repository* or *Clear*, drawn from the one molecule; its Driving and Line and landing follow until their rows build them.

const draw = (setup: RepositorySetupProps | null) => render(<Tooltip.Provider><PlaceRepositorySetupView setup={setup} /></Tooltip.Provider>);

/** A part of the page, found by its heading's id: a section inside it is titled *Sessions* too. */
const part = (name: 'Sessions' | 'Workflow') => document.getElementById(`place-${name.toLowerCase()}`)!.closest('section')!;

afterEach(async () => { await i18n.changeLanguage('en'); });

describe("a repository place's Sessions", () => {
  it('holds the sessions, the reach and the rules under Sessions, and the line and landing under Workflow', () => {
    draw(SETUP_DEFAULTS);

    expect(document.getElementById('place-sessions')).toHaveTextContent('Sessions');
    expect(document.getElementById('place-workflow')).toHaveTextContent('Workflow');
    const sessions = within(part('Sessions'));
    expect(sessions.getByRole('button', { name: 'Sessions' })).toBeInTheDocument();
    expect(sessions.getByRole('button', { name: 'Reach' })).toBeInTheDocument();
    expect(sessions.queryByRole('button', { name: 'Line and landing' })).toBeNull();
    expect(sessions.queryByRole('button', { name: 'Driving' })).toBeNull();
    expect(within(part('Workflow')).getByRole('button', { name: 'Line and landing' })).toBeInTheDocument();
    // Driving keeps its section above them.
    expect(screen.getByRole('button', { name: 'Driving' })).toBeInTheDocument();
  });

  it('offers *Set for this repository* on a value from above and *Clear* on one set here, as the tab did', async () => {
    const onSet = vi.fn();
    const onClear = vi.fn();
    const setup: RepositorySetupProps = {
      ...SETUP_OWN,
      sessions: {
        ...SETUP_OWN.sessions,
        language: { ...SETUP_OWN.sessions!.language!, onSet: (language) => (language === null ? onClear() : onSet(language)) },
      },
    };
    draw(setup);

    // The session language is set here: its section opens on its own, and its row carries *Clear*.
    const sessions = within(part('Sessions'));
    expect(sessions.getByRole('button', { name: 'Sessions' })).toHaveAttribute('aria-expanded', 'true');
    await userEvent.click(sessions.getAllByRole('button', { name: 'Clear' })[0]!);
    expect(onClear).toHaveBeenCalledOnce();
    expect(onSet).not.toHaveBeenCalled();
  });

  it('draws nothing for a repository the driver has not answered', () => {
    draw(null);
    expect(screen.queryByRole('heading', { level: 2 })).toBeNull();
  });

  it('says its headings in Chinese', async () => {
    await i18n.changeLanguage('zh');
    draw(SETUP_DEFAULTS);
    expect(document.getElementById('place-sessions')).toHaveTextContent('会话');
    expect(document.getElementById('place-workflow')).toHaveTextContent('工作流');
  });
});
