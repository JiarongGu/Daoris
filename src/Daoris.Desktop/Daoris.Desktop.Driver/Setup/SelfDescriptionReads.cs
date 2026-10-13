namespace Daoris.Driver;

/// <summary>
/// A repository's description read from its checkout, under the name its callers use (STRUCT3e, as D166's STRUCT3a note
/// cuts a mixed type): <c>SelfDescription.Read(root)</c> for the intake, the set-up press's world and the tests, over
/// <see cref="SelfDescriptionFiles"/>. Another module reaches the reading here, in the Application, never in the
/// Infrastructure; the rules are <see cref="SelfDescription"/>'s, pure.
/// </summary>
public static class SelfDescriptionReads
{
    extension(SelfDescription)
    {
        /// <summary>What <paramref name="root"/> says about itself, or null when it says nothing a reader could use.</summary>
        public static RepositoryDescription? Read(string root) => SelfDescriptionFiles.Read(root);
    }
}
