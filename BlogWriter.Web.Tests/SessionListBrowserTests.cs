using Bunit;
using BlogWriter.Web.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;

namespace BlogWriter.Web.Tests;

public sealed class SessionListBrowserTests : BunitContext
{
    [Theory]
    [InlineData(320)]
    [InlineData(1280)]
    public async Task LiveWorkspace_RestoresByPointerAndKeyboardAndBlocksConcurrentSelection(int width)
    {
        var sessions = new SavedListSessionService();
        for (int index = 0; index < 3; index++)
        {
            BlogSession session = ListLauncherTestHelpers.Session($"saved topic {index}", $"revision {index}", $"saved draft {index}");
            session.State.MinWords = 700 + index;
            session.State.MaxWords = 1350 + index;
            sessions.Sessions.Add(session);
        }
        string before = System.Text.Json.JsonSerializer.Serialize(sessions.Sessions);
        await using var factory = new LiveWorkspaceFactory(sessions);
        factory.UseKestrel(0);
        using HttpClient client = factory.CreateClient();
        Assert.Same(sessions, factory.Services.GetRequiredService<IBlogWriterSessionService>());
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true,
            Args = ["--disable-features=LocalNetworkAccessChecks,LocalNetworkAccessChecksWebSockets"],
        });
        IPage page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = width, Height = 900 } });
        var browserMessages = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var connection = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        page.Console += (_, message) =>
        {
            browserMessages.Enqueue(message.Text);
            if (message.Text.Contains("WebSocket connected", StringComparison.Ordinal)) connection.TrySetResult();
        };
        page.PageError += (_, message) => browserMessages.Enqueue(message);
        await page.RouteAsync(client.BaseAddress!.ToString(), async route =>
        {
            IAPIResponse response = await route.FetchAsync();
            string html = await response.TextAsync();
            html = System.Text.RegularExpressions.Regex.Replace(html,
                "(<script\\b[^>]*\\bsrc=\"[^\"]*blazor[^>]*)(>)", "$1 autostart=\"false\"$2");
            await route.FulfillAsync(new() { Response = response, Body = html });
        });
        await page.GotoAsync(client.BaseAddress!.ToString());
        await page.EvaluateAsync("() => Blazor.start()");
        await connection.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await page.WaitForFunctionAsync("() => Array.from(document.querySelector('#initial-prompt').attributes).some(attribute => attribute.name.startsWith('_bl_'))");
        await page.Locator("button[data-command='new']").ClickAsync();
        await Assertions.Expect(page.Locator("#initial-prompt")).ToBeFocusedAsync();

        for (int index = 0; index < 3; index++)
        {
            await page.Locator("button[data-command='list']").ClickAsync();
            if (index > 0)
            {
                await page.Locator("button[data-confirm='discard']").ClickAsync();
            }
            try
            {
                await Assertions.Expect(page.Locator("button.session-entry")).ToHaveCountAsync(3);
            }
            catch (PlaywrightException error)
            {
                throw new Xunit.Sdk.XunitException($"Iteration {index}; List calls: {sessions.ListCalls}; browser: {string.Join(" | ", browserMessages)}; page: {await page.Locator("body").InnerTextAsync()}; assertion: {error.Message}");
            }
            ILocator selected = page.Locator("button.session-entry").Nth(index);
            if (index == 0)
            {
                await selected.ClickAsync();
            }
            else
            {
                await page.Locator("button.session-entry").Nth(index - 1).FocusAsync();
                await page.Keyboard.PressAsync("Tab");
                Assert.True(await selected.EvaluateAsync<bool>("button => button === document.activeElement"));
                await page.Keyboard.PressAsync(index == 1 ? "Enter" : "Space");
            }
            await Assertions.Expect(page.Locator("#initial-prompt")).ToHaveValueAsync($"saved topic {index}");
            await Assertions.Expect(page.Locator("#revision-prompt")).ToHaveValueAsync($"revision {index}");
            await Assertions.Expect(page.Locator("#min-words")).ToHaveValueAsync((700 + index).ToString());
            await Assertions.Expect(page.Locator("#max-words")).ToHaveValueAsync((1350 + index).ToString());
            Assert.Equal(sessions.Sessions.Take(index + 1).Select(session => session.Id), sessions.LoadedIds.ToArray());
        }

        sessions.PendingLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await page.Locator("button[data-command='list']").ClickAsync();
        await page.Locator("button[data-confirm='discard']").ClickAsync();
        await Assertions.Expect(page.Locator("button.session-entry")).ToHaveCountAsync(3);
        await page.Locator("button.session-entry").First.ClickAsync();
        await Assertions.Expect(page.Locator("button.session-entry").Nth(1)).ToBeDisabledAsync();
        await page.Locator("button.session-entry").Nth(1).ClickAsync(new() { Force = true });
        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.PressAsync("Enter");
        await page.Keyboard.PressAsync("Space");
        sessions.PendingLoad.SetResult(sessions.Sessions[0]);
        await Assertions.Expect(page.Locator("#initial-prompt")).ToHaveValueAsync("saved topic 0");
        await Assertions.Expect(page.Locator("button.session-entry")).ToHaveCountAsync(0);
        Assert.Equal(new[] { sessions.Sessions[0].Id, sessions.Sessions[1].Id, sessions.Sessions[2].Id, sessions.Sessions[0].Id }, sessions.LoadedIds.ToArray());
        Assert.Equal(0, sessions.StartCalls);
        Assert.Equal(0, sessions.RevisionCalls);
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(sessions.Sessions));
    }

    [Theory]
    [InlineData(320)]
    [InlineData(1280)]
    public async Task Entries_WrapWithinRowsWithReadableHierarchy(int width)
    {
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        IPage page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = width, Height = 900 } });
        await page.RouteAsync("**/*", route => route.FulfillAsync(new() { Status = 200, Body = "" }));
        await SetContentAsync(page, selectionEnabled: true);

        string[] issues = await page.EvaluateAsync<string[]>("""
            () => {
                const issues = [];
                if (document.documentElement.scrollWidth > window.innerWidth) issues.push('page overflow');
                for (const entry of document.querySelectorAll('.session-entry')) {
                    const row = entry.getBoundingClientRect();
                    for (const field of entry.querySelectorAll('.session-number,.session-details,strong,.session-metadata,time,.session-word-limits,.session-word-limits>span,.session-preview,.session-preview-label,.session-preview-text')) {
                        const bounds = field.getBoundingClientRect();
                        if (bounds.left < row.left - 1 || bounds.right > row.right + 1 || bounds.top < row.top - 1 || bounds.bottom > row.bottom + 1) issues.push('field outside row: ' + field.className);
                    }
                    const heading = entry.querySelector('strong').getBoundingClientRect();
                    const metadata = entry.querySelector('.session-metadata').getBoundingClientRect();
                    const preview = entry.querySelector('.session-preview').getBoundingClientRect();
                    if (metadata.top < heading.bottom - 1 || preview.top < metadata.bottom - 1) issues.push('overlapping hierarchy');
                    if (entry.scrollWidth > entry.clientWidth) issues.push('row overflow');
                }
                return issues;
            }
            """);

        string screenshots = Path.Combine(AppContext.BaseDirectory, "TestResults");
        Directory.CreateDirectory(screenshots);
        await page.ScreenshotAsync(new() { Path = Path.Combine(screenshots, $"session-list-{width}.png"), FullPage = true });
        Assert.Empty(issues);
        Assert.Equal(3, await page.Locator("button.session-entry").CountAsync());
        Assert.Equal(0, await page.Locator("script:not([type])").CountAsync());
        Assert.Contains("No draft yet", await page.Locator(".session-entry").Nth(1).InnerTextAsync());
    }

    [Theory]
    [InlineData(320)]
    [InlineData(1280)]
    public async Task NativeButtons_ActivateWithPointerEnterAndSpaceButNotWhenDisabled(int width)
    {
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        IPage page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = width, Height = 900 } });
        await page.RouteAsync("**/*", route => route.FulfillAsync(new() { Status = 200, Body = "" }));
        await SetContentAsync(page, selectionEnabled: true);
        await RecordNativeActivationsAsync(page);

        await page.Keyboard.PressAsync("Tab");
        Assert.True(await page.Locator(".session-entry").First.EvaluateAsync<bool>("button => button === document.activeElement"));
        Assert.True(await page.Locator(".session-entry").First.EvaluateAsync<bool>("button => getComputedStyle(button).outlineStyle !== 'none'"));
        await page.Keyboard.PressAsync("Enter");
        await page.Keyboard.PressAsync("Space");
        await page.Locator(".session-entry").Nth(1).ClickAsync();
        Assert.Equal(new[] { 0, 0, 1 }, await page.EvaluateAsync<int[]>("window.sessionSelections"));

        await SetContentAsync(page, selectionEnabled: false);
        await RecordNativeActivationsAsync(page);
        await page.Locator(".session-entry").First.ClickAsync(new() { Force = true });
        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.PressAsync("Enter");
        await page.Keyboard.PressAsync("Space");
        Assert.Empty(await page.EvaluateAsync<int[]>("window.sessionSelections"));
    }

    private async Task SetContentAsync(IPage page, bool selectionEnabled)
    {
        BlogSession source = ListLauncherTestHelpers.Session(new string('T', 180), draft: new string('W', 180) + " opening draft words");
        BlogSessionSummary[] summaries =
        [
            BlogSessionSummary.Create(source.Id, source.State.MainTask, source.CreatedAt, source.UpdatedAt,
                int.MaxValue, int.MaxValue, source.State.Draft),
            ListLauncherTestHelpers.Summary("same topic"),
            ListLauncherTestHelpers.Summary("same topic") with { DraftPreview = "<b>literal markup</b> short draft" },
        ];
        var cut = Render<SessionList>(parameters => parameters
            .Add(component => component.Sessions, summaries)
            .Add(component => component.SelectionEnabled, selectionEnabled));
        await page.SetContentAsync("<!doctype html><html><head><meta name=viewport content=\"width=device-width,initial-scale=1\"></head><body><main class=\"app-main\">" + cut.Markup + "</main></body></html>");
        await page.AddStyleTagAsync(new() { Content = await File.ReadAllTextAsync(FindStylesheet()) });
    }

    private static Task RecordNativeActivationsAsync(IPage page) => page.EvaluateAsync("""
        () => {
            window.sessionSelections = [];
            document.querySelectorAll('.session-entry').forEach((button, index) =>
                button.addEventListener('click', () => window.sessionSelections.push(index)));
        }
        """);

    private static string FindStylesheet()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string path = Path.Combine(directory.FullName, "BlogWriter.Web", "wwwroot", "app.css");
            if (File.Exists(path)) return path;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Cannot locate the workspace stylesheet.");
    }

    private sealed class LiveWorkspaceFactory(SavedListSessionService sessions) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:UseTestingIdentity"] = "true",
                ["AzureResources:CredentialMode"] = "ManagedIdentity",
                ["Foundry:ProjectEndpoint"] = "https://unused-foundry.invalid/",
                ["Cosmos:Endpoint"] = "https://unused-cosmos.invalid/",
                ["Cosmos:DatabaseName"] = "test",
                ["Cosmos:ContainerName"] = "test",
            }));
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IBlogWriterSessionService>();
                services.AddSingleton<IBlogWriterSessionService>(sessions);
            });
        }
    }
}
