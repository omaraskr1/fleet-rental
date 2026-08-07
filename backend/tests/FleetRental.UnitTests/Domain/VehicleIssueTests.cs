using FleetRental.Domain.Common;
using FleetRental.Domain.Entities;
using FleetRental.Domain.Enums;

namespace FleetRental.UnitTests.Domain;

public class VehicleIssueTests
{
    private static readonly Guid CarId = Guid.CreateVersion7();
    private static readonly Guid ReporterId = Guid.CreateVersion7();

    private static VehicleIssue OpenIssue(IssueSeverity severity = IssueSeverity.Medium) =>
        VehicleIssue.Report(CarId, ReporterId, "AC not cooling", severity);

    [Fact]
    public void Report_starts_Open_with_no_resolution()
    {
        var issue = OpenIssue();

        Assert.Equal(IssueStatus.Open, issue.Status);
        Assert.Null(issue.ResolvedAt);
        Assert.Null(issue.ResolutionNotes);
        Assert.NotEqual(default, issue.ReportedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Description_is_required(string description)
    {
        Assert.Throws<DomainException>(() => VehicleIssue.Report(CarId, ReporterId, description, IssueSeverity.Low));
    }

    [Theory]
    [InlineData(IssueSeverity.Critical, IssueStatus.Open, true)]
    [InlineData(IssueSeverity.High, IssueStatus.Open, false)]
    [InlineData(IssueSeverity.Critical, IssueStatus.InProgress, true)]
    public void BlocksBooking_is_true_only_for_an_unresolved_critical_issue(
        IssueSeverity severity, IssueStatus status, bool expected)
    {
        var issue = OpenIssue(severity);

        if (status == IssueStatus.InProgress)
        {
            issue.StartProgress();
        }

        Assert.Equal(expected, issue.BlocksBooking);
    }

    [Fact]
    public void A_resolved_critical_issue_no_longer_blocks_booking()
    {
        var issue = OpenIssue(IssueSeverity.Critical);
        issue.Resolve("Replaced compressor");

        Assert.False(issue.BlocksBooking);
    }

    [Fact]
    public void StartProgress_moves_Open_to_InProgress()
    {
        var issue = OpenIssue();
        issue.StartProgress();
        Assert.Equal(IssueStatus.InProgress, issue.Status);
    }

    [Fact]
    public void StartProgress_twice_is_refused()
    {
        var issue = OpenIssue();
        issue.StartProgress();

        Assert.Throws<DomainException>(issue.StartProgress);
    }

    [Fact]
    public void Resolve_records_notes_and_a_timestamp()
    {
        var issue = OpenIssue();

        issue.Resolve("  Replaced the compressor  ");

        Assert.Equal(IssueStatus.Resolved, issue.Status);
        Assert.Equal("Replaced the compressor", issue.ResolutionNotes);
        Assert.NotNull(issue.ResolvedAt);
    }

    [Fact]
    public void Resolve_works_directly_from_Open_without_requiring_InProgress_first()
    {
        var issue = OpenIssue();
        issue.Resolve();
        Assert.Equal(IssueStatus.Resolved, issue.Status);
    }

    [Fact]
    public void Resolving_twice_is_refused()
    {
        var issue = OpenIssue();
        issue.Resolve();

        Assert.Throws<DomainException>(() => issue.Resolve());
    }

    [Fact]
    public void Reopen_clears_the_resolution_and_returns_to_Open()
    {
        var issue = OpenIssue();
        issue.Resolve("Fixed");

        issue.Reopen();

        Assert.Equal(IssueStatus.Open, issue.Status);
        Assert.Null(issue.ResolvedAt);
        Assert.Null(issue.ResolutionNotes);
    }

    [Fact]
    public void Reopening_an_issue_that_was_never_resolved_is_refused()
    {
        var issue = OpenIssue();
        Assert.Throws<DomainException>(issue.Reopen);
    }

    [Fact]
    public void ChangeSeverity_updates_the_severity_regardless_of_status()
    {
        var issue = OpenIssue(IssueSeverity.Low);
        issue.ChangeSeverity(IssueSeverity.Critical);
        Assert.Equal(IssueSeverity.Critical, issue.Severity);
    }
}
