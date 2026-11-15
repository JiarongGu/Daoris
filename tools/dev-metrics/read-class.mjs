/** The classes a tool result is counted under, in the order a path is tried; a path that matches none is code. */
export const CLASSES = ['brief', 'area', 'index', 'backlog', 'library', 'code'];

const START = String.raw`(?:^|[/\s])`;
const RULES = [
  ['library', new RegExp(`${START}docs/(?:decisions|archive)/|${START}docs/(?:task-archive|FIX-LOG|backlog-parked)\\.md`)],
  ['area', new RegExp(`${START}docs/areas/`)],
  ['index', new RegExp(`${START}docs/index/|${START}\\.claude/INDEX\\.md`)],
  ['backlog', new RegExp(`${START}TASKS\\.md`)],
  ['brief', new RegExp(`${START}(?:AGENTS|CLAUDE)\\.md`)],
];

/** The path a tool call names (a read's file, a search's folder) or, for a shell, its command line. */
export function targetOf(input) {
  const named = input?.file_path ?? input?.path ?? input?.command ?? '';
  return typeof named === 'string' ? named.replace(/\\/g, '/') : '';
}

/** The class of the document a tool call read, from its input. */
export function classOf(input) {
  const target = targetOf(input);
  return RULES.find(([, pattern]) => pattern.test(target))?.[0] ?? 'code';
}
