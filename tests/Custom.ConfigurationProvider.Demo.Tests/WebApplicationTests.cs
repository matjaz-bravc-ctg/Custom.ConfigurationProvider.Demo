using System.Net;

namespace Custom.ConfigurationProvider.Demo.Tests;

/// <summary>
/// The Razor Pages end to end. Every test starts the application on its own empty database.
/// </summary>
public class WebApplicationTests
{
  private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

  [Fact]
  public async Task Home_ShowsAppSettingsJsonValues_WhenDatabaseIsEmpty()
  {
    await using DemoAppFactory factory = new();
    HttpClient client = factory.CreateBrowser();

    var values = await Pages.GetHomeValuesAsync(client);

    Assert.Equal(("Custom Setting A", "Custom Setting B", "This is Setting A"), values);
  }

  [Fact]
  public async Task CreatedDefault_IsShownOnHome_Immediately()
  {
    await using DemoAppFactory factory = new();
    HttpClient client = factory.CreateBrowser();

    await Pages.CreateAsync(client, "Created A", "Created B", isDefault: true);

    var values = await Pages.GetHomeValuesAsync(client);
    Assert.Equal(("Created A", "Created B"), (values.A, values.B));
  }

  [Fact]
  public async Task FirstConfiguration_BecomesDefault_EvenWhenNotMarkedAsDefault()
  {
    await using DemoAppFactory factory = new();
    HttpClient client = factory.CreateBrowser();

    Pages.Configuration created = await Pages.CreateAsync(client, "Only A", "Only B", isDefault: false);

    Assert.True(created.Default);
    Assert.Equal("Only A", (await Pages.GetHomeValuesAsync(client)).A);
  }

  [Fact]
  public async Task SetDefault_IsAPost_AndKeepsOneDefault()
  {
    await using DemoAppFactory factory = new();
    HttpClient client = factory.CreateBrowser();
    await Pages.CreateAsync(client, "First A", "First B", isDefault: true);
    Pages.Configuration second = await Pages.CreateAsync(client, "Second A", "Second B", isDefault: false);

    // A plain GET used to change the default
    await client.GetAsync($"/CustomSettings?id={second.Id}", CancellationToken);
    Assert.Equal("First A", (await Pages.GetHomeValuesAsync(client)).A);

    HttpResponseMessage response = await Pages.SubmitAsync(client, "/CustomSettings", [], $"/CustomSettings?handler=SetDefault&id={second.Id}");

    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    Assert.Equal("Second A", (await Pages.GetHomeValuesAsync(client)).A);
    Assert.Equal([second.Id], (await Pages.GetConfigurationsAsync(client)).Where(c => c.Default).Select(c => c.Id));
  }

  [Fact]
  public async Task SetDefault_ReturnsNotFound_ForUnknownId()
  {
    // Used to crash the application (null reference, then a stack overflow while logging it)
    await using DemoAppFactory factory = new();
    HttpClient client = factory.CreateBrowser();
    await Pages.CreateAsync(client, "A", "B", isDefault: true);

    HttpResponseMessage response = await Pages.SubmitAsync(client, "/CustomSettings", [], "/CustomSettings?handler=SetDefault&id=999");

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health", CancellationToken)).StatusCode);
  }

  [Fact]
  public async Task Edit_UpdatesConfiguration_AndHome()
  {
    await using DemoAppFactory factory = new();
    HttpClient client = factory.CreateBrowser();
    Pages.Configuration created = await Pages.CreateAsync(client, "Old A", "Old B", isDefault: true);

    HttpResponseMessage response = await Pages.SubmitAsync(client, $"/CustomSettings/Edit?id={created.Id}", new()
    {
      ["AppSettingsCustom.Id"] = created.Id.ToString(),
      ["AppSettingsCustom.CustomSettingA"] = "New A",
      ["AppSettingsCustom.CustomSettingB"] = "New B",
      ["AppSettingsCustom.Default"] = "true"
    });

    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    Assert.Equal(("New A", "New B"), ((await Pages.GetHomeValuesAsync(client)).A, (await Pages.GetHomeValuesAsync(client)).B));
  }

  [Fact]
  public async Task Edit_ReturnsNotFound_ForUnknownId()
  {
    await using DemoAppFactory factory = new();
    HttpClient client = factory.CreateBrowser();
    Pages.Configuration created = await Pages.CreateAsync(client, "A", "B", isDefault: true);

    HttpResponseMessage page = await client.GetAsync("/CustomSettings/Edit?id=999", CancellationToken);
    HttpResponseMessage post = await Pages.SubmitAsync(client, $"/CustomSettings/Edit?id={created.Id}", new()
    {
      ["AppSettingsCustom.Id"] = "999",
      ["AppSettingsCustom.CustomSettingA"] = "X"
    }, "/CustomSettings/Edit?id=999");

    Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
  }

  [Fact]
  public async Task DeletingTheDefault_MakesAnotherConfigurationTheDefault()
  {
    await using DemoAppFactory factory = new();
    HttpClient client = factory.CreateBrowser();
    Pages.Configuration first = await Pages.CreateAsync(client, "First A", "First B", isDefault: false);
    Pages.Configuration second = await Pages.CreateAsync(client, "Second A", "Second B", isDefault: true);

    HttpResponseMessage response = await Pages.SubmitAsync(client, $"/CustomSettings/Delete?id={second.Id}", []);

    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    Assert.Equal([first with { Default = true }], await Pages.GetConfigurationsAsync(client));
    Assert.Equal("First A", (await Pages.GetHomeValuesAsync(client)).A);
  }

  [Fact]
  public async Task DeletingTheLastConfiguration_FallsBackToAppSettingsJson()
  {
    // Used to crash the application, because no configuration was left to make the default
    await using DemoAppFactory factory = new();
    HttpClient client = factory.CreateBrowser();
    Pages.Configuration only = await Pages.CreateAsync(client, "Only A", "Only B", isDefault: true);

    HttpResponseMessage response = await Pages.SubmitAsync(client, $"/CustomSettings/Delete?id={only.Id}", []);

    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    Assert.Empty(await Pages.GetConfigurationsAsync(client));
    Assert.Equal("Custom Setting A", (await Pages.GetHomeValuesAsync(client)).A);
  }

  [Fact]
  public async Task Delete_ReturnsNotFound_ForUnknownId()
  {
    await using DemoAppFactory factory = new();
    HttpClient client = factory.CreateBrowser();
    Pages.Configuration created = await Pages.CreateAsync(client, "A", "B", isDefault: true);

    HttpResponseMessage page = await client.GetAsync("/CustomSettings/Delete?id=999", CancellationToken);
    HttpResponseMessage post = await Pages.SubmitAsync(client, $"/CustomSettings/Delete?id={created.Id}", [], "/CustomSettings/Delete?id=999");

    Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
  }

  [Fact]
  public async Task Health_ReportsHealthy()
  {
    await using DemoAppFactory factory = new();

    string health = await factory.CreateClient().GetStringAsync("/health", CancellationToken);

    Assert.Equal("Healthy", health);
  }

  [Fact]
  public async Task StaticAssets_AreServed()
  {
    await using DemoAppFactory factory = new();

    HttpResponseMessage response = await factory.CreateClient().GetAsync("/lib/bootstrap/dist/css/bootstrap.min.css", CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }
}
