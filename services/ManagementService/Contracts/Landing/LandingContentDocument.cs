namespace ManagementService.Contracts.Landing;

public static class LandingLocales
{
    public static readonly string[] Supported = ["en", "ru", "kk"];

    public static bool IsSupported(string? locale) =>
        !string.IsNullOrWhiteSpace(locale) &&
        Supported.Contains(locale.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    public static string Normalize(string locale) => locale.Trim().ToLowerInvariant();
}

public sealed class LandingContentDocument
{
    public LandingNavbarContent Navbar { get; set; } = new();
    public LandingFooterContent Footer { get; set; } = new();
    public LandingHomeContent Home { get; set; } = new();
    public LandingAboutContent About { get; set; } = new();
    public LandingContactContent Contact { get; set; } = new();
}

public sealed class LandingNavbarContent
{
    public string AboutMenu { get; set; } = string.Empty;
    public LandingLinkItem AboutTeam { get; set; } = new();
    public LandingLinkItem DevelopmentStages { get; set; } = new();
    public LandingLinkItem Contact { get; set; } = new();
    public LandingLinkItem Features { get; set; } = new();
    public LandingLinkItem Benefits { get; set; } = new();
    public LandingLinkItem Progress { get; set; } = new();
    public string Theme { get; set; } = string.Empty;
    public LandingLinkItem Login { get; set; } = new();
    public LandingLinkItem Register { get; set; } = new();
}

public sealed class LandingFooterContent
{
    public string Description { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public List<LandingLinkItem> ProjectLinks { get; set; } = [];
    public string Pages { get; set; } = string.Empty;
    public List<LandingLinkItem> PageLinks { get; set; } = [];
    public string Documents { get; set; } = string.Empty;
    public List<LandingLinkItem> DocumentLinks { get; set; } = [];
    public string Follow { get; set; } = string.Empty;
    public string FollowDescription { get; set; } = string.Empty;
    public List<LandingLinkItem> FollowLinks { get; set; } = [];
    public string Copyright { get; set; } = string.Empty;
    public List<LandingLinkItem> LegalLinks { get; set; } = [];
}

public sealed class LandingHomeContent
{
    public string Badge { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public LandingHomeStats Stats { get; set; } = new();
    public LandingLinkItem PrimaryCta { get; set; } = new();
    public LandingLinkItem SecondaryCta { get; set; } = new();
    public LandingImageAsset HeroMedia { get; set; } = new();
    public List<LandingScreenshotItem> Screenshots { get; set; } = [];
    public string FeaturesTitle { get; set; } = string.Empty;
    public string FeaturesDescription { get; set; } = string.Empty;
    public List<LandingFeatureItem> Features { get; set; } = [];
    public string BenefitsTitle { get; set; } = string.Empty;
    public List<LandingIconTextItem> Benefits { get; set; } = [];
    public LandingImageAsset BenefitsMedia { get; set; } = new();
    public string ScreenshotsTitle { get; set; } = string.Empty;
    public string ScreenshotsDescription { get; set; } = string.Empty;
    public string FaqTitle { get; set; } = string.Empty;
    public string FaqDescription { get; set; } = string.Empty;
    public List<LandingFaqItem> Faq { get; set; } = [];
    public string RoadmapTitle { get; set; } = string.Empty;
    public string RoadmapDescription { get; set; } = string.Empty;
    public List<LandingTimelineItem> Roadmap { get; set; } = [];
}

public sealed class LandingHomeStats
{
    public string Team { get; set; } = string.Empty;
    public string Modules { get; set; } = string.Empty;
    public string Months { get; set; } = string.Empty;
}

public sealed class LandingAboutContent
{
    public string Badge { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public LandingAboutStats Stats { get; set; } = new();
    public string MissionTitle { get; set; } = string.Empty;
    public string MissionDescription { get; set; } = string.Empty;
    public string VisionTitle { get; set; } = string.Empty;
    public string VisionDescription { get; set; } = string.Empty;
    public string ValuesTitle { get; set; } = string.Empty;
    public string ValuesDescription { get; set; } = string.Empty;
    public List<LandingIconTextItem> Values { get; set; } = [];
    public string StoryTitle { get; set; } = string.Empty;
    public List<string> Story { get; set; } = [];
    public string Quote { get; set; } = string.Empty;
    public string QuoteAuthor { get; set; } = string.Empty;
    public LandingImageAsset StoryMedia { get; set; } = new();
    public string TimelineTitle { get; set; } = string.Empty;
    public string TimelineDescription { get; set; } = string.Empty;
    public List<LandingTimelineItem> Timeline { get; set; } = [];
    public string TeamTitle { get; set; } = string.Empty;
    public string TeamDescription { get; set; } = string.Empty;
    public List<LandingTeamMember> Team { get; set; } = [];
    public string CtaTitle { get; set; } = string.Empty;
    public string CtaDescription { get; set; } = string.Empty;
    public LandingLinkItem PrimaryCta { get; set; } = new();
    public LandingLinkItem SecondaryCta { get; set; } = new();
}

public sealed class LandingAboutStats
{
    public string Team { get; set; } = string.Empty;
    public string Scenarios { get; set; } = string.Empty;
    public string Months { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
}

public sealed class LandingContactContent
{
    public string Badge { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<LandingContactInfoItem> Info { get; set; } = [];
    public string FormTitle { get; set; } = string.Empty;
    public string FormDescription { get; set; } = string.Empty;
    public LandingContactFormContent Form { get; set; } = new();
    public string RealLinksTitle { get; set; } = string.Empty;
    public string RealLinksDescription { get; set; } = string.Empty;
    public List<LandingSocialLink> SocialLinks { get; set; } = [];
    public string ImportantTitle { get; set; } = string.Empty;
    public List<LandingFaqItem> Faq { get; set; } = [];
    public string DemoStatus { get; set; } = string.Empty;
}

public sealed class LandingContactFormContent
{
    public string NamePlaceholder { get; set; } = string.Empty;
    public string EmailPlaceholder { get; set; } = string.Empty;
    public string SubjectPlaceholder { get; set; } = string.Empty;
    public string MessagePlaceholder { get; set; } = string.Empty;
}

public sealed class LandingLinkItem
{
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public sealed class LandingImageAsset
{
    public string ImageUrl { get; set; } = string.Empty;
    public string ImageAlt { get; set; } = string.Empty;
    public string Caption { get; set; } = string.Empty;
}

public sealed class LandingFeatureItem
{
    public string Icon { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class LandingIconTextItem
{
    public string Icon { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class LandingScreenshotItem
{
    public string Type { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string ImageAlt { get; set; } = string.Empty;
    public string Caption { get; set; } = string.Empty;
}

public sealed class LandingFaqItem
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}

public sealed class LandingTimelineItem
{
    public string Date { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class LandingTeamMember
{
    public string Name { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string ProfileUrl { get; set; } = string.Empty;
}

public sealed class LandingContactInfoItem
{
    public string Icon { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Info { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public sealed class LandingSocialLink
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
