namespace Daoris.Driver;

/// <summary>
/// The intake's room written to the disk (STRUCT3f, as D166's STRUCT3a note cuts a mixed type): <c>IntakeRoom.Prepare(...)</c>
/// for the loop, under the name it has always had. What the room says, where it is and what it allows are
/// <see cref="IntakeRoom"/>'s, pure; making the folder, reading each silent repository's own files and writing the three
/// files are this layer's.
/// </summary>
public static class IntakeRoomFiles
{
    extension(IntakeRoom)
    {
        /// <summary>Seed the room with the circle's declarations as they stand now, and answer where it is.</summary>
        /// <param name="kinds">The kinds its workspace declares, per repository (WORKFLOW1i-b); null or empty where it declares none.</param>
        public static string Prepare(
            string home, string workspace, IReadOnlyList<DeclarationView> declarations, IReadOnlyList<IntakeRepositoryKinds>? kinds = null)
        {
            var room = IntakeRoom.PathOf(home, workspace);
            Directory.CreateDirectory(Path.Combine(room, ".claude"));

            // What each repository that declared nothing says about itself (D77), read from its own files
            // at every open — a README changes as a declaration does — and never written into.
            var described = new Dictionary<string, RepositoryDescription>(StringComparer.OrdinalIgnoreCase);
            foreach (var repository in declarations.Where(d => !(d.Adopted && d.Registered) && d.Root is { Length: > 0 }))
            {
                if (SelfDescription.Read(repository.Root!) is { } description) described[repository.Repository] = description;
            }

            Write(Path.Combine(room, "AGENTS.md"), IntakeRoom.Render(workspace, declarations, described, kinds));
            // Two harnesses read AGENTS.md; Claude Code reads CLAUDE.md — the canon's own shape (D59).
            Write(Path.Combine(room, "CLAUDE.md"), "@AGENTS.md\n");
            Write(Path.Combine(room, ".claude", "settings.json"), IntakeRoom.Settings());
            return room;
        }
    }

    /// <summary>Beside, then renamed — a harness starting in the room never reads half a file.</summary>
    private static void Write(string path, string content)
    {
        AtomicFile.WriteText(path, content);
    }
}
