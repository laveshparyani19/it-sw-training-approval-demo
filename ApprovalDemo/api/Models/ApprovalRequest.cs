using System;
using System.ComponentModel.DataAnnotations;

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
        [Required(ErrorMessage = "Title is required")]
        [StringLength(500, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 500 characters")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Requested By is required")]
        [StringLength(200, MinimumLength = 2, ErrorMessage = "Requested By must be between 2 and 200 characters")]
        public string RequestedBy { get; set; } = string.Empty;
    }

    public class DecisionDto
    {
        [Required(ErrorMessage = "Decision By is required")]
        [StringLength(200, MinimumLength = 2, ErrorMessage = "Decision By must be between 2 and 200 characters")]
        public string DecisionBy { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Reject Reason must not exceed 1000 characters")]
        public string? RejectReason { get; set; }
    }
}
