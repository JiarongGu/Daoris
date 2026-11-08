import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import '../../locales/i18n';
import type { SystemWiring, WiringFolder } from '../../data/wiring';
import { WiringFolders } from './WiringFolders';

const folder = (over: Partial<WiringFolder>): WiringFolder => ({
  id: 'app', path: 'D:/somewhere/app', pathWhy: null, present: true, bytes: 1024, bytesWhy: null, measured: null, old: [], ...over,
} as WiringFolder);

const draw = (folders: WiringFolder[]) => render(
  <WiringFolders
    wiring={{
      folder: { path: 'D:/somewhere/Daoris', pathWhy: null, kind: 'development' },
      folders, leftOver: [], measuring: false,
    } as unknown as Pick<SystemWiring, 'folder' | 'folders' | 'leftOver' | 'measuring'>}
    onMeasure={() => {}}
  />,
);

describe('the folders list', () => {
  it('says a folder a development run has none of once, in its path column, and nothing in its size column', () => {
    draw([folder({ id: 'app', path: null, pathWhy: 'development', present: false, bytes: null })]);

    expect(screen.getAllByText('A development run has none')).toHaveLength(1);
    expect(screen.queryByText('Not there yet')).toBeNull();
  });

  it('says a folder with a path that is not there yet in its size column', () => {
    draw([folder({ id: 'update', path: 'D:/somewhere/update', present: false, bytes: null })]);

    expect(screen.getAllByText('Not there yet')).toHaveLength(1);
  });
});
