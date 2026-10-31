using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>GITTXN1: the builder's text and its refusals, before git runs.</summary>
public sealed class RefTransactionTests
{
    private const string A = "1111111111111111111111111111111111111111";
    private const string B = "2222222222222222222222222222222222222222";

    [Fact]
    public void Each_act_is_one_line_with_its_expectation_and_a_dependency_is_verified_first()
    {
        var (input, refusal) = new RefTransaction()
            .Create("refs/heads/made", A).Update("refs/heads/moved", A, B).Delete("refs/heads/gone", B).Verify("refs/heads/main", A).Build();
        Assert.Null(refusal);
        Assert.Equal(
            $"verify refs/heads/main {A}\ncreate refs/heads/made {A}\nupdate refs/heads/moved {B} {A}\ndelete refs/heads/gone {B}\n", input);
    }

    [Fact]
    public void Identical_expectations_are_named_once_and_an_act_own_expectation_is_not_verified_beside_it()
    {
        var (input, refusal) = new RefTransaction()
            .Verify("refs/heads/main", A).Verify("refs/heads/main", A).Verify("refs/heads/x", A).Delete("refs/heads/x", A).Delete("refs/heads/x", A).Build();
        Assert.Null(refusal);
        Assert.Equal($"verify refs/heads/main {A}\ndelete refs/heads/x {A}\n", input);
    }

    [Fact]
    public void A_self_referencing_rest_on_the_branch_at_another_commit_is_refused()
    {
        var (input, refusal) = new RefTransaction().Verify("refs/heads/x", B).Delete("refs/heads/x", A).Build();
        Assert.Null(input);
        Assert.Contains("refs/heads/x", refusal);
    }

    [Fact]
    public void Two_values_for_one_ref_are_a_contradiction()
    {
        Assert.NotNull(new RefTransaction().Verify("refs/heads/m", A).Verify("refs/heads/m", B).Build().Refusal);
        Assert.NotNull(new RefTransaction().Delete("refs/heads/m", A).Update("refs/heads/m", A, B).Build().Refusal);
        Assert.NotNull(new RefTransaction().Create("refs/heads/m", A).Verify("refs/heads/m", A).Build().Refusal);
    }

    [Fact]
    public void Two_names_for_one_ref_and_a_name_that_is_no_full_ref_are_refused()
    {
        Assert.Contains("two names", new RefTransaction().Delete("refs/heads/Main", A).Verify("refs/heads/main", A).Build().Refusal);
        Assert.NotNull(new RefTransaction().Delete("HEAD", A).Build().Refusal);
        Assert.NotNull(new RefTransaction().Delete("refs/heads/a..b", A).Build().Refusal);
        Assert.NotNull(new RefTransaction().Delete("refs/heads/a", "abc").Build().Refusal);
    }

    [Fact]
    public void A_rename_checks_both_names_and_its_reversal_is_the_same_two_lines_the_other_way()
    {
        Assert.Equal($"delete refs/heads/a {A}\ncreate refs/heads/b {A}\n", RefTransaction.Rename("refs/heads/a", "refs/heads/b", A).Build().Input);
        Assert.Equal($"delete refs/heads/b {A}\ncreate refs/heads/a {A}\n", RefTransaction.ReverseRename("refs/heads/a", "refs/heads/b", A).Build().Input);
    }
}
