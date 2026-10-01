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

    /// <summary>Sends one request through a client for <paramref name="options"/> to a local stub and returns the body the stub saw.</summary>
    private static async Task<JsonDocument> SendAsync(LlmOptions options)
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

        options.BaseUrl = prefix;
        using var client = LlmClientFactory.Create(options);
        await client.GetResponseAsync<ExtractionReply>([new ChatMessage(ChatRole.User, "hi")], useJsonSchemaResponseFormat: true);
        return JsonDocument.Parse(await served);
    }

    [TestMethod]
    public async Task Without_pinning_only_require_parameters_is_sent_for_provider_routing()
    {
        using var request = await SendAsync(new LlmOptions { ApiKey = "sk-test", Model = "m" });

        var provider = request.RootElement.GetProperty("provider");
        Assert.IsTrue(provider.GetProperty("require_parameters").GetBoolean());
        Assert.IsFalse(provider.TryGetProperty("order", out _));
        Assert.IsFalse(provider.TryGetProperty("allow_fallbacks", out _));
    }

    [TestMethod]
    public async Task Pinned_providers_are_sent_in_order_without_fallbacks_next_to_require_parameters()
    {
        using var request = await SendAsync(new LlmOptions { ApiKey = "sk-test", Model = "m", PinnedProviders = "Google AI Studio, Vertex" });

        var provider = request.RootElement.GetProperty("provider");
        Assert.IsTrue(provider.GetProperty("require_parameters").GetBoolean());
        Assert.IsFalse(provider.GetProperty("allow_fallbacks").GetBoolean());
        Assert.IsTrue(provider.GetProperty("order").EnumerateArray().Select(e => e.GetString()).SequenceEqual(["Google AI Studio", "Vertex"]));
    }

    [TestMethod]
    public async Task The_provider_that_served_a_reply_is_readable_from_the_response()
    {
        using var listener = new HttpListener();
        var prefix = $"http://127.0.0.1:{FreePort()}/";
        listener.Prefixes.Add(prefix);
        listener.Start();
        _ = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            var reply = Encoding.UTF8.GetBytes("""{"id":"x","object":"chat.completion","created":0,"model":"m","provider":"Google AI Studio","choices":[{"index":0,"finish_reason":"stop","message":{"role":"assistant","content":"{\"recipes\":[]}"}}]}""");
            context.Response.ContentType = "application/json";
            await context.Response.OutputStream.WriteAsync(reply);
            context.Response.Close();
        });

        using var client = LlmClientFactory.Create(new LlmOptions { ApiKey = "sk-test", Model = "m", BaseUrl = prefix });
        var response = await client.GetResponseAsync<ExtractionReply>([new ChatMessage(ChatRole.User, "hi")], useJsonSchemaResponseFormat: true);

        Assert.AreEqual("Google AI Studio", LlmClientFactory.ServingProvider(response));
    }

    [TestMethod]
    public async Task The_cost_of_a_reply_is_readable_from_the_usage_block_of_the_response()
    {
        using var listener = new HttpListener();
        var prefix = $"http://127.0.0.1:{FreePort()}/";
        listener.Prefixes.Add(prefix);
        listener.Start();
        _ = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            var reply = Encoding.UTF8.GetBytes("""{"id":"x","object":"chat.completion","created":0,"model":"m","provider":"Google AI Studio","choices":[{"index":0,"finish_reason":"stop","message":{"role":"assistant","content":"{\"recipes\":[]}"}}],"usage":{"prompt_tokens":120,"completion_tokens":7,"total_tokens":127,"cost":0.000123,"cost_details":{"upstream_inference_cost":null}}}""");
            context.Response.ContentType = "application/json";
            await context.Response.OutputStream.WriteAsync(reply);
            context.Response.Close();
        });

        using var client = LlmClientFactory.Create(new LlmOptions { ApiKey = "sk-test", Model = "m", BaseUrl = prefix });
        var response = await client.GetResponseAsync<ExtractionReply>([new ChatMessage(ChatRole.User, "hi")], useJsonSchemaResponseFormat: true);

        Assert.AreEqual(0.000123m, LlmClientFactory.Cost(response));
        Assert.AreEqual(120, response.Usage?.InputTokenCount);
        Assert.AreEqual(7, response.Usage?.OutputTokenCount);
    }

    [TestMethod]
    public void A_response_without_raw_data_has_no_known_cost() =>
        Assert.IsNull(LlmClientFactory.Cost(new ChatResponse()));

    [TestMethod]
    public void A_response_without_raw_data_has_no_known_serving_provider() =>
        Assert.IsNull(LlmClientFactory.ServingProvider(new ChatResponse()));

    [TestMethod]
    public async Task OpenRouter_requests_a_strict_json_schema_from_providers_that_honour_it()
    {
        using var request = await SendAsync(new LlmOptions { ApiKey = "sk-test", Model = "m" });

        var format = request.RootElement.GetProperty("response_format");
        Assert.AreEqual("json_schema", format.GetProperty("type").GetString());
        Assert.IsTrue(format.GetProperty("json_schema").GetProperty("strict").GetBoolean());
        Assert.IsTrue(request.RootElement.GetProperty("provider").GetProperty("require_parameters").GetBoolean());
        var schema = format.GetProperty("json_schema").GetProperty("schema");
        AssertStrictObjects(schema);

        var recipe = schema.GetProperty("properties").GetProperty("recipes").GetProperty("items");
        foreach (var name in new[] { "ingredientLines", "steps" })
        {
            var list = recipe.GetProperty("properties").GetProperty(name);
            Assert.AreEqual(JsonValueKind.String, list.GetProperty("type").ValueKind, $"{name} must not be nullable");
            // The chat library's strict transform turns minItems into a line of the description instead of a keyword.
            StringAssert.Contains(list.GetProperty("description").GetString(), "minItems: 1", $"{name} needs at least one entry");
        }
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
