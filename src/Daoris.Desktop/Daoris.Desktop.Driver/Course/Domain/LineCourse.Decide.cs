namespace Daoris.Driver;

// FOUNDATION1e part 2 (the foundation design §3.1, §3.2, §10 row 1e): the one decision of how a line goes on, read by nothing until rows h
// and i adopt it one path per branch.

public abstract partial record LineCourse
{
    /// <summary>How the line goes on, given what pulled the course and the facts the executor read.</summary>
    public static LineCourse Decide(CourseFacts facts, CourseTrigger trigger) => throw new NotImplementedException();
}
