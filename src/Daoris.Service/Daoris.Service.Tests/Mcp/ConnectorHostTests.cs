using Daoris.Knowledge.Mcp;
using Microsoft.Extensions.Configuration;

namespace Daoris.Service.Tests;

/// <summary>
/// The connector's host watches nothing in the folder it is started from. The generic host's defaults read
/// <c>appsettings.json</c> from the content root, which is the working folder, and watch that root and everything below it
/// for the file to change, walking each folder renamed anywhere beneath. A session starts its connector in its checkout, so
/// every connector would watch the whole checkout, its ignored trees too, and a rehearsal's swap of an install under
/// <c>_fixtures/</c> would be refused while one of them walked the folder it had just renamed.
/// </summary>
public sealed class ConnectorHostTests
{
    [Fact]
    public void The_connector_reads_its_configuration_from_its_own_folder_and_watches_no_file()
    {
        var builder = KnowledgeServer.HostBuilder([]);
        try
        {
            Assert.Equal(
                Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory),
                Path.TrimEndingDirectorySeparator(builder.Environment.ContentRootPath));

            var files = ((IConfigurationRoot)builder.Configuration).Providers.OfType<FileConfigurationProvider>().ToList();
            Assert.NotEmpty(files);
            Assert.All(files, file => Assert.False(file.Source.ReloadOnChange, $"'{file.Source.Path}' is watched for a change"));
        }
        finally
        {
            ((IDisposable)builder.Configuration).Dispose();
        }
    }
}
