import { posix } from 'node:path';

/** The assemblies that hold the application: the driver until it dissolves, the service's core until its policy moves. */
export const APPLICATIONS = ['Daoris.Desktop.Driver', 'Daoris.Application', 'Daoris.Service.Core', 'Daoris.Service.Application'];

/** A package a unit tier may name without a list: the test libraries and analyzers. */
export const ALLOWED_PACKAGE = /^(Microsoft\.NET\.Test\.Sdk|xunit(\..+)?|coverlet\..+|.*Analyzers?)$/;

const ADAPTERS_ROOT = 'src/Daoris.Adapters/';

/** Each project file of `files` as its name, its folder, what it references and the role the census holds it to. */
export function readProjects({ files, read }) {
  return files.filter((path) => path.endsWith('.csproj')).map((path) => {
    const text = read(path);
    const name = posix.basename(path, '.csproj');
    const refs = [...text.matchAll(/<ProjectReference\s+Include="([^"]+)"/g)].map((m) => m[1].replace(/\\/g, '/'));
    return {
      path,
      name,
      folder: posix.dirname(path),
      projects: refs.map((ref) => posix.basename(ref, '.csproj')),
      packages: [...text.matchAll(/<PackageReference\s+Include="([^"]+)"/g)].map((m) => m[1]),
      role: roleOf(name, path),
    };
  });
}

function roleOf(name, path) {
  if (name === 'Daoris.Ports') return 'ports';
  if (name === 'Daoris.Testing') return 'testing';
  if (path.startsWith(ADAPTERS_ROOT)) return 'adapter';
  if (APPLICATIONS.includes(name)) return 'application';
  if (name.endsWith('.Tests')) return 'unit-tests';
  return 'other';
}
