using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Miscord.Server.Data;
using Miscord.Server.DTOs;
using Miscord.Server.Services;

namespace Miscord.Server.Tests;

public class IntegrationTestBase : IDisposable
{
    public readonly WebApplicationFactory<Program> Factory;
    public readonly HttpClient Client;
    private readonly string _dbName;

    private const string TestSecretKey = "ThisIsATestSecretKeyThatIsLongEnoughForHmacSha256Testing!";
    private const string TestIssuer = "TestIssuer";
    private const string TestAudience = "TestAudience";

    public IntegrationTestBase()
    {
        _dbName = Guid.NewGuid().ToString();

        var testConfig = new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = TestSecretKey,
            ["Jwt:Issuer"] = TestIssuer,
            ["Jwt:Audience"] = TestAudience,
            ["Jwt:AccessTokenExpirationMinutes"] = "60",
            ["Jwt:RefreshTokenExpirationDays"] = "7"
        };

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    // Clear existing configuration sources and add test config
                    config.Sources.Clear();
                    config.AddInMemoryCollection(testConfig);
                });

                builder.ConfigureServices(services =>
                {
                    // Remove the real database registrations
                    var dbContextDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<MiscordDbContext>));
                    if (dbContextDescriptor != null)
                        services.Remove(dbContextDescriptor);

                    var dbContextOptionsDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions));
                    if (dbContextOptionsDescriptor != null)
                        services.Remove(dbContextOptionsDescriptor);

                    // Remove all DbContext-related services
                    var descriptorsToRemove = services
                        .Where(d => d.ServiceType == typeof(MiscordDbContext) ||
                                    d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true)
                        .ToList();
                    foreach (var descriptor in descriptorsToRemove)
                        services.Remove(descriptor);

                    // Add in-memory database
                    services.AddDbContext<MiscordDbContext>(options =>
                    {
                        options.UseInMemoryDatabase(_dbName);
                    });

                    // Reconfigure JWT Bearer authentication with test settings
                    services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                    {
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidateAudience = true,
                            ValidateLifetime = true,
                            ValidateIssuerSigningKey = true,
                            ValidIssuer = TestIssuer,
                            ValidAudience = TestAudience,
                            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecretKey)),
                            ClockSkew = TimeSpan.Zero
                        };
                    });
                });
            });

        Client = Factory.CreateClient();
    }

    public async Task<AuthResponse> RegisterUserAsync(string username, string email, string password)
    {
        var inviteCode = await CreateInviteCodeAsync();
        var response = await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(username, email, password, inviteCode));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!;
    }

    public async Task<string> CreateInviteCodeAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var inviteService = scope.ServiceProvider.GetRequiredService<IServerInviteService>();
        var invite = await inviteService.CreateInviteAsync(null, maxUses: 0);
        return invite.Code;
    }

    public async Task<AuthResponse> LoginUserAsync(string email, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!;
    }

    public void SetAuthToken(string token)
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public void ClearAuthToken()
    {
        Client.DefaultRequestHeaders.Authorization = null;
    }

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
    }
}
