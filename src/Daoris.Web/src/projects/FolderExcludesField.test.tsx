import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import '../locales/i18n';
import { FolderExcludesField, parseExcludes } from './FolderExcludesField';

// A folder's own excludes, typed where it is added: one glob to a line or separated by commas, each kept once.

describe("a folder's own excludes", () => {
  it('reads one glob to a line or separated by commas, trimmed and each once', () => {
    expect(parseExcludes('build/**\n *.log , build/**\n\n')).toEqual(['build/**', '*.log']);
    expect(parseExcludes('  ')).toEqual([]);
  });

  it('is a field that says what the globs are and hands back what is typed', () => {
    const onChange = vi.fn();
    render(<FolderExcludesField value="" onChange={onChange} />);
    fireEvent.change(screen.getByLabelText('Files to leave out'), { target: { value: '*.log' } });
    expect(onChange).toHaveBeenCalledWith('*.log');
    expect(screen.getByText(/beside the defaults/)).toBeTruthy();
  });
});
