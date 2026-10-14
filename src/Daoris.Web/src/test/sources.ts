/**
 * The page's sources as a gate over the whole tree reads them (STRUCT5c, D166): a glob from the project's root,
 * `import.meta.glob('/src/**…')`, whose keys this names as the sources' top names them, `./work/Composer.tsx`. A relative
 * glob's keys start from the file that globs, so a gate moved into a folder would read the tree from there and say every
 * path another way; a rooted glob reads it the same from any folder.
 */
export function rooted<T>(found: Record<string, T>): Record<string, T> {
  return Object.fromEntries(Object.entries(found).map(([path, value]) => [path.replace(/^\/src\//, './'), value]));
}
