import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import '../../locales/i18n';
import { ReleaseTargetForm } from './ReleaseTargetForm';
import { releaseNodesOf, targetValues } from './releaseTarget';

// The release target's window form:the release manager's holder picks from what the plugin found, a
// definition, its stage and the action, with a reason, and the one door sets it on every release step of the workflow.

const FOUND = {
  url: 'https://dev.azure.com/acme/Proj/_release',
  pipelines: [{ id: 7, url: '', definitions: [
    { id: 42, name: 'Portal', url: '', triggers: ['main'], stages: [{ id: 402, name: 'Production', after: [], deploysOn: 'merge' }] },
    { id: 43, name: 'Portal-feature', url: '', triggers: ['dev'], stages: [{ id: 403, name: 'Test', after: [], deploysOn: 'merge' }, { id: 404, name: 'Staging', after: ['Test'], deploysOn: 'start' }] },
  ] }],
};

describe('the release target form', () => {
  it('offers what the plugin found and saves the chosen definition, stage and action with the reason', async () => {
    const user = userEvent.setup();
    const onSave = vi.fn().mockResolvedValue({ done: true });
    render(<ReleaseTargetForm found={FOUND} onSave={onSave} />);

    const choose = async (field: string, option: string) => {
      await user.click(screen.getByRole('combobox', { name: field }));
      await user.click(await screen.findByRole('option', { name: option }));
    };
    await choose('Release definition', '#43 Portal-feature · pipeline 7 · dev');
    await choose('Stage', '404 Staging');
    await choose('Action', 'Start the stage of the release made');
    const save = screen.getByRole('button', { name: 'Set the target' });
    expect(save).toBeDisabled();
    fireEvent.change(screen.getByLabelText('Why this target'), { target: { value: 'the work merged into dev' } });
    fireEvent.click(save);

    expect(onSave).toHaveBeenCalledWith({ pipeline: 7, releaseDefinition: 43, stage: 404, action: 'start-stage' }, 'the work merged into dev');
  });

  it('says where the plugin found nothing, and sets nothing', () => {
    render(<ReleaseTargetForm found={{ url: '', pipelines: [] }} onSave={vi.fn()} />);
    expect(screen.getByText('The plugin found no release definition taking this repository\'s builds.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Set the target' })).toBeNull();
  });

  it('writes each value as the door takes it, JSON text, on every release step the workflow names', () => {
    expect(targetValues({ pipeline: 7, releaseDefinition: 43, stage: 404, action: 'observe' }))
      .toEqual({ pipeline: '7', releaseDefinition: '43', stage: '404', action: '"observe"' });
    expect(releaseNodesOf([
      { id: 'monitor', kind: 'monitor' }, { id: 'release-target', kind: 'daoris-azure-devops/release-target' },
      { id: 'release-watch', kind: 'daoris-azure-devops/release-watch' }, { id: 'release', kind: 'daoris-azure-devops/release' },
    ])).toEqual(['release-target', 'release-watch', 'release']);
  });
});
