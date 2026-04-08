using System.ComponentModel.DataAnnotations;

namespace ApprovalDemo.Api.Models
{
    public sealed class StudentDirectoryItem
    {
        public int Id { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string GradeName { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string? PhotoUrl { get; set; }
        public bool IsActive { get; set; }
    }

    public sealed class StudentDirectoryQuery
    {
        public string? Grade { get; set; }
        public string? Section { get; set; }
        public string? Search { get; set; }
        public bool OnlyActive { get; set; } = true;

        [Range(1, 1000)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 24;
    }

    public sealed class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
