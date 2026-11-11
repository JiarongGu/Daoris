import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import '../locales/i18n';
import { excludesChange, FolderExcludesRow } from './FolderExcludesRow';

describe('a folder department\'s excludes row', () => {
  it('says what a typed list adds and what it takes back, each glob once', () => {
    expect(excludesChange(['build/**', '*.log'], 'build/**\n*.tmp, *.tmp\n')).toEqual({ add: ['*.tmp'], remove: ['*.log'] });
    expect(excludesChange([], '')).toEqual({ add: [], remove: [] });
  });

  it('sends nothing until the list differs, then sends the change alone', async () => {
    const onSave = vi.fn();
    render(<FolderExcludesRow held={['build/**']} onSave={onSave} />);
    const save = screen.getByRole('button', { name: 'Leave these out' });
    const field = screen.getByLabelText('Files to leave out') as HTMLTextAreaElement;
    expect(field.value).toBe('build/**');
    expect((save as HTMLButtonElement).disabled).toBe(true);

    await userEvent.type(field, '\n*.log');
    expect((save as HTMLButtonElement).disabled).toBe(false);
    await userEvent.click(save);

    expect(onSave).toHaveBeenCalledWith({ add: ['*.log'], remove: [] });
  });
});
