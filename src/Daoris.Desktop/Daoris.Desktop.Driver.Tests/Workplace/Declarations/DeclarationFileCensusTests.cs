using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The held-file half of the run-time censuses (the design's §23, §62): an open of a held declaration's file outside the store is red
/// even through a helper that swallows its error, and a data reader's open is a row's; red on a row left out and on a row gone.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class DeclarationFileCensusTests
{
    [Fact]
    public void Each_data_reader_row_still_declares_its_member_and_a_row_gone_is_said()
    {
        Assert.Empty(DeclarationFilesHeld.Gone(DeclarationFilesHeld.Readers.Keys));
        Assert.Equal(["Daoris.Desktop.Modules/Loop/LoopGrounds.cs LoopGrounds.Gone"], DeclarationFilesHeld.Gone(["Daoris.Desktop.Modules/Loop/LoopGrounds.cs LoopGrounds.Gone"]));
    }

    [Fact]
    public void An_open_of_a_held_declaration_through_a_helper_that_swallows_its_error_is_red_and_a_data_readers_row_is_not()
    {
        var folder = Path.Combine(TestHomes.Root, $"held-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            var file = Path.Combine(folder, DeclarationNames.Gates);
            File.WriteAllText(file, "{}");

            using (var held = DeclarationFilesHeld.Hold([file], DeclarationFilesHeld.Readers.Keys))
            {
                Assert.Null(Planted.Swallowed(file));
                Assert.Equal([$"Planted.Swallowed opened the held {DeclarationNames.Gates}"], held.Problems);
            }

            using (var held = DeclarationFilesHeld.Hold([file], [.. DeclarationFilesHeld.Readers.Keys, "Daoris.Desktop.Driver.Tests/Planted.cs Planted.Swallowed"]))
            {
                Assert.Null(Planted.Swallowed(file));
                Assert.Empty(held.Problems);
            }
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static class Planted
    {
        internal static string? Swallowed(string path)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (IOException)
            {
                return null;
            }
        }
    }
}
