using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using GamificationService.Configuration;
using Xunit;

namespace ManagementService.Tests;

public sealed class SwaggerGenerationTests
{
    [Theory]
    [InlineData("AccessHost", "IdentityService", "auth", "api/auth")]
    [InlineData("AccessHost", "UserService", "users", "api/users")]
    [InlineData("WorkspaceHost", "ManagementService", "management", "api/management")]
    [InlineData("WorkspaceHost", "AnalyticsService", "analytics", "api/analytics")]
    [InlineData("RealtimeHost", "MessengerService", "messenger", "api/messenger")]
    [InlineData("RealtimeHost", "CallService", "calls", "api/calls")]
    [InlineData("", "GamificationService", "v1", "api/gamification")]
    public async Task ActualControllersProduceValidSwaggerJson(
        string hostName, string moduleName, string documentName, string routePrefix)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var module = Assembly.Load(moduleName);
        var mvc = builder.Services.AddControllers(options =>
        {
            // Используется настоящий convention соответствующего combined host.
            if (hostName.Length == 0) return;
            var host = Assembly.Load(hostName);
            var descriptorType = host.GetType(hostName + ".HostModuleDescriptor", throwOnError: true)!;
            var descriptor = Activator.CreateInstance(descriptorType, module, routePrefix, documentName)!;
            var descriptors = Array.CreateInstance(descriptorType, 1);
            descriptors.SetValue(descriptor, 0);
            var conventionType = host.GetType(hostName + ".HostModuleConvention", throwOnError: true)!;
            var convention = Activator.CreateInstance(conventionType, descriptors)!;
            options.Conventions.Add((IApplicationModelConvention)convention);
        });
        // Нужна только выбранная реальная сборка контроллеров, без startup БД/Kafka.
        mvc.PartManager.ApplicationParts.Clear();
        mvc.AddApplicationPart(module);
        if (hostName.Length == 0) builder.Services.AddGamificationSwagger();
        else builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(documentName, new OpenApiInfo { Title = moduleName, Version = "v1" });
            options.DocInclusionPredicate((name, description) => description.GroupName == name);
        });
        await using var app = builder.Build();
        if (hostName.Length == 0) app.UsePathBase("/" + routePrefix);
        app.UseSwagger();
        app.MapControllers();
        await app.StartAsync();
        var swaggerPath = (hostName.Length == 0 ? "/" + routePrefix : "")
            + $"/swagger/{documentName}/swagger.json";
        var response = await app.GetTestClient().GetAsync(swaggerPath);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.StartsWith("3.", document.RootElement.GetProperty("openapi").GetString());
        var paths = document.RootElement.GetProperty("paths").EnumerateObject().ToList();
        Assert.NotEmpty(paths);
        if (hostName.Length != 0)
            Assert.All(paths, path => Assert.True(path.Name == "/" + routePrefix
                || path.Name.StartsWith("/" + routePrefix + "/", StringComparison.Ordinal)));
        if (moduleName == "GamificationService")
        {
            Assert.Contains(document.RootElement.GetProperty("servers").EnumerateArray(),
                server => server.GetProperty("url").GetString() == "/api/gamification");
            Assert.Contains(paths, path => path.Name == "/shop/buy");
            Assert.Contains(paths, path => path.Value.TryGetProperty("post", out var post)
                && post.TryGetProperty("requestBody", out var body)
                && body.GetProperty("content").EnumerateObject().Any());
        }
    }
}
