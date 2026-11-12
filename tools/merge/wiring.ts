/**
 * What of a build file a test's result can depend on. A project reference, a `Using`, an analyzer's additional file, a comment, a
 * `global using` of a namespace and a group a property no file sets to true switches off say how a project is wired, and the
 * compile that runs once per tree verifies the wiring: a change to only these leaves every class's verdict standing. Anything
 * else in a build file (a package, a property, a setting) is kept, so it still reaches the classes it builds.
 */

const MSBUILD = /\.(csproj|props|targets)$/i;
const GLOBAL_USINGS = /(^|\/)GlobalUsings\.cs$/i;
const CONDITIONAL_GROUP = /<(ItemGroup|PropertyGroup)\b[^>]*\bCondition\s*=\s*"\s*'\$\((\w+)\)'\s*==\s*'true'\s*"[^>]*>[\s\S]*?<\/\1>/g;

export const isMsbuildFile = (path: string): boolean => MSBUILD.test(path);

/** The names a build file sets to true (`<Name>true</Name>`): a group conditioned on any other is switched off everywhere. */
export function propertiesSetTrue(texts: string[]): Set<string> {
  const names = new Set<string>();
  for (const text of texts) for (const found of text.matchAll(/<(\w+)>\s*true\s*<\/\1>/gi)) names.add(found[1] as string);
  return names;
}

/** The text of `path` reduced to what can change a result, or `text` as it is for a file that is neither a build file nor global usings. */
export function resultText(path: string, text: string, setTrue: Set<string>): string {
  if (GLOBAL_USINGS.test(path)) return text.replace(/^[ \t]*global[ \t]+using[ \t]+[\w.]+[ \t]*;[ \t]*\r?\n?/gm, '');
  if (!MSBUILD.test(path)) return text;
  return text
    .replace(/<!--[\s\S]*?-->/g, '')
    .replace(CONDITIONAL_GROUP, (group, _tag: string, property: string) => (setTrue.has(property) ? group : ''))
    .replace(/<ProjectReference\b[^>]*?(?:\/>|>[\s\S]*?<\/ProjectReference>)/g, '')
    .replace(/<(Using|AdditionalFiles)\b[^>]*?\/>/g, '')
    .replace(/<(ItemGroup|PropertyGroup)\b[^>]*>\s*<\/\1>/g, '')
    .replace(/\s+/g, '');
}
