using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ManagementService.Contracts.Landing;
using Xunit;

namespace ManagementService.Tests.Integration;

public sealed class LandingControllerTests : IClassFixture<ManagementServiceTestServer>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;

    public LandingControllerTests(ManagementServiceTestServer server)
    {
        _client = server.Client;
    }

    [Fact]
    public async Task AdminGet_ShouldRequireAuthentication()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/management/admin/landing/en");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminGet_ShouldReturnFallbackDocument_WhenDatabaseEntryDoesNotExist()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Guid.NewGuid().ToString());

        var response = await _client.GetAsync("/api/management/admin/landing/en");

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Equal("en", document.RootElement.GetProperty("locale").GetString());
        Assert.Equal("fallback", document.RootElement.GetProperty("source").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            document.RootElement.GetProperty("content").GetProperty("home").GetProperty("title").GetString()));
    }

    [Fact]
    public async Task AdminPut_ShouldPersistDocument_AndPublicGetShouldReturnSavedContent()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Guid.NewGuid().ToString());

        var payload = new LandingContentDocument
        {
            Navbar = new LandingNavbarContent
            {
                AboutMenu = "About",
                AboutTeam = new LandingLinkItem { Label = "Team", Url = "/about#team" },
                DevelopmentStages = new LandingLinkItem { Label = "Stages", Url = "/about#timeline" },
                Contact = new LandingLinkItem { Label = "Contact", Url = "/contact" },
                Features = new LandingLinkItem { Label = "Features", Url = "#features" },
                Benefits = new LandingLinkItem { Label = "Benefits", Url = "#benefits" },
                Progress = new LandingLinkItem { Label = "Progress", Url = "#progress" },
                Theme = "Theme",
                Login = new LandingLinkItem { Label = "Login", Url = "/auth?mode=login" },
                Register = new LandingLinkItem { Label = "Register", Url = "/auth?mode=register" }
            },
            Footer = new LandingFooterContent
            {
                Description = "Saved from integration test",
                Project = "Project",
                Pages = "Pages",
                Documents = "Documents",
                Follow = "Follow",
                FollowDescription = "Follow description",
                Copyright = "Copyright",
                ProjectLinks = [new LandingLinkItem { Label = "Features", Url = "#features" }],
                PageLinks = [new LandingLinkItem { Label = "About", Url = "/about" }],
                DocumentLinks = [new LandingLinkItem { Label = "Privacy", Url = "/privacy" }],
                FollowLinks = [new LandingLinkItem { Label = "GitHub", Url = "https://github.com/MarkZamore/Collaby" }],
                LegalLinks = [new LandingLinkItem { Label = "Cookies", Url = "/cookies" }]
            },
            Home = new LandingHomeContent
            {
                Badge = "Badge",
                Title = "Custom title",
                Description = "Description",
                Stats = new LandingHomeStats { Team = "Team", Modules = "Modules", Months = "Months" },
                PrimaryCta = new LandingLinkItem { Label = "Open", Url = "/auth?mode=register" },
                SecondaryCta = new LandingLinkItem { Label = "About", Url = "/about" },
                FeaturesTitle = "Features",
                FeaturesDescription = "Features description",
                BenefitsTitle = "Benefits",
                ScreenshotsTitle = "Screenshots",
                ScreenshotsDescription = "Screenshots description",
                FaqTitle = "FAQ",
                FaqDescription = "FAQ description",
                RoadmapTitle = "Roadmap",
                RoadmapDescription = "Roadmap description"
            },
            About = new LandingAboutContent
            {
                Badge = "About badge",
                Title = "About title",
                Description = "About description",
                Stats = new LandingAboutStats { Team = "Team", Scenarios = "Scenarios", Months = "Months", Goal = "Goal" },
                MissionTitle = "Mission",
                MissionDescription = "Mission description",
                VisionTitle = "Vision",
                VisionDescription = "Vision description",
                ValuesTitle = "Values",
                ValuesDescription = "Values description",
                StoryTitle = "Story",
                Story = ["Paragraph"],
                Quote = "Quote",
                QuoteAuthor = "Author",
                TimelineTitle = "Timeline",
                TimelineDescription = "Timeline description",
                TeamTitle = "Team title",
                TeamDescription = "Team description",
                CtaTitle = "CTA",
                CtaDescription = "CTA description",
                PrimaryCta = new LandingLinkItem { Label = "Open", Url = "/auth?mode=login" },
                SecondaryCta = new LandingLinkItem { Label = "Contact", Url = "/contact" }
            },
            Contact = new LandingContactContent
            {
                Badge = "Contact badge",
                Title = "Contact title",
                Description = "Contact description",
                FormTitle = "Form title",
                FormDescription = "Form description",
                Form = new LandingContactFormContent
                {
                    NamePlaceholder = "Name",
                    EmailPlaceholder = "Email",
                    SubjectPlaceholder = "Subject",
                    MessagePlaceholder = "Message"
                },
                RealLinksTitle = "Real links",
                RealLinksDescription = "Real links description",
                ImportantTitle = "Important",
                DemoStatus = "Saved demo status"
            }
        };

        var putResponse = await _client.PutAsync(
            "/api/management/admin/landing/en",
            new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json"));

        putResponse.EnsureSuccessStatusCode();

        var publicResponse = await _client.GetAsync("/api/management/landing/en");
        publicResponse.EnsureSuccessStatusCode();
        var savedPayload = await publicResponse.Content.ReadFromJsonAsync<LandingContentDocument>(JsonOptions);

        Assert.NotNull(savedPayload);
        Assert.Equal("Custom title", savedPayload!.Home.Title);
        Assert.Equal("Saved demo status", savedPayload.Contact.DemoStatus);
        Assert.Equal("/auth?mode=register", savedPayload.Home.PrimaryCta.Url);
    }

    [Fact]
    public async Task UnsupportedLocale_ShouldReturnBadRequest()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Guid.NewGuid().ToString());

        var adminResponse = await _client.GetAsync("/api/management/admin/landing/de");
        var publicResponse = await _client.GetAsync("/api/management/landing/de");

        Assert.Equal(HttpStatusCode.BadRequest, adminResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, publicResponse.StatusCode);
    }
}
