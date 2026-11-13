import { test } from 'vitest';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { PassedClasses, unitId } from './class-reuse.ts';
import { fixtureFolder } from './fixture-home.ts';
import { planClasses } from './full-replay.ts';
import { fullReplayOf, keyContextOf, storeFile } from '../merge-branch/full-set.mjs';

const root = fileURLToPath(new URL('../..', import.meta.url));
const GATE = { name: 'driver-process-git', run: 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --settings src/Daoris.Desktop/git.process.runsettings' };

test("a Process half's run is narrowed by this repository's census to the classes no standing pass covers", () => {
  const { dir, remove } = fixtureFolder('full-set');
  try {
    const world = keyContextOf(root);
    const units = world.map.units.filter((unit: { kind: string; suite: string }) => unit.kind === 'dotnet' && unit.suite === GATE.name);
    assert.ok(units.length > 5, 'the census reads the half\'s classes');
    const say = () => {};
    const none = fullReplayOf({ root, checkout: dir, dir, commit: 'aaaaaaa', say, world }).narrow(GATE);
    assert.equal(none?.run, GATE.run, 'with no pass standing it runs as declared');
    assert.equal(none?.replay.replayedCount, 0);

    const store = new PassedClasses(storeFile(dir));
    const keys = planClasses(units, world.context, store).keys;
    for (const unit of units.slice(1)) store.add(unitId(unit), keys.get(unitId(unit)) as string, '2026-11-12T10:00:00Z', 'abc1234');
    store.save();
    const some = fullReplayOf({ root, checkout: dir, dir, commit: 'aaaaaaa', say, world }).narrow(GATE);
    assert.equal(some?.replay.replayedCount, units.length - 1);
    assert.equal(some?.replay.ran, 1);
    const file = /--settings "?([^"\s]+)"?/.exec(String(some?.run))?.[1] as string;
    assert.ok(existsSync(file), 'the narrowed run settings are written beside the log');
    const xml = readFileSync(file, 'utf8').replaceAll('&amp;', '&');
    assert.ok(xml.includes(`FullyQualifiedName!~${units[1]!.unit.replace(/[\\()&|=!~]/g, '\\$&')}.`));
    assert.ok(!xml.includes(`FullyQualifiedName!~${units[0]!.unit}.`), 'the class with no pass is not left out');

    store.add(unitId(units[0]!), keys.get(unitId(units[0]!)) as string, '2026-11-12T10:00:00Z', 'abc1234');
    store.save();
    const all = fullReplayOf({ root, checkout: dir, dir, commit: 'aaaaaaa', say, world }).narrow(GATE);
    assert.equal(all?.run, null, 'every class replayed: nothing runs');
    assert.equal(join(dir, 'local', 'reached-classes.json'), storeFile(dir));
  } finally {
    remove();
  }
}, 400_000);
