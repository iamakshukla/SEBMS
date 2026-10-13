using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SEBMS.Web.Services;

public record ApiResult<T>(T? Data, HttpStatusCode Status, IReadOnlyList<string> Errors)
{
    public bool Ok => (int)Status is >= 200 and < 300;
    public bool Unauthorized => Status == HttpStatusCode.Unauthorized;
}

/// <summary>Thin wrapper over the SEBMS API. Attaches the signed-in user's JWT to every call.</summary>
public class ApiClient(HttpClient http, IHttpContextAccessor accessor)
{
    public const string TokenClaim = "access_token";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public Task<ApiResult<T>> GetAsync<T>(string url) => SendAsync<T>(HttpMethod.Get, url, null);
    public Task<ApiResult<T>> PostAsync<T>(string url, object body) => SendAsync<T>(HttpMethod.Post, url, body);
    public Task<ApiResult<T>> PutAsync<T>(string url, object body) => SendAsync<T>(HttpMethod.Put, url, body);
    public Task<ApiResult<object>> DeleteAsync(string url) => SendAsync<object>(HttpMethod.Delete, url, null);

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object? body)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);

        var token = accessor.HttpContext?.User.FindFirst(TokenClaim)?.Value;
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request);
        }
        catch (HttpRequestException)
        {
            return new(default, HttpStatusCode.ServiceUnavailable, ["The API is not reachable. Make sure SEBMS.Api is running."]);
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                return new(default, response.StatusCode, ParseErrors(text, response.StatusCode));

            if (string.IsNullOrWhiteSpace(text) || typeof(T) == typeof(object))
                return new(default, response.StatusCode, []);
            return new(JsonSerializer.Deserialize<T>(text, Json), response.StatusCode, []);
        }
    }

    // The API returns plain text, a JSON array of strings, or a ValidationProblemDetails object.
    private static IReadOnlyList<string> ParseErrors(string text, HttpStatusCode status)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [status == HttpStatusCode.Unauthorized ? "Your session has expired. Please sign in again." : $"Request failed ({(int)status})."];
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
                return root.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s != "").ToList();
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("errors", out var errors))
                return errors.EnumerateObject().SelectMany(p => p.Value.EnumerateArray().Select(e => e.GetString() ?? "")).ToList();
            if (root.ValueKind == JsonValueKind.String)
                return [root.GetString()!];
        }
        catch (JsonException) { /* plain text */ }
        return [text.Trim('"')];
    }
}
