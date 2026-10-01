using System.Net;
using System.Text;

namespace RecipeJoe.UnitTests.Sweep;

/// <summary>A local HTTP endpoint that answers like OpenRouter's chat completions, one canned reply per request, decided by the request body.</summary>
internal sealed class StubOpenRouter : IDisposable
{
    private readonly HttpListener listener = new();
    private readonly Func<string, int, (int Status, string Body)> answer;
    private int requests;

    public StubOpenRouter(Func<string, int, (int Status, string Body)> answer)
    {
        this.answer = answer;
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        BaseUrl = $"http://127.0.0.1:{port}/";
        listener.Prefixes.Add(BaseUrl);
        listener.Start();
        _ = Task.Run(ServeAsync);
    }

    public string BaseUrl { get; }

    public int Requests => Volatile.Read(ref requests);

    /// <summary>A 200 completion with <paramref name="content"/> as the assistant message, the serving provider and a usage block with cost.</summary>
    public static (int, string) Completion(string content, string provider = "StubProvider", int promptTokens = 100, int completionTokens = 10, decimal cost = 0.001m)
    {
        var escaped = System.Text.Json.JsonSerializer.Serialize(content);
        var costText = cost.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return (200, $$$"""{"id":"x","object":"chat.completion","created":0,"model":"m","provider":"{{{provider}}}","choices":[{"index":0,"finish_reason":"stop","message":{"role":"assistant","content":{{{escaped}}}}}],"usage":{"prompt_tokens":{{{promptTokens}}},"completion_tokens":{{{completionTokens}}},"total_tokens":{{{promptTokens + completionTokens}}},"cost":{{{costText}}}}}""");
    }

    private async Task ServeAsync()
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
            {
                return;
            }

            using var reader = new StreamReader(context.Request.InputStream);
            var body = await reader.ReadToEndAsync();
            var (status, reply) = answer(body, Interlocked.Increment(ref requests) - 1);
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(reply));
            context.Response.Close();
        }
    }

    public void Dispose() => listener.Close();
}
