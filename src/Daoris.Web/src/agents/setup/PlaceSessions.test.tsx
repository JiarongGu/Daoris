import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import { SETUP_DEFAULTS, SETUP_OWN } from '../../projects/fixtures';
import type { RepositorySetupProps } from '../../projects/setup/RepositorySetup';
import { FULL, GATES_CHANGED, INTEGRATION_CHANGED } from '../../workflow/fixtures';
import type { PlaceWorkflowProps } from '../../workflow/place/PlaceWorkflowView';
import { PlaceSessionsBodyView, PlaceWorkflowBodyView } from './PlaceSessions';

// AGENTSPLACE1f (the Agents-place design §3): a repository place's Sessions hold today's rows moved whole from Repositories' Setup tab, each
// with *Set for this repository* or *Clear*, drawn from the one molecule. SETUPUX1h: its Driving is the page head's, and each section's body
// is drawn apart, inside the fold that names it.

// AGENTSPLACE1h: its Workflow, handed in as the application hands it, holds the route, Gates and the line's landing rows.
const WORKFLOW: PlaceWorkflowProps = { name: 'engine', current: { ...FULL, start: { ...GATES_CHANGED, integration: INTEGRATION_CHANGED } } };

const drawSessions = (setup: RepositorySetupProps | null) =>
  render(<Tooltip.Provider><PlaceSessionsBodyView setup={setup} /></Tooltip.Provider>);
const drawWorkflow = (setup: RepositorySetupProps | null, workflow: PlaceWorkflowProps | null = WORKFLOW) =>
  render(<Tooltip.Provider><PlaceWorkflowBodyView setup={setup} workflow={workflow} /></Tooltip.Provider>);

afterEach(async () => { await i18n.changeLanguage('en'); });

describe("a repository place's Sessions", () => {
  it('holds the sessions, the reach and the rules, and neither the line and landing nor the driving', () => {
    drawSessions(SETUP_DEFAULTS);
    expect(screen.getByRole('button', { name: 'Sessions' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Reach' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Line and landing' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Driving' })).toBeNull();
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
    drawSessions(setup);

    // The session language is set here: its section opens on its own, and its row carries *Clear*.
    expect(screen.getByRole('button', { name: 'Sessions' })).toHaveAttribute('aria-expanded', 'true');
    await userEvent.click(screen.getAllByRole('button', { name: 'Clear' })[0]!);
    expect(onClear).toHaveBeenCalledOnce();
    expect(onSet).not.toHaveBeenCalled();
  });

  it('draws nothing for a repository the driver has not answered', () => {
    drawSessions(null);
    expect(screen.queryByRole('heading', { level: 2 })).toBeNull();
  });
});

describe("a repository place's Workflow", () => {
  it('holds the routes, Gates and the folds of the line and landing, and no sessions rows', () => {
    drawWorkflow(SETUP_DEFAULTS);
    expect(screen.getByRole('list', { name: 'Where work here goes' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Landing' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'The line' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Gates' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reach' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Line and landing' })).toBeNull();
  });

  it('draws the integration declaration once, in Gates, and not again under the landed route', () => {
    const landed = { start: { ...GATES_CHANGED, integration: INTEGRATION_CHANGED }, workspace: 'aurora', onChoose: vi.fn(), onAcceptIntegration: vi.fn() };
    drawWorkflow({ ...SETUP_OWN, landed });

    expect(within(document.body).getAllByText('Accept for integration')).toHaveLength(1);
    expect(screen.queryByText('Integration declaration')).toBeNull();
  });

  it('keeps the heavy gates in More, which the Sessions section no longer draws', async () => {
    drawWorkflow({ ...SETUP_DEFAULTS, heavy: { gates: [{ name: 'build', run: 'dotnet build' }], onSave: vi.fn() } });
    expect(screen.getByRole('button', { name: 'More' })).toHaveAccessibleDescription(/1 heavy gate: build/);
    expect(screen.queryByRole('button', { name: 'Heavy gates' })).toBeNull();

    cleanup();
    drawSessions({ ...SETUP_DEFAULTS, heavy: { gates: [{ name: 'build', run: 'dotnet build' }], onSave: vi.fn() } });
    expect(screen.queryByRole('button', { name: 'Heavy gates' })).toBeNull();
  });

  it('draws no Workflow while the driver has not answered it', () => {
    const { container } = drawWorkflow(SETUP_DEFAULTS, null);
    expect(container).toBeEmptyDOMElement();
  });

  it('says its names in Chinese', async () => {
    await i18n.changeLanguage('zh');
    drawWorkflow(SETUP_DEFAULTS);
    expect(screen.getByRole('list', { name: '工作在这里如何分流' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '落地' })).toBeInTheDocument();
  });
});
