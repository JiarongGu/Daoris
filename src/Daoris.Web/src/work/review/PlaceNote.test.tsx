import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import '../../locales/i18n';
import { FolderNoGit } from '../../projects/FolderNoGit';
import { isFolder, type Registration } from '../../data/api';
import { DiffFileRow } from './DiffFileRow';
import { PlaceNote } from './PlaceNote';

// A folder's review and page, props in: its change set says what it is measured against, a file that is not text is a row with no
// hunks that says why, and the page's git tabs say a folder has none of git's.

describe('a folder department', () => {
  it('says its review is measured against what the folder held, and that what only git offers is absent', () => {
    render(<PlaceNote />);
    expect(screen.getByText(/This department is a folder/).textContent).toContain('History, branches and landing are git');
  });

  it('draws a file that is not text as a row with no hunks that says it is compared by size and hash', () => {
    render(
      <ul>
        <DiffFileRow
          file={{ path: 'picture.png', status: 'added', added: null, removed: null, patch: null }}
          open
          viewed={false}
          onToggle={vi.fn()}
          onViewed={vi.fn()}
          place
        />
      </ul>,
    );
    expect(screen.getByText(/compared by its size and hash/)).toBeTruthy();
    expect(screen.queryByText(/git diff/)).toBeNull();
  });

  it('is a folder by its kind, and a row that never said is a repository', () => {
    const row = { repository: 'designs', adopted: false } as Registration;
    expect(isFolder({ ...row, kind: 'folder' })).toBe(true);
    expect(isFolder({ ...row, kind: 'git' })).toBe(false);
    expect(isFolder(row)).toBe(false);
    render(<FolderNoGit />);
    expect(screen.getByText(/a folder has none/)).toBeTruthy();
  });
});
