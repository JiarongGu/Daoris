import { join, posix } from 'node:path';
import { filterHolds, settingsFilter } from './settings.mjs';
import { constantsIn, reachesIn, stringsOf, traitsIn, typesIn } from './types.mjs';
import { namesIn } from './source.mjs';

const posixPath = (path) => path.split('\\').join('/');
const folderOf = (path) => posix.dirname(path);
const inFolder = (path, folder) => path.startsWith(`${folder}/`) && !/\/(bin|obj)\//.test(path.slice(folder.length));
const isProjectFile = (path) => /\.(csproj|props|targets)$/i.test(path);

/** What a file of a test project holds that acts on every test without being named: a global using, an assembly or module attribute, a module initializer. */
const UNNAMED = /\bglobal\s+using\b|\[\s*(?:assembly|module)\s*:|\bModuleInitializer\b/;

/**
 * The project and run settings a Process half's command names (`dotnet test <project> --settings <file>`), `/`-separated as the
 * repository's paths are, or null for a command that names either not: a half this cannot read runs whole.
 */
export function halfOf(run) {
  const words = run.trim().match(/"[^"]*"|'[^']*'|\S+/g)?.map((word) => word.replace(/^["']|["']$/g, '')) ?? [];
  if (words[0] !== 'dotnet' || words[1] !== 'test' || !words[2] || words[2].startsWith('-')) return null;
  const at = words.findIndex((word) => word === '--settings' || word === '-s');
  const settings = at >= 0 ? words[at + 1] : words.find((word) => /^--settings[=:]/.test(word))?.replace(/^--settings[=:]/, '');
  if (!settings) return null;
  const clean = (path) => posixPath(path).split('/').filter((part) => part && part !== '.').join('/');
  const project = clean(words[2]).replace(/\/[^/]+\.csproj$/i, '');
  return { project, settings: clean(settings) };
}

/** A half's command told other run settings (`file`, quoted): what `reachSettings` wrote, in place of the half's own. */
export function withSettings(run, file) {
  const words = run.trim().match(/"[^"]*"|'[^']*'|\S+/g) ?? [];
  const at = words.findIndex((word) => word === '--settings' || word === '-s');
  if (at < 0 || at + 1 >= words.length) throw new Error(`the command names no settings to replace: ${run}`);
  words[at + 1] = `"${file}"`;
  return words.join(' ');
}

/** A project's file, and each `ProjectReference` in it as `{ folder, compiles }`: `compiles` false for one built only to be started. */
function projectOf(folder, files, read) {
  const csproj = files.find((file) => folderOf(file) === folder && file.endsWith('.csproj'));
  if (!csproj) return null;
  const xml = read(csproj);
  const references = [];
  for (const [tag] of xml.matchAll(/<ProjectReference\b[^>]*?(?:\/>|>[\s\S]*?<\/ProjectReference>)/g)) {
    const include = /Include\s*=\s*"([^"]+)"/.exec(tag)?.[1];
    if (!include) continue;
    const compiles = !/ReferenceOutputAssembly\s*=\s*"false"|<ReferenceOutputAssembly>\s*false\s*</i.test(tag);
    references.push({ folder: folderOf(posix.normalize(posix.join(folder, posixPath(include)))), compiles });
  }
  const links = [...xml.matchAll(/<Compile\b[^>]*\bInclude\s*=\s*"([^"]+)"/g)].map((m) => posix.normalize(posix.join(folder, posixPath(m[1]))));
  const assembly = /<AssemblyName>\s*([^<\s]+)\s*<\/AssemblyName>/.exec(xml)?.[1] ?? null;
  return { csproj, references, links, assembly };
}

/** The folders of the projects a project compiles against, read through; never one built only to be started. */
export function referencedProjects(project, files, read) {
  const found = [];
  const visit = (folder) => {
    for (const reference of projectOf(folder, files, read)?.references ?? []) {
      if (!reference.compiles || found.includes(reference.folder)) continue;
      found.push(reference.folder);
      visit(reference.folder);
    }
  };
  visit(project);
  return found;
}

/** The source area a path is in: its source project's first folder (`…/Daoris.Desktop.Driver/Sessions`), or null at a project's root or outside. */
export function areaOf(path, sources) {
  for (const source of sources) {
    if (!path.startsWith(`${source}/`)) continue;
    const rest = path.slice(source.length + 1).split('/');
    return rest.length > 1 ? `${source}/${rest[0]}` : null;
  }
  return null;
}

/** Whether a file's strings name `name`: as a word of its own, a path's part or a file name, not inside another word. */
export const names = (strings, name) => new RegExp(`(?:^|[^A-Za-z0-9_.-])${name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}(?:$|[^A-Za-z0-9_-])`).test(strings);

/**
 * The census of one Process half (`half`: its project and settings, `halfOf`), read from the repository's `files` (paths
 * `/`-separated from its root) through `read`. It answers the half's `classes` (each with its full name, the files it is read
 * from, its traits, its `reach` and any hand-written `reaches`), the `sources` it compiles against, the `programs` it starts,
 * what its project's files hold (`files`), and anything it could not read (`problems`).
 */
export function censusOf(half, files, read) {
  const problems = [];
  const xml = files.includes(half.settings) ? read(half.settings) : null;
  const filter = xml === null ? null : settingsFilter(xml);
  if (filter === null) problems.push(`${half.settings} holds no TestCaseFilter`);
  const sources = referencedProjects(half.project, files, read);
  const project = projectOf(half.project, files, read);
  const projectFiles = files.filter((file) => inFolder(file, half.project) && file.endsWith('.cs'));

  // The test project: each file's types, names, strings and extension methods, read once.
  const parsed = new Map();
  const constants = new Map();
  for (const file of projectFiles) {
    const text = read(file);
    const { namespace, types, code, literal, extensions } = typesIn(text);
    parsed.set(file, { namespace, types, names: namesIn(code), strings: stringsOf(code, literal), extensions, unnamed: UNNAMED.test(code) });
    for (const [key, value] of constantsIn(text, types)) constants.set(key, value);
  }
  // Each type by its full name (a nested one `Outer+Inner`), its partial declarations merged.
  const byFull = new Map();
  const bySimple = new Map();
  for (const [file, { namespace, types }] of parsed) {
    for (const type of types) {
      const chain = [];
      for (let t = type; t; t = t.outer ? types.find((other) => other.name === t.outer && other.open < t.at && t.at < other.close) : null) chain.unshift(t.name);
      const full = `${namespace ? `${namespace}.` : ''}${chain.join('+')}`;
      const entry = byFull.get(full) ?? { full, name: type.name, files: new Set(), decls: [], topLevel: type.topLevel };
      entry.files.add(file);
      entry.decls.push(type);
      byFull.set(full, entry);
      if (!bySimple.has(type.name)) bySimple.set(type.name, []);
      if (!bySimple.get(type.name).includes(entry)) bySimple.get(type.name).push(entry);
    }
  }
  const baseOf = (entry) => {
    for (const decl of entry.decls) {
      for (const base of decl.bases) {
        const found = (bySimple.get(base) ?? []).find((other) => other !== entry && other.decls.some((d) => d.kind === 'class'));
        if (found) return found;
      }
    }
    return null;
  };
  const chainOf = (entry) => {
    const out = [];
    for (let at = entry; at && !out.includes(at); at = baseOf(at)) out.push(at);
    return out;
  };
  const modified = (entry, word) => entry.decls.some((d) => d.modifiers.includes(word));
  const testClasses = [...byFull.values()].filter((entry) => entry.decls.some((d) => d.kind === 'class') && !modified(entry, 'abstract')
    && !modified(entry, 'static') && chainOf(entry).some((each) => each.decls.some((d) => d.tests)));

  // What each test-project file gives the files that name it: its top-level types and its extension methods.
  const givenBy = new Map();
  const give = (name, file) => {
    if (!givenBy.has(name)) givenBy.set(name, new Set());
    givenBy.get(name).add(file);
  };
  for (const entry of byFull.values()) if (entry.topLevel) for (const file of entry.files) give(entry.name, file);
  for (const [file, { extensions }] of parsed) for (const name of extensions) give(name, file);

  // The source projects' top-level types and extension methods, each by the files that declare it.
  const defined = new Map();
  for (const source of sources) {
    for (const file of files) {
      if (!inFolder(file, source) || !file.endsWith('.cs')) continue;
      const { types, extensions } = typesIn(read(file));
      for (const name of [...types.filter((type) => type.topLevel).map((type) => type.name), ...extensions]) {
        if (!defined.has(name)) defined.set(name, new Set());
        defined.get(name).add(file);
      }
    }
  }
  // The programs the test project builds only to start: each an area of its own, with what it compiles in.
  const programs = (project?.references ?? []).filter((reference) => !reference.compiles).map(({ folder }) => {
    const program = projectOf(folder, files, read);
    const linked = [...new Set((program?.links ?? []).map((path) => areaOf(path, sources)).filter(Boolean))];
    const everything = (program?.references ?? []).some((reference) => reference.compiles && sources.includes(reference.folder));
    // Named by its project's folder, or a file of its build (`daoris-driver.dll`): its assembly's name alone is a word strings
    // hold for other reasons (the launcher's is `Daoris`, the host's a lease's reason).
    const built = program?.assembly ? [`${program.assembly}.dll`, `${program.assembly}.exe`] : [];
    return { folder, names: [posix.basename(folder), ...built], linked, everything };
  });

  const traitMap = (pairs) => {
    const map = new Map();
    for (const [name, value] of pairs) {
      if (!map.has(name)) map.set(name, new Set());
      map.get(name).add(value);
    }
    return map;
  };
  const classes = [];
  for (const entry of testClasses) {
    const chain = chainOf(entry);
    const classPairs = chain.flatMap((each) => each.decls.flatMap((d) => traitsIn(d.attributes, constants)));
    const traits = traitMap(classPairs);
    // In the half when, for one of its tests by its full name, the class's traits hold its filter, or that test's own with them do
    // (a class whose git cases alone are Process).
    const tests = chain.flatMap((each) => each.decls.flatMap((d) => d.methods));
    const inHalf = filter === null || tests.some((attributes) => {
      const name = `${entry.full}.${/(\w+)\s*$/.exec(attributes)?.[1] ?? ''}`;
      return filterHolds(filter, traits, name) || filterHolds(filter, traitMap([...classPairs, ...traitsIn(attributes, constants)]), name);
    });
    if (!inHalf) continue;
    for (const [name, value] of tests.flatMap((attributes) => traitsIn(attributes, constants))) {
      if (!traits.has(name)) traits.set(name, new Set());
      traits.get(name).add(value);
    }
    // Its files: its own and its bases', then each test-project file giving what they name, read through.
    const reading = new Set(chain.flatMap((each) => [...each.files]));
    const queue = [...reading];
    while (queue.length) {
      const file = queue.shift();
      for (const name of parsed.get(file)?.names ?? []) {
        for (const other of givenBy.get(name) ?? []) {
          if (!reading.has(other)) {
            reading.add(other);
            queue.push(other);
          }
        }
      }
    }
    const reach = new Set();
    const strings = [...reading].map((file) => parsed.get(file)?.strings ?? '').join(' ');
    for (const file of reading) {
      for (const name of parsed.get(file)?.names ?? []) {
        for (const source of defined.get(name) ?? []) {
          const area = areaOf(source, sources);
          if (area) reach.add(area);
        }
      }
    }
    for (const program of programs) {
      if (!program.names.some((name) => names(strings, name))) continue;
      reach.add(program.folder);
      for (const area of program.linked) reach.add(area);
      if (program.everything) reach.add('*');
    }
    const reaches = chain.flatMap((each) => each.decls.flatMap((d) => reachesIn(d.attributes)));
    for (const hand of reaches) {
      if (!hand.why) problems.push(`${entry.full}: a [Reaches] without its reason (Why = "…")`);
      for (const area of hand.areas) {
        const full = area === '*' ? '*'
          : [...sources, ...programs.map((program) => program.folder)].map((folder) => {
            const base = posix.basename(folder);
            if (area === base) return programs.some((program) => program.folder === folder) ? folder : null;
            return area.startsWith(`${base}/`) ? `${folderOf(folder)}/${area}` : null;
          }).find(Boolean);
        if (full) reach.add(full);
        else problems.push(`${entry.full}: [Reaches("${area}")] names no folder this half compiles against or starts`);
      }
    }
    classes.push({
      name: entry.full.split('.').at(-1), full: entry.full, files: [...reading].sort(),
      traits: Object.fromEntries([...traits].map(([name, values]) => [name, [...values].sort()])),
      reach: [...reach].sort(), reaches, strings,
    });
  }
  classes.sort((a, b) => a.full.localeCompare(b.full));
  return {
    half, filter, sources, programs, classes, problems,
    // Each test-project file: whether it acts unnamed, and whether it declares test classes alone (a class's own file).
    files: Object.fromEntries([...parsed].map(([file, { unnamed }]) => {
      const tops = [...byFull.values()].filter((entry) => entry.topLevel && entry.files.has(file));
      return [file, { unnamed, own: tops.length > 0 && tops.every((entry) => testClasses.includes(entry)) }];
    })),
  };
}

/**
 * What one changed path reaches of a half, by its census: `{ whole: why }`, every class; `{ areas }`, the classes whose reach
 * holds one; `{ classes }`, those named; or `{ none: why }`. Asked only of a path the lane table sends to the half's gate.
 */
export function pathReach(path, census) {
  const { half, sources, programs } = census;
  const among = (test) => census.classes.filter(test).map((each) => each.full);
  if (inFolder(path, half.project)) {
    if (isProjectFile(path)) return { whole: `${path} is the test project's own project file` };
    if (path.endsWith('.cs')) {
      const file = census.files[path];
      if (!file) return { whole: `${path} is a test source this tree does not hold` };
      if (file.unnamed) return { whole: `${path} acts on every test unnamed (a global using, an assembly attribute, a module initializer)` };
      const classes = among((each) => each.files.includes(path));
      return classes.length ? { classes } : { none: `no class of the half reads ${path}` };
    }
    // A fixture or a golden: the classes whose strings name it, or a folder of the test project above it.
    const parts = path.slice(half.project.length + 1).split('/');
    const words = [parts.at(-1), parts.at(-1).replace(/\.[^.]+$/, ''), ...parts.slice(0, -1)];
    const classes = among((each) => words.some((word) => names(each.strings, word)));
    return classes.length ? { classes } : { none: `no class of the half names ${path}` };
  }
  const area = areaOf(path, sources);
  if (area) return { areas: [area] };
  const source = sources.find((folder) => path.startsWith(`${folder}/`));
  if (source) return { whole: `${path} is at ${source}'s root (its project file)` };
  const program = programs.find((each) => path.startsWith(`${each.folder}/`));
  if (program) return { areas: [program.folder] };
  // Another project beside the test project's, its project file too: nothing compiles against it and the tests start nothing
  // of it, so only a test naming it reads it.
  const beside = folderOf(half.project);
  const rest = path.startsWith(`${beside}/`) ? path.slice(beside.length + 1).split('/') : [];
  if (rest.length > 1) {
    const classes = among((each) => names(each.strings, rest[0]));
    return classes.length ? { classes } : { none: `no class of the half compiles against, starts or names ${rest[0]}` };
  }
  return { whole: `no class's reach places ${path}` };
}

/**
 * Which of a half's classes `paths` reach, each with the first path that reaches it, and the paths that reach the whole half.
 * A class with no reach is reached by any path; `*` in its reach is every area.
 */
export function classesReached(paths, census) {
  const whole = [];
  const by = new Map();
  for (const path of paths) {
    const placed = pathReach(path, census);
    if (placed.whole) {
      whole.push({ path, why: placed.whole });
      continue;
    }
    // A path no class reads, by what every class's files name and say, reaches none: one with no reach is read too. A file
    // of the test project reaches the classes that read it, the same reading. A source area is where a class with no reach,
    // whose sources the census could not place, is reached.
    if (placed.none) continue;
    for (const each of census.classes) {
      if (by.has(each.full)) continue;
      const hit = placed.classes
        ? placed.classes.includes(each.full)
        : !each.reach.length || each.reach.includes('*') || placed.areas.some((area) => each.reach.includes(area));
      if (hit) by.set(each.full, path);
    }
  }
  if (whole.length) return { whole, classes: census.classes.map((each) => each.full), by: new Map(census.classes.map((each) => [each.full, whole[0].path])) };
  return { whole, classes: census.classes.filter((each) => by.has(each.full)).map((each) => each.full), by };
}

// ---------------------------------------------------------------------------------------------------
// The tool
