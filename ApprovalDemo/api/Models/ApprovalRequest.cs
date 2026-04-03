using System;

namespace ApprovalDemo.Api.Models
{
    public class ApprovalRequest
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string RequestedBy { get; set; } = string.Empty;
        public byte Status { get; set; } // 0=Pending, 1=Approved, 2=Rejected
        public DateTime CreatedAt { get; set; }
        public string? DecisionBy { get; set; }
        public DateTime? DecisionAt { get; set; }
        public string? RejectReason { get; set; }
    }

    public class CreateRequestDto
    {
        public string Title { get; set; } = string.Empty;
        public string RequestedBy { get; set; } = string.Empty;
    }

    public class DecisionDto
    {
        public string DecisionBy { get; set; } = string.Empty;
        public string? RejectReason { get; set; }
    }
}
