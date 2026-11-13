import { readProjects } from './projects.mjs';

/**
 * Lyntai's memory and storage types in the application: its storage namespaces, its graph engine, its graph store and its migrations.
 * They are the Memory.Lyntai adapter's, and the application reaches each repository's memory through the port a host hands it. The
 * allowance is none, so a file that names one is a breach; Lyntai's vector seams (`Lyntai.Inference`, `IVectorStore`) are not this.
 */
const STORAGE = /\bLyntai\.Storage\b|\bLyntai\.Memory\.Engines\b|\b(?:GraphMemoryEngine|GraphMemoryOptions|GraphMemorySeams|SqliteMemoryGraphStore|I(?:Typed(?:Link)?|Countable)?MemoryGraphStore|MigrationRunnerService|SqliteConnectionFactory)\b/g;

/** The occurrences in one source's text, comments aside. */
export function memorySites(text) {
  let count = 0;
  for (const raw of text.split('\n')) {
    const line = raw.trim();
    if (line.startsWith('//') || line.startsWith('*') || line.startsWith('/*')) continue;
    count += (line.match(STORAGE) ?? []).length;
  }
  return count;
}

/** Each source of an application project that names one of Lyntai's memory or storage types, with its count. */
export function checkMemory(tree) {
  const held = readProjects(tree).filter((project) => project.role === 'application');
  const problems = [];
  for (const path of tree.files) {
    if (!path.endsWith('.cs') || /(^|\/)(bin|obj)\//.test(path)) continue;
    if (!held.some((project) => path.startsWith(`${project.folder}/`))) continue;
    const count = memorySites(tree.read(path));
    if (count > 0) problems.push(`${path} names Lyntai's memory or storage types ${count} times: they are the Memory.Lyntai adapter's, reached through the memory port`);
  }
  return problems;
}
