namespace Daoris.Driver;

/// <summary>
/// The names of the variables Daoris puts in a child's environment that no start hands on to a person's own terminal (D194 point 5; the
/// supervisor design §2.2): a process start's own, so the start that strips them names them here and not in the subdomain that sets them.
/// </summary>
public static class ChildMarks
{
    /// <summary>The variable the holder's terminal carries its key in (<see cref="HolderKey.Variable"/>, which reads it from here).</summary>
    public const string RoleKeyVariable = "DAORIS_ROLE_KEY";
}
