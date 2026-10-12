import { describe, expect, it } from 'vitest';
import { driverSource } from './driverSource';

/**
 * A twin vitest reads the driver's source by the file's name, wherever it lies under the driver's folder (STRUCT1, D166),
 * so the driver's files move into module folders and no twin is edited for it.
 */
describe('a driver source found by name', () => {
  it('is read wherever it lies, at the top or in a folder', () => {
    expect(driverSource('NoteCodes.cs')).toContain('class NoteCodes');
    expect(driverSource('HelpGoProposals.cs')).toContain('class HelpGoProposals');
  });

  it('is refused when no file has the name', () => {
    expect(() => driverSource('Gone.cs')).toThrow('no Gone.cs under Daoris.Desktop.Driver: a source read by name must be there, at any depth');
  });
});
