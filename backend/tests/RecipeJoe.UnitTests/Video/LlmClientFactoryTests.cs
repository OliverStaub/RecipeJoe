using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using RecipeJoe.Api.Video;

namespace RecipeJoe.UnitTests.Video;

[TestClass]
public sealed class LlmClientFactoryTests
{
    [TestMethod]
    public void OpenRouter_is_the_default_provider()
    {
        using var client = LlmClientFactory.Create(new LlmOptions { ApiKey = "sk-test" });

        Assert.AreEqual("openai", client.GetService<ChatClientMetadata>()?.ProviderName?.ToLowerInvariant());
    }

    [TestMethod]
    public void OpenRouter_uses_the_configured_model_and_endpoint()
    {
        using var client = LlmClientFactory.Create(new LlmOptions { ApiKey = "sk-test", Model = "tiny/model", BaseUrl = "http://localhost:9999/v1" });
        var metadata = client.GetService<ChatClientMetadata>();

        Assert.AreEqual("tiny/model", metadata?.DefaultModelId);
        Assert.AreEqual(new Uri("http://localhost:9999/v1"), metadata?.ProviderUri);
    }

    [TestMethod]
    public async Task OpenRouter_requests_a_strict_json_schema_from_providers_that_honour_it()
    {
        using var listener = new HttpListener();
        var prefix = $"http://127.0.0.1:{FreePort()}/";
        listener.Prefixes.Add(prefix);
        listener.Start();
        var served = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            using var reader = new StreamReader(context.Request.InputStream);
            var body = await reader.ReadToEndAsync();
            var reply = Encoding.UTF8.GetBytes("""{"id":"x","object":"chat.completion","created":0,"model":"m","choices":[{"index":0,"finish_reason":"stop","message":{"role":"assistant","content":"{\"recipes\":[]}"}}]}""");
            context.Response.ContentType = "application/json";
            await context.Response.OutputStream.WriteAsync(reply);
            context.Response.Close();
            return body;
        });

        using var client = LlmClientFactory.Create(new LlmOptions { ApiKey = "sk-test", Model = "m", BaseUrl = prefix });
        await client.GetResponseAsync<ExtractionReply>([new ChatMessage(ChatRole.User, "hi")], useJsonSchemaResponseFormat: true);

        using var request = JsonDocument.Parse(await served);
        var format = request.RootElement.GetProperty("response_format");
        Assert.AreEqual("json_schema", format.GetProperty("type").GetString());
        Assert.IsTrue(format.GetProperty("json_schema").GetProperty("strict").GetBoolean());
        Assert.IsTrue(request.RootElement.GetProperty("provider").GetProperty("require_parameters").GetBoolean());
        AssertStrictObjects(format.GetProperty("json_schema").GetProperty("schema"));
    }

    /// <summary>Strict mode needs every object closed (<c>additionalProperties: false</c>) with all its properties required.</summary>
    private static void AssertStrictObjects(JsonElement node)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
                if (node.TryGetProperty("properties", out var properties))
                {
                    Assert.IsTrue(node.TryGetProperty("additionalProperties", out var closed) && closed.ValueKind == JsonValueKind.False, "object must forbid additional properties");
                    var required = node.GetProperty("required").EnumerateArray().Select(r => r.GetString()).ToHashSet();
                    CollectionAssert.AreEquivalent(properties.EnumerateObject().Select(p => p.Name).ToList(), required.ToList());
                }
                foreach (var child in node.EnumerateObject())
                    AssertStrictObjects(child.Value);
                break;
            case JsonValueKind.Array:
                foreach (var item in node.EnumerateArray())
                    AssertStrictObjects(item);
                break;
        }
    }

    private static int FreePort()
    {
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    [TestMethod]
    public void An_unknown_provider_is_not_supported() =>
        Assert.ThrowsExactly<NotSupportedException>(() => LlmClientFactory.Create(new LlmOptions { Provider = (LlmProvider)99, ApiKey = "sk-test" }));
}
