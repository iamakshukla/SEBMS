using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SEBMS.Api.Services;
using SEBMS.Infrastructure.Identity;

namespace SEBMS.Tests;

public class JwtTokenServiceTests
{
    private const string Key = "unit-test-signing-key-that-is-long-enough-for-hmac-sha256";

    private static JwtTokenService Service() => new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = Key, ["Jwt:Issuer"] = "SEBMS", ["Jwt:Audience"] = "SEBMS.Clients"
        }).Build());

    private static readonly ApplicationUser User = new() { Id = "u-1", Email = "a@b.com", UserName = "a@b.com" };

    [Fact]
    public void Create_ProducesTokenCarryingUserIdAndEmail()
    {
        var (token, _) = Service().Create(User);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("SEBMS", jwt.Issuer);
        Assert.Contains("SEBMS.Clients", jwt.Audiences);
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.NameIdentifier && c.Value == "u-1");
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "a@b.com");
    }

    [Fact]
    public void Create_ExpiresInAboutEightHours()
    {
        var (_, expiresAt) = Service().Create(User);

        var hours = (expiresAt - DateTime.UtcNow).TotalHours;
        Assert.InRange(hours, 7.9, 8.1);
    }

    [Fact]
    public void Create_TokenValidatesWithTheSigningKey_AndFailsWithAnother()
    {
        var (token, _) = Service().Create(User);
        var parameters = new TokenValidationParameters
        {
            ValidIssuer = "SEBMS", ValidAudience = "SEBMS.Clients",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key))
        };
        var handler = new JwtSecurityTokenHandler();

        handler.ValidateToken(token, parameters, out _);   // does not throw

        parameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("a-completely-different-signing-key-0123456789"));
        Assert.ThrowsAny<SecurityTokenException>(() => handler.ValidateToken(token, parameters, out _));
    }
}
