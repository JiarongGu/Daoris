using System.Text;
using Daoris.Contracts;
using Daoris.ContractsGen;

// The page's types, written from the routes Daoris.Contracts declares and the window routes' marks (tools/contracts.mjs runs this):
//   contracts-gen --out <service.ts> --routes <routes.ts> --stamp <hash>
// The stamp is the hash tools/contracts.mjs takes of the declaring sources, written as each file's first line, so a check reads
// staleness with no build. Exit codes: 0 written, 2 a usage or declaration error.
string? output = null, routes = null, stamp = null;
for (var i = 0; i + 1 < args.Length; i += 2)
{
    if (args[i] == "--out") output = args[i + 1];
    else if (args[i] == "--routes") routes = args[i + 1];
    else if (args[i] == "--stamp") stamp = args[i + 1];
}

if (output is null || routes is null || stamp is null)
{
    Console.Error.WriteLine("usage: contracts-gen --out <service.ts> --routes <routes.ts> --stamp <hash>");
    return 2;
}

string service, window;
try
{
    service = PageTypes.Write(RouteDeclaration.In(typeof(IRoute<>).Assembly));
    window = PageRoutes.Write() + PageSettings.Write() + PageNodeKinds.Write();
}
catch (InvalidOperationException refused)
{
    Console.Error.WriteLine($"contracts-gen: {refused.Message}");
    return 2;
}

Written(output, service);
Written(routes, window);
return 0;

void Written(string path, string text)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    var beside = $"{path}.partial";
    File.WriteAllText(beside, $"// contracts: {stamp}\n{text}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    File.Move(beside, path, overwrite: true);
}
