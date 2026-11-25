import { declaredPath, readManifest } from './records.mjs';
import { GENERATED, compare } from './common.mjs';

/** The heading each area document keeps its additions under (the canon's `templates/area.md`). */
export const ADDING = 'Adding to it';

/** The cells of a row, trimmed: the area's table has four, `Kind`, `Place`, `Registered by`, `Fails until`. */
const CELLS = 4;

const isRow = (line) => /^\|.*\|\s*$/.test(line);
const isRule = (line) => /^\|[\s:|-]+\|\s*$/.test(line);
const cellsOf = (line) => line.trim().slice(1, -1).split('|').map((cell) => cell.trim());

/** The area documents the manifest declares: the files of its folder, the router (`README.md`) aside. */
function areaFiles(ctx) {
  const folder = declaredPath(readManifest(ctx.root), 'areas');
  if (!folder) return [];
  return ctx.files.filter((path) => path.startsWith(`${folder}/`) && path.endsWith('.md') && path.slice(folder.length + 1) !== 'README.md').sort(compare);
}

/**
 * One area's section: its rows, each `{ cells, line }`, and the rows it kept out with why. The section runs from its
 * heading to the next `## ` heading outside a code fence; a header row (its first cell `Kind`), a rule row and any line
 * that is not a table row (a sentence saying what the part takes) are not rows.
 */
function sectionOf(text) {
  const lines = text.split('\n');
  const start = lines.findIndex((line) => line.trim() === `## ${ADDING}`);
  if (start < 0) return null;
  const rows = [];
  const refused = [];
  let fenced = false;
  for (let i = start + 1; i < lines.length; i += 1) {
    const line = lines[i];
    if (/^\s*(```|~~~)/.test(line)) fenced = !fenced;
    if (fenced) continue;
    if (line.startsWith('## ')) break;
    if (!isRow(line) || isRule(line)) continue;
    const cells = cellsOf(line);
    if (cells[0].toLowerCase() === 'kind') continue;
    if (cells.length === CELLS && cells.every(Boolean)) rows.push({ cells, line: i + 1 });
    else refused.push({ line: i + 1, fact: `a row of ${cells.length} cells with ${cells.filter((cell) => !cell).length} empty, not ${CELLS} filled: ${line.trim()}` });
  }
  return { rows, refused };
}

/**
 * `docs/index/adding.md`: every area's *Adding to it* rows in one page, a heading per kind (`Add <kind>`, so a words search for
 * "add a machine setting" meets the heading) in the order of the kind's words, so "where does a new X go" is one read.
 * A row's cells are copied as the area writes them with its area's path after them; the rule a row points at stays in its area.
 */
export function addingPage(ctx) {
  const out = [
    '# Adding to it: where a new kind of thing goes',
    '',
    `${GENERATED} Each heading is one kind of addition a part takes, its lines copied from its area document's \`## ${ADDING}\`: the file or table that holds it, the registration it needs and what fails until it is there. The rule behind a kind is in its area, never restated here.`,
  ];
  const files = areaFiles(ctx);
  if (!files.length) {
    out.push('', 'No area documents are declared (`documents.areas` in `daoris.json`), so there is no row to gather.');
    return `${out.join('\n')}\n`;
  }
  const rows = [];
  const without = [];
  const refused = [];
  for (const path of files) {
    const section = sectionOf(ctx.read(path));
    if (!section) {
      without.push(path);
      continue;
    }
    for (const row of section.rows) rows.push({ ...row, path });
    for (const row of section.refused) refused.push({ ...row, path });
  }
  const byKind = (row) => row.cells[0].toLowerCase().replace(/^(an?|the) /, '');
  rows.sort((a, b) => compare(byKind(a), byKind(b)) || compare(a.path, b.path) || a.line - b.line);
  for (const { cells: [kind, place, registered, fails], path } of rows) {
    out.push('', `### Add ${kind}`, '', `- **Place:** ${place}`, `- **Registered by:** ${registered}`, `- **Fails until:** ${fails}`, `- **Area:** \`${path}\``);
  }
  if (refused.length) {
    out.push('', '## Rows read wrong', '');
    for (const row of refused) out.push(`- \`${row.path}\`:${row.line} ${row.fact}`);
  }
  if (without.length) out.push('', `These area documents have no \`## ${ADDING}\`: ${without.map((path) => `\`${path}\``).join(', ')}.`);
  return `${out.join('\n')}\n`;
}
