using MilkWala.Models;
using MilkWala.Services.Data;
using Xunit;

namespace MilkWala.Tests;

public class ComplaintLedgerTests
{
    private static Complaint Add(TestDb db, ComplaintStatus status = ComplaintStatus.Open)
    {
        var complaint = new Complaint { CustomerId = 1, CustomerName = "Rahul", Subject = "Short pouch", Status = status };
        db.Connection.Insert(complaint);
        return complaint;
    }

    private static Complaint Update(TestDb db, Complaint complaint, ComplaintStatus status, string? notes)
    {
        Complaint result = null!;
        db.InTransaction(c => result = ComplaintLedger.Update(c, complaint.Id, status, notes));
        return result;
    }

    [Theory]
    [InlineData(ComplaintStatus.Open, ComplaintStatus.InProgress, true)]
    [InlineData(ComplaintStatus.Open, ComplaintStatus.Closed, true)]
    [InlineData(ComplaintStatus.InProgress, ComplaintStatus.Closed, true)]
    [InlineData(ComplaintStatus.Closed, ComplaintStatus.InProgress, true)]  // reopen
    [InlineData(ComplaintStatus.InProgress, ComplaintStatus.Open, false)]
    [InlineData(ComplaintStatus.Closed, ComplaintStatus.Open, false)]
    [InlineData(ComplaintStatus.Open, ComplaintStatus.Open, false)]
    [InlineData(ComplaintStatus.Closed, ComplaintStatus.Closed, false)]
    public void Workflow_allows_only_the_intended_transitions(ComplaintStatus from, ComplaintStatus to, bool allowed)
    {
        Assert.Equal(allowed, ComplaintWorkflow.CanTransition(from, to));
    }

    [Fact]
    public void Priority_puts_open_before_in_progress_before_closed()
    {
        var order = new[] { ComplaintStatus.Closed, ComplaintStatus.Open, ComplaintStatus.InProgress }
            .OrderBy(ComplaintWorkflow.Priority).ToArray();

        Assert.Equal(new[] { ComplaintStatus.Open, ComplaintStatus.InProgress, ComplaintStatus.Closed }, order);
    }

    [Fact]
    public void Closing_stores_the_trimmed_notes_and_queues_the_change()
    {
        using var db = new TestDb();
        var complaint = Add(db);

        var updated = Update(db, complaint, ComplaintStatus.Closed, "  Sent an extra 0.5 L packet.  ");

        Assert.Equal(ComplaintStatus.Closed, updated.Status);
        var stored = db.Connection.Get<Complaint>(complaint.Id);
        Assert.Equal("Sent an extra 0.5 L packet.", stored.ResolutionNotes);
        var queued = db.Connection.Table<SyncQueueItem>().Single();
        Assert.Equal("Complaints", queued.TableName);
        Assert.Equal(SyncOperation.Update, queued.Operation);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Closing_without_notes_is_refused_and_changes_nothing(string? notes)
    {
        using var db = new TestDb();
        var complaint = Add(db);

        var ex = Assert.Throws<BusinessRuleException>(
            () => db.InTransaction(c => ComplaintLedger.Update(c, complaint.Id, ComplaintStatus.Closed, notes)));

        Assert.Contains("resolve", ex.Message);
        Assert.Equal(ComplaintStatus.Open, db.Connection.Get<Complaint>(complaint.Id).Status);
        Assert.Equal(0, db.Count<SyncQueueItem>());
    }

    [Fact]
    public void Starting_work_does_not_need_notes_and_keeps_existing_ones()
    {
        using var db = new TestDb();
        var complaint = Add(db);

        Update(db, complaint, ComplaintStatus.InProgress, null);

        var stored = db.Connection.Get<Complaint>(complaint.Id);
        Assert.Equal(ComplaintStatus.InProgress, stored.Status);
        Assert.Equal(string.Empty, stored.ResolutionNotes);
    }

    [Fact]
    public void Reopening_keeps_the_previous_resolution_notes_when_none_are_given()
    {
        using var db = new TestDb();
        var complaint = Add(db);
        Update(db, complaint, ComplaintStatus.Closed, "Replaced the pouch.");

        Update(db, complaint, ComplaintStatus.InProgress, null);

        var stored = db.Connection.Get<Complaint>(complaint.Id);
        Assert.Equal(ComplaintStatus.InProgress, stored.Status);
        Assert.Equal("Replaced the pouch.", stored.ResolutionNotes);
    }

    [Fact]
    public void A_disallowed_transition_is_refused()
    {
        using var db = new TestDb();
        var complaint = Add(db, ComplaintStatus.Closed);

        var ex = Assert.Throws<BusinessRuleException>(
            () => db.InTransaction(c => ComplaintLedger.Update(c, complaint.Id, ComplaintStatus.Open, "x")));

        Assert.Contains("Closed", ex.Message);
    }

    [Fact]
    public void Unknown_complaint_is_reported()
    {
        using var db = new TestDb();
        Assert.Throws<RecordNotFoundException>(() => ComplaintLedger.Update(db.Connection, 999, ComplaintStatus.InProgress, null));
    }
}
