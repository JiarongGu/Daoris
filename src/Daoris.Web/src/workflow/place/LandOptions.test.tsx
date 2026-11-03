import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { draftOf, graphOf, setOption } from '../edit/graphDraft';
import { APPEND, judgedFor, PALETTE, ROLES } from '../edit/editFixtures';
import { MERGE_GRAPH } from '../graphFixtures';
import { LandOptions } from './LandOptions';

// WORKFLOWCLEAN1h-b: the Landing row under a saved version is the land step's options, saved as the version's next, with the places an
// append reaches let follow it by one consent (SETUPUX1s).

afterEach(async () => {
  vi.restoreAllMocks();
  await act(async () => { await i18n.changeLanguage('en'); });
});

const squashed = () => graphOf(setOption(draftOf(MERGE_GRAPH), 'landing', 'squash', true));

const draw = (more: Partial<Parameters<typeof LandOptions>[0]> = {}) => {
  const onJudge = vi.fn();
  const onSave = vi.fn(async (_ask: unknown) => ({ ...APPEND, written: true, version: 3 }));
  const view = render(
    <LandOptions
      palette={PALETTE} roles={ROLES} graph={MERGE_GRAPH} workflow={{ id: 'docs-release', version: 2 }}
      judged={null} judgedOf={null} onJudge={onJudge} saving={false} onSave={onSave} {...more}
    />,
  );
  return { onJudge, onSave, ...view };
};

describe('the Landing row under a saved version', () => {
  it("is the land step's options, and a change is judged as the version's next", async () => {
    const { onJudge } = draw();

    await userEvent.click(screen.getByRole('checkbox', { name: /one commit/ }));

    await waitFor(() => expect(onJudge).toHaveBeenCalledWith({ id: 'docs-release', graph: squashed(), base: 2 }));
    expect(screen.getByRole('button', { name: 'Review and save' })).toBeDisabled();
  });

  it('asks the consent of the places the append reaches, and saves the version with it', async () => {
    const { onSave } = draw({ judged: APPEND, judgedOf: judgedFor(squashed()) });
    await userEvent.click(screen.getByRole('checkbox', { name: /one commit/ }));

    await userEvent.click(await screen.findByRole('button', { name: 'Review and save' }));
    const save = screen.getByRole('button', { name: 'Save v3' });
    expect(save).toBeDisabled();
    await userEvent.click(screen.getByRole('checkbox', { name: /follow it/i }));
    await userEvent.click(save);

    expect(onSave).toHaveBeenCalledWith({
      id: 'docs-release', graph: squashed(), base: 2, authorize: ['repository:engine:default', 'workspace:aurora:kind:docs'],
    });
  });
});
