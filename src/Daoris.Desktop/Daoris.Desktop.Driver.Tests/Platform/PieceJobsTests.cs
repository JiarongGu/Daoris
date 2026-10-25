using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COAGENT1b (D199 point 2; the co-agent design §2.3 steps 5 and 6, §7.2): a piece's strict job is made empty and named, never shared
/// with another holder of the name, and a look reads it as empty, running or unknown, a job no name answers to being unknown. No process
/// is started here: a job is a kernel object, and the partner's process in it is the <c>Process</c> half's.
/// </summary>
public sealed class PieceJobsTests
{
    [Fact]
    public void A_pieces_job_is_made_empty_under_its_name_and_a_second_with_the_name_is_refused()
    {
        if (!OperatingSystem.IsWindows()) return;
        var piece = Guid.NewGuid().ToString("N")[..12];

        using var job = PieceJobs.Create(piece);
        Assert.NotNull(job);
        Assert.Equal(0, job.Active);
        Assert.Equal(PartnerJob.Empty, PieceJobs.Observe(PieceJobs.NameOf(piece), job));
        Assert.Equal(PartnerJob.Empty, PieceJobs.Observe(PieceJobs.NameOf(piece)));

        // Not wholly this one's, so it holds nothing: the piece is refused rather than run beside another's job.
        Assert.Null(PieceJobs.Create(piece));
    }

    [Fact]
    public void A_job_no_name_answers_to_is_unknown_never_empty()
    {
        var piece = Guid.NewGuid().ToString("N")[..12];
        Assert.Equal(PartnerJob.Unknown, PieceJobs.Observe(PieceJobs.NameOf(piece)));
        Assert.Equal(PartnerJob.Unknown, PieceJobs.Observe(null));

        if (!OperatingSystem.IsWindows()) return;
        var job = PieceJobs.Create(piece)!;
        job.Dispose();
        // Its last handle closed: what it held ended with it, but a restart cannot tell that from a job never made.
        Assert.Equal(PartnerJob.Unknown, PieceJobs.Observe(PieceJobs.NameOf(piece)));
    }

    [Fact]
    public void A_restart_reading_an_unknown_job_leaves_the_piece_uncertain_and_an_empty_one_ending()
    {
        var now = new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
        var started = new PartnerState("p1", PartnerStates.Started, null, now) { Job = PieceJobs.NameOf("p1"), Copy = "p-p1" };

        var unknown = PartnerReconcile.Next("p1", started, new(true, true, "s1", PieceJobs.Observe(started.Job), null), now);
        Assert.Equal((PartnerStates.Uncertain, PartnerWhy.JobUnknown), (unknown.Write!.State, unknown.Write.Code));
        Assert.Equal(PartnerAct.Keep, PartnerReconcile.Next("p1", started, new(true, true, "s1", PartnerJob.Empty, null), now).Act);
    }
}
