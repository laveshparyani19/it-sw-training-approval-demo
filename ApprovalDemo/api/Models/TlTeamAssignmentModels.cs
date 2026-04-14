using System;
using System.ComponentModel.DataAnnotations;

namespace ApprovalDemo.Api.Models
{
    public sealed class TeamOptionItem
    {
        public string DepartmentName { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;
        public string DisplayLabel { get; set; } = string.Empty;
    }

    public sealed class CreateTlTeamAssignmentDto
    {
        [Required]
        [StringLength(50, MinimumLength = 2)]
        public string TlStaffCode { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string DepartmentName { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string TeamName { get; set; } = string.Empty;

        [Required]
        [MinLength(1, ErrorMessage = "Select at least one team member.")]
        public int[] MemberStaffIds { get; set; } = Array.Empty<int>();

        [StringLength(4000)]
        public string? TaskDescription { get; set; }
    }

    public sealed class TlTeamAssignmentItem
    {
        public Guid Id { get; set; }
        public string TlStaffCode { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;
        public int[] MemberStaffIds { get; set; } = Array.Empty<int>();
        public string? TaskDescription { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
