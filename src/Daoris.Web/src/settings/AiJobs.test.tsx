import { cleanup, render, screen } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import { AiJobs, type HelpAccessJob, type HelpGrantsJob, type SearchTier } from './AiJobs';

// AGT6: Daoris's own AI on the Settings page — each job it may use a model for, the tier that answers
// it now, and how to change it (D24, `model-decoupling`). The tier is the service's own words; Intake and Ask Daoris are rows of
// *Actors* since MODELROLE1d (`ai/ActorAbout.test.tsx`, `AiDomain.test.tsx`).

const LEXICAL: SearchTier = {
  tier: 'lexical only', semantic: false,
  note: 'Set DAORIS_EMBED_MODEL to enable semantic recall — it is what finds two repositories that reached the same conclusion in different words.',
};

const show = (props: Parameters<typeof AiJobs>[0]) =>
  render(<Tooltip.Provider><AiJobs {...props} /></Tooltip.Provider>);

describe('AI features', () => {
  /** D24: which tier answers is said in the service's own words, beside what changes it. */
  it('states the search tier and its note verbatim, and how the model is chosen', () => {
    show({ search: LEXICAL });

    expect(screen.getByText('lexical only')).toBeInTheDocument();
    expect(screen.getByText(LEXICAL.note!)).toBeInTheDocument();
    // How to change it: the service's environment, read when it starts — never a control that
    // pretends to set it.
    // Each variable is code, as every other row's terminal door is (UX5 U55: they were body text).
    expect(screen.getByText('DAORIS_EMBED_MODEL').tagName).toBe('CODE');
    expect(screen.getByText('DAORIS_EMBED_URL').tagName).toBe('CODE');
    // SEM3b (D123): the window a long entry is embedded in pieces by is the deployment's too.
    expect(screen.getByText('DAORIS_EMBED_WINDOW').tagName).toBe('CODE');
    expect(screen.getByText(/restart/)).toBeInTheDocument();
  });

  it('claims no tier before the service has said one', () => {
    show({});

    expect(screen.queryByText('lexical only')).toBeNull();
    expect(screen.getByText('not yet known')).toBeInTheDocument();
  });

  /** HELPMODE1c1 (D183 point 1; the modes design §5): restricted mode's configured access, under Ask Daoris's access. */
  it('draws configured access under Ask Daoris’s access while restricted, and none under full access or where it is given none', () => {
    const access: HelpAccessJob = {
      mode: 'restricted', confirming: false, adds: [], onPick: vi.fn(), onEnable: vi.fn(), onCancel: vi.fn(),
    };
    const grants: HelpGrantsJob = { agent: { name: 'Claude Code', rules: true }, grants: [], onChoose: vi.fn() };

    show({ search: LEXICAL, access, grants });
    expect(screen.getByRole('region', { name: 'Configured access' })).toBeInTheDocument();

    cleanup();
    show({ search: LEXICAL, access: { ...access, mode: 'full' }, grants });
    expect(screen.queryByRole('region', { name: 'Configured access' })).toBeNull();

    cleanup();
    show({ search: LEXICAL, access });
    expect(screen.queryByRole('region', { name: 'Configured access' })).toBeNull();
  });
});
