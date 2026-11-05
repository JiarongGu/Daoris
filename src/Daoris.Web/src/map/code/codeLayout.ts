/**
 * A repository's modules in layers by dependency (MAP3a): what uses a module sits above it, what it
 * depends on below. Rows are in name order, so the picture does not move between two looks at the
 * same file.
 *
 * @remarks
 * Longest-path layering: a module sits one row below the DEEPEST module that depends on it, so an
 * arrow always points down. A producer may record a cycle, so the walk ignores the edge that closes
 * one; every module is still placed exactly once. No graph library, for the reason the workspace
 * map gives: a repository is tens of modules, and a layout engine costs more than it buys.
 */
export function layerModules(
  ids: string[],
  dependencies: { from: string; to: string }[],
): string[][] {
  const known = new Set(ids);
  const users = new Map<string, string[]>(ids.map((id) => [id, []]));
  for (const { from, to } of dependencies) {
    if (known.has(from) && known.has(to) && from !== to) users.get(to)!.push(from);
  }

  const depth = new Map<string, number>();
  const walking = new Set<string>();
  const depthOf = (id: string): number => {
    const held = depth.get(id);
    if (held !== undefined) return held;
    // The edge that closes a cycle counts for nothing, rather than walking it forever.
    if (walking.has(id)) return -1;
    walking.add(id);
    const below = Math.max(-1, ...users.get(id)!.map(depthOf)) + 1;
    walking.delete(id);
    depth.set(id, below);
    return below;
  };

  const layers: string[][] = [];
  for (const id of [...ids].sort()) {
    const row = depthOf(id);
    (layers[row] ??= []).push(id);
  }
  return layers.filter(Boolean).map((row) => row.sort());
}
