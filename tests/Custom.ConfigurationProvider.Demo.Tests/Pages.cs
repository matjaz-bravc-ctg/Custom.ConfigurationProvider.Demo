using System.Net;
using System.Text.RegularExpressions;

namespace Custom.ConfigurationProvider.Demo.Tests;

/// <summary>
/// Reads values from the rendered pages and submits their forms
/// </summary>
public static partial class Pages
{
  public sealed record Configuration(int Id, string CustomSettingA, string CustomSettingB, bool Default);

  private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

  public static async Task<(string A, string B, string StaticA)> GetHomeValuesAsync(HttpClient client)
  {
    string html = await client.GetStringAsync("/", CancellationToken);
    return (Cell(html, "CustomSettingA"), Cell(html, "CustomSettingB"), Cell(html, "SettingA"));
  }

  public static async Task<List<Configuration>> GetConfigurationsAsync(HttpClient client)
  {
    string html = await client.GetStringAsync("/CustomSettings", CancellationToken);
    return ConfigurationRow().Matches(html)
      .Select(match => new Configuration(
        int.Parse(match.Groups["id"].Value),
        WebUtility.HtmlDecode(match.Groups["a"].Value),
        WebUtility.HtmlDecode(match.Groups["b"].Value),
        match.Groups["checkbox"].Value.Contains("checked")))
      .ToList();
  }

  /// <summary>
  /// Opens the page with the form (for its antiforgery token) and submits the fields
  /// </summary>
  public static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, string formPage, Dictionary<string, string> fields, string? action = null)
  {
    string html = await client.GetStringAsync(formPage, CancellationToken);
    Match token = AntiforgeryToken().Match(html);
    Assert.True(token.Success, $"No antiforgery token on {formPage}");
    fields["__RequestVerificationToken"] = token.Groups[1].Value;
    return await client.PostAsync(action ?? formPage, new FormUrlEncodedContent(fields), CancellationToken);
  }

  public static async Task<Configuration> CreateAsync(HttpClient client, string a, string b, bool isDefault)
  {
    HttpResponseMessage response = await SubmitAsync(client, "/CustomSettings/Create", new()
    {
      ["AppSettingsCustom.CustomSettingA"] = a,
      ["AppSettingsCustom.CustomSettingB"] = b,
      ["AppSettingsCustom.Default"] = isDefault ? "true" : "false"
    });
    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    return (await GetConfigurationsAsync(client)).Last(configuration => configuration.CustomSettingA == a);
  }

  private static string Cell(string html, string name)
  {
    Match match = Regex.Match(html, $@"<td>{name}</td>\s*<td>(.*?)</td>", RegexOptions.Singleline);
    Assert.True(match.Success, $"{name} not found on the page");
    return WebUtility.HtmlDecode(match.Groups[1].Value.Trim());
  }

  [GeneratedRegex(@"<th scope=""row"">(?<a>.*?)</th>\s*<td>(?<b>.*?)</td>\s*<td><input type=""checkbox""(?<checkbox>[^>]*)>.*?/CustomSettings/Edit\?id=(?<id>\d+)", RegexOptions.Singleline)]
  private static partial Regex ConfigurationRow();

  [GeneratedRegex(@"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""")]
  private static partial Regex AntiforgeryToken();
}
