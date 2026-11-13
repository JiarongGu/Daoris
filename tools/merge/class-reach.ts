/**
 * Whether a test class is reached by one changed file of a source project: by the file that declares a type the class names, not
 * by the whole area (the source project's first folder) the file is in. A file the census holds no declaration of (a resource, a
 * project's data) and an area a class reaches by its program or a hand-written `[Reaches]` keep the area's grain, so the finer
 * answer is never narrower than what the class's files name.
 */

/** What a class of a Process half's census says of its reach. */
export type ClassReach = { reach: readonly string[]; sourceFiles?: readonly string[]; areaOnly?: readonly string[] };

/** Whether `cls` is reached by `path`, a file in `area`; `declared` holds the source files the census saw declare a type or an extension. */
export function reachedByFile(cls: ClassReach, path: string, area: string, declared: ReadonlySet<string>): boolean {
  if (!cls.reach.length || cls.reach.includes('*')) return true;
  if (!cls.reach.includes(area)) return false;
  if (!cls.sourceFiles || !declared.has(path)) return true;
  return cls.areaOnly?.includes(area) === true || cls.sourceFiles.includes(path);
}
