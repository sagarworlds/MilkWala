using MilkWala.Models;
using SQLite;

namespace MilkWala.Services.Data
{
    /// <summary>The allowed life cycle of a customer complaint.</summary>
    public static class ComplaintWorkflow
    {
        /// <summary>
        /// Open can be picked up or closed straight away; in-progress is closed when resolved; a closed complaint
        /// can be reopened (back to in-progress) if the customer says the problem is not fixed.
        /// </summary>
        public static bool CanTransition(ComplaintStatus from, ComplaintStatus to) =>
            (from, to) is (ComplaintStatus.Open, ComplaintStatus.InProgress)
                       or (ComplaintStatus.Open, ComplaintStatus.Closed)
                       or (ComplaintStatus.InProgress, ComplaintStatus.Closed)
                       or (ComplaintStatus.Closed, ComplaintStatus.InProgress);

        /// <summary>Sort key for the vendor's list: what needs attention first.</summary>
        public static int Priority(ComplaintStatus status) => status switch
        {
            ComplaintStatus.Open => 0,
            ComplaintStatus.InProgress => 1,
            _ => 2
        };
    }

    /// <summary>Vendor-side handling of complaints. Run inside a transaction.</summary>
    public static class ComplaintLedger
    {
        /// <summary>
        /// Moves a complaint to a new status and records the vendor's notes, queued for sync.
        /// </summary>
        /// <param name="connection">Connection of the surrounding transaction.</param>
        /// <param name="complaintId">The complaint (re-read inside the transaction).</param>
        /// <param name="newStatus">Target status.</param>
        /// <param name="resolutionNotes">What was done; required when closing so the customer sees an answer.</param>
        /// <exception cref="RecordNotFoundException">The complaint does not exist.</exception>
        /// <exception cref="BusinessRuleException">The transition is not allowed, or a closing note is missing.</exception>
        public static Complaint Update(SQLiteConnection connection, int complaintId, ComplaintStatus newStatus, string? resolutionNotes)
        {
            var complaint = connection.Find<Complaint>(complaintId)
                ?? throw new RecordNotFoundException(nameof(Complaint), complaintId);

            if (!ComplaintWorkflow.CanTransition(complaint.Status, newStatus))
                throw new BusinessRuleException($"A {complaint.Status} complaint cannot be moved to {newStatus}.");

            var notes = (resolutionNotes ?? string.Empty).Trim();
            if (newStatus == ComplaintStatus.Closed && notes.Length == 0)
                throw new BusinessRuleException("Please write what was done to resolve the complaint before closing it.");

            complaint.Status = newStatus;
            if (notes.Length > 0)
                complaint.ResolutionNotes = notes;
            complaint.UpdatedAt = DateTime.UtcNow;
            connection.Update(complaint);
            SyncQueueWriter.Enqueue(connection, complaint, SyncOperation.Update);

            return complaint;
        }
    }
}
