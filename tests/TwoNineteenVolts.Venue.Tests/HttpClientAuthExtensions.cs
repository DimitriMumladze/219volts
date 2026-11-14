using System.Net.Http.Json;

namespace TwoNineteenVolts.Venue.Tests;

public static class HttpClientAuthExtensions
{
    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, int userId, string pin) =>
        client.PostAsJsonAsync("/api/auth/login", new { userId, pin });

    public static Task<HttpResponseMessage> SupportLoginAsync(this HttpClient client, string password) =>
        client.PostAsJsonAsync("/api/auth/support-login", new { password });
}
