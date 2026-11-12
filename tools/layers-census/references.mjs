import { ALLOWED_PACKAGE, readProjects } from './projects.mjs';

const isAdapter = (name) => name.startsWith('Daoris.Adapters.');

/** The packages a fake runs on because the fake is the real engine still: the store's fake is SQLite in memory (design §5.1). */
const FAKE_ENGINES = ['Microsoft.Data.Sqlite', 'SQLitePCLRaw.bundle_e_sqlite3'];

/**
 * The reference graph's breaches. Ports references nothing but analyzers; Testing references Ports and the test libraries; the
 * application assemblies and every unit-test project reference no adapter and no package but analyzers and the test libraries,
 * except those `list.packages` names per project, which only shrinks. In a unit test process nothing real is then reachable.
 */
export function checkReferences(tree, list) {
  const problems = [];
  const projects = readProjects(tree);
  for (const project of projects) {
    const outside = project.packages.filter((name) => !ALLOWED_PACKAGE.test(name));
    if (project.role === 'ports') {
      for (const name of project.projects) problems.push(`${project.name} references the project ${name}: the ports reference the standard library alone`);
      for (const name of outside) problems.push(`${project.name} takes the package ${name}: the ports reference the standard library alone`);
    } else if (project.role === 'testing') {
      for (const name of project.projects.filter((each) => each !== 'Daoris.Ports')) {
        problems.push(`${project.name} references the project ${name}: the fakes reference the ports and the test libraries alone`);
      }
      for (const name of outside.filter((each) => !FAKE_ENGINES.includes(each))) {
        problems.push(`${project.name} takes the package ${name}: the fakes reference the ports and the test libraries alone, and the store's engine`);
      }
    } else if (project.role === 'application' || project.role === 'unit-tests') {
      for (const name of project.projects.filter(isAdapter)) {
        problems.push(`${project.name} references the adapter ${name}: only a composition root does, and a unit test runs on fakes`);
      }
      const allowed = list.packages[project.name] ?? [];
      for (const name of outside.filter((each) => !allowed.includes(each))) {
        problems.push(`${project.name} takes the package ${name}: an application and its unit tests take analyzers and test libraries alone`);
      }
      for (const name of allowed.filter((each) => !outside.includes(each))) {
        problems.push(`${project.name}: ${name} is on the list and no longer referenced: write the list again (node tools/layers-census.mjs --write)`);
      }
    }
  }
  const named = new Set(projects.map((project) => project.name));
  for (const name of Object.keys(list.packages)) {
    if (!named.has(name)) problems.push(`${name} is on the package list and is no project: write the list again (node tools/layers-census.mjs --write)`);
  }
  return problems;
}

/** The packages each application and unit-test project takes beyond the allowed kinds, for the list's first writing. */
export function packagesOutside(tree) {
  const found = {};
  for (const project of readProjects(tree)) {
    if (project.role !== 'application' && project.role !== 'unit-tests') continue;
    const outside = project.packages.filter((name) => !ALLOWED_PACKAGE.test(name));
    if (outside.length) found[project.name] = outside.sort();
  }
  return found;
}
