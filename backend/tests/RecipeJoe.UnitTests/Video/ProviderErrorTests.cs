using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecipeJoe.Api;
using RecipeJoe.Api.Import;
using RecipeJoe.Api.Video;

namespace RecipeJoe.UnitTests.Video;

[TestClass]
public sealed class ProviderErrorTests
{
    private static readonly VideoText Video = new("Zwei Kuchen", "Backen mit Oma", "Erst Teig, dann Guss.");

    [TestMethod]
    public async Task A_finish_reason_error_reply_is_LlmUnavailable_and_logs_the_provider_message()
    {
        var logger = new CapturingLogger();

        var result = await ExtractFromAsync(
            """{"id":"x","object":"chat.completion","created":0,"model":"m","provider":"Chutes","choices":[{"index":0,"finish_reason":"error","native_finish_reason":"error","error":{"code":502,"message":"Upstream melted"},"message":{"role":"assistant","content":""}}]}""",
            HttpStatusCode.OK,
            logger
        );

        Assert.AreEqual(ImportFailure.LlmUnavailable, result.Failure);
        StringAssert.Contains(logger.Text, "Upstream melted");
        StringAssert.Contains(logger.Text, "Chutes");
    }

    [TestMethod]
    public async Task A_top_level_error_object_on_HTTP_200_is_LlmUnavailable_and_logs_the_provider_message()
    {
        var logger = new CapturingLogger();

        var result = await ExtractFromAsync("""{"error":{"code":429,"message":"Rate limited upstream","metadata":{"provider_name":"Targon"}}}""", HttpStatusCode.OK, logger);

        Assert.AreEqual(ImportFailure.LlmUnavailable, result.Failure);
        StringAssert.Contains(logger.Text, "Rate limited upstream");
        StringAssert.Contains(logger.Text, "Targon");
    }

    private static async Task<Result<IReadOnlyList<ParsedRecipe>, ImportFailure>> ExtractFromAsync(string reply, HttpStatusCode status, ILogger<RecipeExtractor> logger)
    {
        using var listener = new HttpListener();
        var prefix = $"http://127.0.0.1:{FreePort()}/";
        listener.Prefixes.Add(prefix);
        listener.Start();
        _ = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            context.Response.StatusCode = (int)status;
            context.Response.ContentType = "application/json";
            await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(reply));
            context.Response.Close();
        });

        var options = new LlmOptions { ApiKey = "sk-test", Model = "m", BaseUrl = prefix, Timeout = TimeSpan.FromSeconds(30) };
        using var client = LlmClientFactory.Create(options);
        return await new RecipeExtractor(client, Options.Create(options), logger).ExtractAsync(Video, CancellationToken.None);
    }

    private static int FreePort()
    {
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private sealed class CapturingLogger : ILogger<RecipeExtractor>
    {
        private readonly StringBuilder _text = new();

        public string Text => _text.ToString();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            _text.AppendLine(string.Concat(formatter(state, exception), " ", exception?.ToString()));
    }
}
