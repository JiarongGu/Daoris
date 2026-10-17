using Daoris.Knowledge.Http;
using Microsoft.Extensions.Configuration;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// The HTTP host watches no settings file (HTTPWATCH1): the web host's defaults watch the content root and everything below
/// it for <c>appsettings.json</c>, and walk each folder renamed there, which held the install's <c>app/</c> against an update's
/// swap and a checkout's folders against their renames.
/// </summary>
public sealed class HostRootTests
{
    [Fact]
    public void The_host_watches_no_file_and_does_not_take_the_working_folder_as_its_root()
    {
        var builder = HostRoot.Builder([]);
        var files = ((IConfigurationRoot)builder.Configuration).Providers.OfType<FileConfigurationProvider>().ToList();
        Assert.NotEmpty(files);
        Assert.All(files, file => Assert.False(file.Source.ReloadOnChange, $"'{file.Source.Path}' is watched for a change"));
        // The test's binaries carry no bundle and its working folder is its own: neither the project's nor a checkout's.
        Assert.Equal(
            Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory),
            Path.TrimEndingDirectorySeparator(builder.Environment.ContentRootPath));
    }

    [Fact]
    public void The_bundle_beside_the_binary_is_served_before_the_working_folders()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "hostroot-" + Guid.NewGuid().ToString("N"));
        var bin = Path.Combine(root, "bin");
        var work = Path.Combine(root, "work");
        try
        {
            Directory.CreateDirectory(Path.Combine(bin, "wwwroot"));
            Directory.CreateDirectory(Path.Combine(work, "wwwroot"));
            Assert.Equal(bin, HostRoot.ContentRoot(work, bin));

            Directory.Delete(Path.Combine(bin, "wwwroot"));
            Assert.Equal(work, HostRoot.ContentRoot(work, bin));

            Directory.Delete(Path.Combine(work, "wwwroot"));
            Assert.Equal(bin, HostRoot.ContentRoot(work, bin));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
