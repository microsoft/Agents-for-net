
namespace SearchCommand.Model
{
    /// <summary>
    /// The strongly typed NuGet package model for Adaptive Card
    /// </summary>
    public class CardPackage
    {
        public string? Id { get; set; }

        public string? Version { get; set; }

        public string? Description { get; set; }

        public string? Tags { get; set; }

        public string? Authors { get; set; }

        public string? Owners { get; set; }

        public string? LicenseUrl { get; set; }

        public string? ProjectUrl { get; set; }

        public string? NuGetUrl { get; set; }

        public static CardPackage Create(Package package)
        {
            return new CardPackage
            {
                Id = package.Id ?? string.Empty,
                Version = package.Version ?? string.Empty,
                Description = package.Description ?? string.Empty,
                Tags = package.Tags == null ? string.Empty : string.Join(", ", package.Tags),
                Authors = package.Authors == null ? string.Empty : string.Join(", ", package.Authors),
                Owners = package.Owners == null ? string.Empty : string.Join(", ", package.Owners),
                LicenseUrl = package.LicenseUrl ?? string.Empty,
                ProjectUrl = package.ProjectUrl ?? string.Empty,
                NuGetUrl = $"https://www.nuget.org/packages/{package.Id}"
            };
        }
    }
}
