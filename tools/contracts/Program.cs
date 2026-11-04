using System.Text;
using Daoris.Contracts;
using Daoris.ContractsGen;

// The page's types, written from the routes Daoris.Contracts declares (tools/contracts.mjs runs this):
//   contracts-gen --out <file> --stamp <hash>
// The stamp is the hash tools/contracts.mjs takes of the declaring sources, written as the file's first line, so a check reads
// staleness with no build. Exit codes: 0 written, 2 a usage or declaration error.
string? output = null, stamp = null;
for (var i = 0; i + 1 < args.Length; i += 2)
{
    if (args[i] == "--out") output = args[i + 1];
    else if (args[i] == "--stamp") stamp = args[i + 1];
}

if (output is null || stamp is null)
{
    Console.Error.WriteLine("usage: contracts-gen --out <file> --stamp <hash>");
    return 2;
}

string text;
try
{
    text = PageTypes.Write(RouteDeclaration.In(typeof(IRoute<>).Assembly));
}
catch (InvalidOperationException refused)
{
    Console.Error.WriteLine($"contracts-gen: {refused.Message}");
    return 2;
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
var beside = $"{output}.partial";
File.WriteAllText(beside, $"// contracts: {stamp}\n{text}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
File.Move(beside, output, overwrite: true);
return 0;
