const held = new WeakMap<readonly string[], Set<string>>();

/**
 * Every folder above a tracked file, for the list handed in. The scan asks once per .cs file for the same list, and building
 * the set each time was minutes of the tool tests' wall time, so it is built once for a list.
 */
export function folderSet(files: readonly string[]): Set<string> {
  let folders = held.get(files);
  if (!folders) {
    folders = new Set(files.flatMap((path) => path.split('/').slice(0, -1).map((_, i, parts) => parts.slice(0, i + 1).join('/'))));
    held.set(files, folders);
  }
  return folders;
}
