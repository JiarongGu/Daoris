import { relative } from 'node:path';
import { describe, expect, it } from 'vitest';
import { DRIVER_FOLDER, driverFiles, driverSource, oneNamed } from './driverSource';

/**
 * A twin vitest reads the driver's source by the file's name, wherever it lies under the driver's folder (STRUCT1, D166),
 * so the driver's files move into module folders and no twin is edited for it. The cases name no driver file but one the
 * twins parse already, so a change to another driver file never owes this suite a run.
 */
describe('a driver source found by name', () => {
  it('is the one file of its name wherever it lies, and a name no file has, or two have, is refused', () => {
    expect(oneNamed('Rose.cs', ['driver/Garden/Rose.cs', 'driver/Garden/RoseBed.cs'])).toBe('driver/Garden/Rose.cs');
    expect(() => oneNamed('Gone.cs', [])).toThrow('no Gone.cs under Daoris.Desktop.Driver: a source read by name must be there, at any depth');
    expect(() => oneNamed('Thorn.cs', ['a/Thorn.cs', 'b/Thorn.cs']))
      .toThrow('Thorn.cs is under Daoris.Desktop.Driver 2 times, so its name does not say which: a/Thorn.cs, b/Thorn.cs');
  });

  it("walks every folder of the driver, the build's output aside, and reads the file it finds", () => {
    const parts = driverFiles().map((path) => relative(DRIVER_FOLDER, path).split(/[\\/]/));
    expect(parts.some((part) => part.length > 2)).toBe(true);
    expect(parts.some((part) => part[0] === 'bin' || part[0] === 'obj')).toBe(false);
    expect(driverSource('NoteCodes.cs')).toContain('class NoteCodes');
  });
});
