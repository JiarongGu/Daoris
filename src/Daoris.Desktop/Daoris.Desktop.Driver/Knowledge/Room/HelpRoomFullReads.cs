using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The checkouts full access names, under <i>What you may do</i>: each readable one here by repository, workspace and place, since
/// its shell reads them where restricted mode's tool alone does; unnamed, a question about code meets a blind search of the disk.
/// </summary>
internal static class HelpRoomFullReads
{
    /// <summary>The paragraph, or empty where no checkout is readable or the person turned reading off in every one.</summary>
    public static string Render(HelpMachine machine)
    {
        var reads = machine.Reads.Where(read => !machine.CheckoutsOff.Contains(read.Name, StringComparer.OrdinalIgnoreCase)).ToList();
        if (reads.Count == 0) return "";
        var text = new StringBuilder();
        text.Append("The checkouts here are on this machine. Read the code in them before you answer a question about it, with your\n");
        text.Append("shell or with `checkout_read` (`status` and `branches` among it), and never search the disk for one:\n\n");
        foreach (var read in reads)
        {
            text.Append($"- `{read.Name}` (workspace `{read.Workspace}`): `{read.Path}`\n");
        }

        text.Append('\n');
        return text.ToString();
    }
}
