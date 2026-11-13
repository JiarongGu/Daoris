import { test } from 'vitest';
import assert from 'node:assert/strict';
import { reachedByFile, type ClassReach } from './class-reach.ts';

const area = 'src/D/Driver/Sessions';
const declared = new Set([`${area}/A.cs`, `${area}/B.cs`]);
const names: ClassReach = { reach: [area], sourceFiles: [`${area}/A.cs`], areaOnly: [] };

test('a class is reached by the file that declares what it names, not by its neighbours in the area', () => {
  assert.equal(reachedByFile(names, `${area}/A.cs`, area, declared), true);
  assert.equal(reachedByFile(names, `${area}/B.cs`, area, declared), false);
});

test('a file nothing is declared in, an area reached by a program or a hand reach, and an unknown keep the area grain', () => {
  assert.equal(reachedByFile(names, `${area}/data.json`, area, declared), true);
  assert.equal(reachedByFile({ ...names, areaOnly: [area] }, `${area}/B.cs`, area, declared), true);
  assert.equal(reachedByFile({ reach: [] }, `${area}/B.cs`, area, declared), true);
  assert.equal(reachedByFile({ reach: ['*'] }, 'elsewhere/X.cs', area, declared), true);
  assert.equal(reachedByFile(names, 'src/D/Driver/Other/X.cs', 'src/D/Driver/Other', declared), false);
  assert.equal(reachedByFile({ reach: [area] }, `${area}/B.cs`, area, declared), true);
});
