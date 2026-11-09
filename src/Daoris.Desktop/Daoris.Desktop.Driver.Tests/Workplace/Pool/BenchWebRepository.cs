using System.Text;
using System.Text.Json;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The bench's second repository shape, generated: a web-only npm project (a type check, then a bundler) of a few hundred modules
/// and a day of commits, each changing a few of them, which a tree runs with the found layer's generic rules alone. Its two tools are
/// pinned at the versions the checkout's web project locks, so its install is answered from the machine's npm cache.
/// </summary>
internal static class BenchWebRepository
{
    private const int Modules = 240;

    /// <summary>The repository made at <paramref name="origin"/>, its lockfile written by npm; its commits, oldest first.</summary>
    internal static async Task<IReadOnlyList<string>> MakeAsync(string origin, string checkout, int commits, BenchRuns run)
    {
        Directory.CreateDirectory(Path.Combine(origin, "src"));
        using (var lockfile = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(checkout, "src", "Daoris.Web", "package-lock.json"))))
        {
            var packages = lockfile.RootElement.GetProperty("packages");
            string Version(string name) => packages.GetProperty("node_modules/" + name).GetProperty("version").GetString()!;
            await File.WriteAllTextAsync(Path.Combine(origin, "package.json"),
                $$"""
                { "name": "bench-web", "private": true, "type": "module",
                  "scripts": { "build": "tsc --noEmit && vite build" },
                  "devDependencies": { "typescript": "{{Version("typescript")}}", "vite": "{{Version("vite")}}" } }

                """);
        }

        await File.WriteAllTextAsync(Path.Combine(origin, "tsconfig.json"),
            """{ "compilerOptions": { "target": "ES2022", "module": "ESNext", "moduleResolution": "bundler", "strict": true, "noEmit": true, "skipLibCheck": true }, "include": ["src"] }""" + "\n");
        await File.WriteAllTextAsync(Path.Combine(origin, "vite.config.ts"), "export default { build: { outDir: 'dist' } };\n");
        await File.WriteAllTextAsync(Path.Combine(origin, "index.html"), "<!doctype html><script type=\"module\" src=\"/src/main.ts\"></script>\n");
        await File.WriteAllTextAsync(Path.Combine(origin, ".gitignore"), "node_modules/\ndist/\n");
        var main = new StringBuilder();
        for (var each = 0; each < Modules; each++)
        {
            await WriteModuleAsync(origin, each, 0);
            main.Append($"import {{ value as v{each} }} from './m{each:D3}';\n");
        }

        main.Append("console.log([").Append(string.Join(", ", Enumerable.Range(0, Modules).Select(each => $"v{each}(1)"))).Append("].reduce((a, b) => a + b, 0));\n");
        await File.WriteAllTextAsync(Path.Combine(origin, "src", "main.ts"), main.ToString());
        var (seconds, code) = await run.NpmAsync(origin, "install", "--package-lock-only", "--ignore-scripts", "--no-audit", "--no-fund", "--prefer-offline");
        if (code != 0) throw new InvalidOperationException("npm could not write the generated repository's lockfile");
        run.Say($"the web shape: {Modules} modules generated, its lockfile written by npm in {seconds:F1} s");

        await GitAsync(origin, "init", "--quiet", "-b", "main");
        await GitAsync(origin, "config", "user.email", "bench@example.test");
        await GitAsync(origin, "config", "user.name", "Bench");
        await GitAsync(origin, "add", ".");
        await GitAsync(origin, "commit", "--quiet", "-m", "the web shape's first commit");
        var made = new List<string> { (await GitAsync(origin, "rev-parse", "HEAD")).Trim() };
        var random = new Random(14);
        for (var commit = 1; commit < commits; commit++)
        {
            var changed = random.Next(1, 9);
            for (var each = 0; each < changed; each++) await WriteModuleAsync(origin, random.Next(Modules), commit);
            await GitAsync(origin, "commit", "--quiet", "-am", $"change {changed} modules");
            made.Add((await GitAsync(origin, "rev-parse", "HEAD")).Trim());
        }

        return made;
    }

    /// <summary>One module, importing the one before it, so the type check and the bundle read the whole graph; its revision in its text.</summary>
    private static Task WriteModuleAsync(string origin, int index, int revision)
    {
        int[] imports = index > 0 ? [index - 1] : [];
        var text = new StringBuilder();
        foreach (var each in imports) text.Append($"import {{ value as v{each} }} from './m{each:D3}';\n");
        text.Append($"export const revision = {revision};\n");
        text.Append($"export interface Shape{index} {{ readonly id: number; readonly name: string; readonly tags: readonly string[] }}\n");
        text.Append($"export function value(n: number): number {{\n  const shape: Shape{index} = {{ id: n, name: 'm{index:D3}', tags: ['r' + revision] }};\n");
        text.Append($"  return shape.id * {index + 1} + revision{string.Concat(imports.Select(each => $" + v{each}(0)"))};\n}}\n");
        return File.WriteAllTextAsync(Path.Combine(origin, "src", $"m{index:D3}.ts"), text.ToString());
    }
}
