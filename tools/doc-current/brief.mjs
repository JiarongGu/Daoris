/**
 * The brief: the root instruction file every session reads whole. It is held to two facts, so they fail rather than report:
 * what it names exists, and its before-done commands are exactly the hand-back gates `daoris.gates.json` declares.
 */

/** The brief's file, whose own part (outside the generated region) `doc-current` reads. */
export const BRIEF = 'AGENTS.md';

const START = '<!-- daoris:rules';
const END = '<!-- /daoris:rules';

/** The brief's lines with the generated region blanked, so a span's line number stays the file's. */
export function outsideRegion(lines) {
  const from = lines.findIndex((line) => line.startsWith(START));
  if (from < 0) return lines;
  const to = lines.findIndex((line, at) => at > from && line.startsWith(END));
  return lines.map((line, at) => (at >= from && (to < 0 || at <= to) ? '' : line));
}

/** The commands the hand-back gates declare: a gate's `handback` string, or its `run` where `handback` is true. */
export function declaredHandback(gates) {
  return (gates?.gates ?? []).flatMap((gate) => {
    if (typeof gate.handback === 'string') return [gate.handback];
    return gate.handback === true ? [gate.run] : [];
  });
}

/** The code spans of the brief's before-done item: from `Before done:` to the end of its list item. */
export function saidHandback(text) {
  const at = text.indexOf('Before done:');
  if (at < 0) return [];
  const rest = text.slice(at + 'Before done:'.length);
  const end = rest.search(/\n\s*\n|\n\s*- /);
  return [...(end < 0 ? rest : rest.slice(0, end)).matchAll(/`([^`]+)`/g)].map((match) => match[1].trim());
}

/** The brief's before-done commands held against the declared ones, both ways. */
export function compareHandback(text, gates) {
  const said = saidHandback(text);
  const declared = declaredHandback(gates);
  const missing = declared.filter((command) => !said.includes(command));
  const extra = said.filter((command) => !declared.includes(command));
  return { said, declared, missing, extra, differs: missing.length > 0 || extra.length > 0 };
}
