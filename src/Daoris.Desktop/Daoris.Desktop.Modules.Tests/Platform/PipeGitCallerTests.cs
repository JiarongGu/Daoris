namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The pipe's caller as the git scheduler reads it: the verb and its parents, nearest first, the walk ending at this Daoris and at a
/// parent created after its child (another process given the id since). No process: the table is the test's.
/// </summary>
public sealed class PipeGitCallerTests
{
    private static readonly DateTime Born = DateTime.Parse("2026-11-09T10:00:00Z").ToUniversalTime();

    private sealed class Tree : IProcessTable
    {
        public Dictionary<int, ProcessFacts> Processes { get; } = [];

        public int? Client { get; set; }

        public void Add(int id, int parent, DateTime created) => Processes[id] = new ProcessFacts(id, parent, created, null, null, _ => null);

        public int? IdOf(PipeClientProcess? client) => Client;

        public ProcessFacts? Read(int id) => Processes.GetValueOrDefault(id);
    }

    [Fact]
    public void A_hooks_verb_is_read_with_its_shell_and_the_git_that_ran_the_hook_up_to_this_daoris()
    {
        var tree = new Tree { Client = 30 };
        tree.Add(1, 0, Born);              // this Daoris
        tree.Add(10, 1, Born.AddSeconds(1)); // the git placed by the scheduler
        tree.Add(20, 10, Born.AddSeconds(2)); // the hook's shell
        tree.Add(30, 20, Born.AddSeconds(3)); // the verb

        Assert.Equal([30, 20, 10], PipeGitCaller.AncestorsOf(tree, null, own: 1));
    }

    [Fact]
    public void A_parent_created_after_its_child_ends_the_walk_and_an_unread_client_has_none()
    {
        var tree = new Tree { Client = 30 };
        tree.Add(10, 0, Born.AddSeconds(9)); // the id given to another process since
        tree.Add(20, 10, Born.AddSeconds(2));
        tree.Add(30, 20, Born.AddSeconds(3));

        Assert.Equal([30, 20], PipeGitCaller.AncestorsOf(tree, null, own: 1));
        Assert.Empty(PipeGitCaller.AncestorsOf(new Tree(), null, own: 1));
    }
}
