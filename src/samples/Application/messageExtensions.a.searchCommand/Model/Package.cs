
namespace SearchCommand.Model
{
    /// <summary>
    /// The strongly typed NuGet package search result
    /// </summary>
    public class Package
    {
        public string? Id { get; set; }

        public string? Version { get; set; }

        public string? Description { get; set; }

        public string[]? Tags { get; set; }

        public string[]? Authors { get; set; }

        public string[]? Owners { get; set; }

        public string? IconUrl { get; set; }

        public string? LicenseUrl { get; set; }

        public string? ProjectUrl { get; set; }

        public object[]? PackageTypes { get; set; }
    }
}
