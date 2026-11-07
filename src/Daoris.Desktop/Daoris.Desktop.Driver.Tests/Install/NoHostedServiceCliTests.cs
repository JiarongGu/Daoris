using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The hosted services' command lines (<c>gh</c>, <c>az</c>) are no tool Daoris declares, lists or downloads. A pull
/// request is a plugin's REST calls with the identity's token, so no part of the tool list names either program.
/// </summary>
public sealed class NoHostedServiceCliTests
{
    private static readonly string[] HostedServiceClis = ["gh", "az"];

    [Fact]
    public void The_declared_tools_name_neither_hosted_service_cli()
    {
        var ids = Tools.Declared.Select(tool => tool.Id).ToList();
        var answers = Tools.Declared.SelectMany(tool => tool.Answers).ToList();

        Assert.Equal(["git", "node", "pwsh"], ids);
        Assert.Empty(HostedServiceClis.Intersect(answers, StringComparer.Ordinal));
        Assert.All(HostedServiceClis, program => Assert.Null(Tools.Find(program)));
    }

    [Fact]
    public void The_built_in_list_offers_no_version_of_either_hosted_service_cli()
    {
        var list = ToolResources.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, ToolResources.FileName)), ToolResources.BuiltIn);

        Assert.Null(list.Problem);
        Assert.Empty(HostedServiceClis.Where(list.Tools.ContainsKey));
    }
}
