import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../locales/i18n';
import { COUNTS, ENGINE } from './fixtures';
import { ProjectPage } from './ProjectPage';

// A department's page offers *Open* where its folder is on this machine and a door answers, and says nothing where it is not.

describe("a department's folder on its page", () => {
  it('offers Open where a door answers, and the press asks for it', async () => {
    const onOpenFolder = vi.fn();
    render(<Tooltip.Provider><ProjectPage registration={ENGINE} counts={COUNTS.engine} onOpenFolder={onOpenFolder} /></Tooltip.Provider>);

    await userEvent.click(screen.getByRole('button', { name: 'Open' }));

    expect(onOpenFolder).toHaveBeenCalledOnce();
  });

  it('offers nothing where no door answers', () => {
    render(<Tooltip.Provider><ProjectPage registration={ENGINE} counts={COUNTS.engine} /></Tooltip.Provider>);

    expect(screen.queryByRole('button', { name: 'Open' })).toBeNull();
  });
});
