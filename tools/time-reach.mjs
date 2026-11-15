/**
 * Whether a merge's changed paths reach a test the time ratchet found over its limit. A test over its limit that no changed
 * path reaches is not the merge's: it is slow on this machine under this load, and the merge neither made it slower nor can
 * make it faster, so the ratchet names it and fails nothing. A test a path reaches stays the merge's, judged as before.
 *
 * A test's file is the one that holds it: a `file::name` key (Vitest, the node reporter) names it; a dotnet key
 * `Namespace.Class.Method` is the tracked `.cs` file named for its class, or for the start of its name (a partial class's
 * files). A path reaches it when it is that file, a file beside it (its helpers and fixtures), a file named for what the
 * class tests (`LineAccount.cs` for `LineAccountTests`), or a build or dependency file that every test reads. A test whose file
 * cannot be found is reached: an unknown is judged, never excused.
 */
import { basename, dirname } from 'node:path/posix';

/** Files every test of a tree reads, whatever it tests. */
const SHARED = /(^|\/)(Directory\.[^/]+|[^/]+\.(csproj|props|targets|runsettings)|package(-lock)?\.json|vite\.config\.[^/]+|vitest\.config\.[^/]+|tsconfig[^/]*\.json|\.editorconfig|daoris\.lanes\.json|global\.json)$/;

const stemOf = (path) => basename(path).replace(/\.[^.]*$/, '').replace(/\.test$/, '');
const subjectOf = (name) => name.replace(/(Census)?Tests?$/, '');

/** The tracked file holding `key`'s test, or null where none can be found. */
export function testFile(key, files) {
  if (key.includes('::')) return key.split('::')[0].replace(/\\/g, '/');
  const parts = key.split('(')[0].split('.');
  const name = (parts.at(-2) ?? '').split('+')[0];
  if (!name) return null;
  const cs = files.filter((file) => file.endsWith('.cs'));
  return cs.find((file) => stemOf(file) === name) ?? cs.find((file) => stemOf(file).length >= 6 && name.startsWith(stemOf(file))) ?? null;
}

/** Whether any of `changed` reaches the test `key`, given the repository's tracked `files`. */
export function reaches(key, changed, files) {
  const file = testFile(key, files);
  if (file === null) return true;
  const name = key.includes('::') ? stemOf(file) : (key.split('(')[0].split('.').at(-2) ?? '').split('+')[0];
  const subject = subjectOf(name);
  return changed.some((path) => {
    if (path === file || dirname(path) === dirname(file) || SHARED.test(path)) return true;
    const stem = stemOf(path);
    return subject.length >= 4 && stem.length >= 4 && (subject.includes(stem) || stem.includes(subject));
  });
}

/** `found` (key -> { ms, limit }) split into the tests the merge's `changed` paths reach and those they do not. */
export function splitByReach(found, changed, files) {
  const mine = {};
  const others = {};
  for (const [key, over] of Object.entries(found)) (reaches(key, changed, files) ? mine : others)[key] = over;
  return { mine, others };
}
