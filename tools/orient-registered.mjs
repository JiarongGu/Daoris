/**
 * The registered parts, for `docs/index/registered.md`: each seam's marks, read as text, in the order their root runs them.
 *
 * A seam is a sealed subclass of `PartAttribute` (`src/Daoris.Contracts/Parts/`) whose constructor takes the name, the phase
 * enum and the parts it comes after, in that order; its marks are `[<Seam>("name", <Phase>.<Value>, "after", …)]`. The order is
 * `PartOrder.Of`'s: phase by phase in the enum's order, then each part after the parts it names, ties by code unit. A mark that
 * order refuses (a name declared twice, an after that is no part or runs later, a cycle) is listed apart with why: the root's
 * parts test fails on it, and the page still says where it is.
 */

const SEAM = /\bclass\s+([A-Z]\w*)Attribute\s*\(\s*string\s+\w+\s*,\s*([A-Z]\w*)\s+\w+[^)]*\)\s*:\s*PartAttribute\b/g;

const compare = (a, b) => (a < b ? -1 : a > b ? 1 : 0);

const lineOf = (text, offset) => text.slice(0, offset).split('\n').length;

/** A C# text with its comments blanked, offsets kept, so a commented-out mark or enum member is not read. */
function code(text) {
  return text.replace(/\/\*[\s\S]*?\*\/|\/\/[^\n]*/g, (comment) => comment.replace(/[^\n]/g, ' '));
}

function sources(ctx) {
  return ctx.files.filter((path) => path.startsWith('src/') && path.endsWith('.cs') && !/\.Tests\//.test(path));
}

/** Each seam the sources declare: its mark's name and its phase enum's members, in their order. */
function seams(ctx, files) {
  const found = [];
  const enums = new Map();
  for (const file of files) {
    const text = code(ctx.read(file));
    for (const match of text.matchAll(SEAM)) found.push({ seam: match[1], phase: match[2], file });
    for (const match of text.matchAll(/\benum\s+([A-Z]\w*)\s*\{([^}]*)\}/g)) {
      const members = match[2].replace(/\[[^\]]*\]/g, '').split(',').map((part) => /^\s*([A-Za-z_]\w*)/.exec(part)?.[1]).filter(Boolean);
      enums.set(match[1], members);
    }
  }
  return found.map((seam) => ({ ...seam, phases: enums.get(seam.phase) ?? [] })).sort((a, b) => compare(a.seam, b.seam));
}

function marks(ctx, files, { seam, phase }) {
  const mark = new RegExp(`\\[\\s*${seam}\\s*\\(\\s*"([^"]+)"\\s*,\\s*${phase}\\.(\\w+)((?:\\s*,\\s*"[^"]*")*)\\s*\\)\\s*\\]`, 'g');
  const found = [];
  for (const file of files) {
    const text = code(ctx.read(file));
    for (const match of text.matchAll(mark)) {
      const after = [...match[3].matchAll(/"([^"]*)"/g)].map((each) => each[1]);
      const type = /\b(?:class|record)\s+([A-Z]\w*)/.exec(text.slice(match.index + match[0].length))?.[1] ?? '';
      found.push({ name: match[1], phase: match[2], after, type, file, line: lineOf(text, match.index) });
    }
  }
  return found.sort((a, b) => compare(a.name, b.name) || compare(a.file, b.file) || a.line - b.line);
}

/** The marks in their root's order, and the ones that order refuses, each with why. */
export function orderParts(found, phases) {
  const refused = [];
  const counts = new Map();
  for (const part of found) counts.set(part.name, (counts.get(part.name) ?? 0) + 1);
  const byName = new Map(found.filter((part) => counts.get(part.name) === 1).map((part) => [part.name, part]));
  const rank = (part) => phases.indexOf(part.phase);
  for (const part of found) {
    const why = counts.get(part.name) > 1 ? 'declared twice'
      : rank(part) < 0 ? `${part.phase} is no phase`
      : part.after.map((after) => (!byName.has(after) ? `${after} is no part`
        : rank(byName.get(after)) > rank(part) ? `${after} runs in a later phase` : null)).find(Boolean);
    if (why) refused.push({ ...part, why });
  }

  const ordered = [];
  const refusedNames = new Set(refused.map((part) => part.name));
  for (const phase of phases) {
    const here = found.filter((part) => part.phase === phase && !refusedNames.has(part.name));
    const names = new Set(here.map((part) => part.name));
    const waiting = new Map(here.map((part) => [part.name, new Set(part.after.filter((after) => names.has(after)))]));
    for (;;) {
      const next = [...waiting].filter(([, after]) => after.size === 0).map(([name]) => name).sort(compare)[0];
      if (next === undefined) break;
      waiting.delete(next);
      ordered.push({ ...byName.get(next), phase });
      for (const after of waiting.values()) after.delete(next);
    }
    for (const name of [...waiting.keys()].sort(compare)) refused.push({ ...byName.get(name), why: 'waits on a cycle' });
  }
  return { ordered, refused };
}

/** `docs/index/registered.md`'s text, its first line `generated`. */
export function registeredPage(ctx, generated) {
  const files = sources(ctx);
  const out = [
    '# Registered parts',
    '',
    `${generated} Each root's parts by seam, in the order the root runs them: phase by phase, each after the parts it names, ties by name (\`PartOrder.Of\`). Each row names the mark's file and line.`,
  ];
  const all = seams(ctx, files);
  if (!all.length) out.push('', 'No seam is declared in this tree.');
  for (const seam of all) {
    const { ordered, refused } = orderParts(marks(ctx, files, seam), seam.phases);
    out.push('', `## \`${seam.seam}\``, '', `Phases, in order: ${seam.phases.join(', ') || '—'}. Declared in \`${seam.file}\`.`, '');
    if (!ordered.length && !refused.length) {
      out.push('No part is registered.');
      continue;
    }
    out.push('| # | Part | Phase | After | Class | Where |', '|---|---|---|---|---|---|');
    ordered.forEach((part, i) => out.push(
      `| ${i + 1} | \`${part.name}\` | ${part.phase} | ${part.after.map((after) => `\`${after}\``).join(', ') || '—'} | \`${part.type}\` | \`${part.file}:${part.line}\` |`));
    for (const part of refused) {
      out.push(`| refused: ${part.why} | \`${part.name}\` | ${part.phase} | ${part.after.join(', ') || '—'} | \`${part.type}\` | \`${part.file}:${part.line}\` |`);
    }
  }
  return `${out.join('\n')}\n`;
}
