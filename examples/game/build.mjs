// The example game's build: it reads the engine game pins, as `daoris family materialize` wrote it under pinned/engine, never the
// engine's checkout beside it, and fails where that engine is not there or lacks a call game makes.
import { existsSync, readFileSync } from 'node:fs';

const NEEDED = ['advance', 'render'];
const folder = 'pinned/engine';
// The tracked example pins nothing, its engine being no repository with a line; a copy that pins one builds against it.
if (!existsSync('daoris.family.lock')) {
  console.log('game pins no engine commit: nothing to build against');
  process.exit(0);
}
if (!existsSync(`${folder}/.daoris-pin`)) {
  console.error(`game does not build: no engine at ${folder}; run \`daoris family materialize\` first`);
  process.exit(1);
}
const { commit } = JSON.parse(readFileSync(`${folder}/.daoris-pin`, 'utf8'));
const { calls } = JSON.parse(readFileSync(`${folder}/interface.json`, 'utf8'));
const missing = NEEDED.filter((call) => !calls.includes(call));
if (missing.length > 0) {
  console.error(`game does not build against engine at ${commit.slice(0, 12)}: it lacks ${missing.join(', ')}`);
  process.exit(1);
}
console.log(`game builds against engine at ${commit.slice(0, 12)}`);
