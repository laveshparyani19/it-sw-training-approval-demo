using System.ComponentModel.DataAnnotations;

namespace ApprovalDemo.Api.Models
{
    public sealed class StaffDirectoryItem
    {
        public int Id { get; set; }
        public string StaffCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;
        public string? PhotoUrl { get; set; }
        public bool IsActive { get; set; }
        public bool IsSystemAccount { get; set; }
    }

    public sealed class StaffDirectoryQuery
    {
        public string? Department { get; set; }
        public string? Team { get; set; }
        public string? Departments { get; set; }
        public string? Teams { get; set; }
        public string? Search { get; set; }
        public bool OnlyActive { get; set; } = true;
        public bool ExcludeSystemAccounts { get; set; } = true;

        [Range(1, 1000)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 24;
    }
}
