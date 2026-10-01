using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using RecipeJoe.Api.Video;

namespace RecipeJoe.UnitTests.Video;

[TestClass]
public sealed class LlmClientFactoryTests
{
    private static string? ProviderOf(LlmOptions options)
    {
        using var client = LlmClientFactory.Create(options);
        return client.GetService<ChatClientMetadata>()?.ProviderName;
    }

    [TestMethod]
    public void Ollama_is_the_default_provider() =>
        Assert.AreEqual("ollama", ProviderOf(new LlmOptions())?.ToLowerInvariant());

    [TestMethod]
    public void OpenRouter_is_selected_by_config() =>
        Assert.AreEqual("openai", ProviderOf(new LlmOptions { Provider = LlmProvider.OpenRouter, ApiKey = "sk-test" })?.ToLowerInvariant());

    [TestMethod]
    public void Ollama_uses_the_configured_model_and_endpoint()
    {
        using var client = LlmClientFactory.Create(new LlmOptions { Model = "tiny:1b", BaseUrl = "http://localhost:11434" });
        var metadata = client.GetService<ChatClientMetadata>();

        Assert.AreEqual("tiny:1b", metadata?.DefaultModelId);
        Assert.AreEqual(new Uri("http://localhost:11434"), metadata?.ProviderUri);
    }

    [TestMethod]
    public async Task Ollama_requests_carry_a_constant_num_ctx_and_refuse_to_truncate()
    {
        var handler = new CapturingHandler();
        using var client = LlmClientFactory.Create(new LlmOptions { ContextTokens = 4096 }, handler);

        await client.GetResponseAsync("hallo");

        var body = JsonNode.Parse(handler.Body!)!;
        Assert.AreEqual(4096, (int)body["options"]!["num_ctx"]!);
        Assert.IsFalse((bool)body["truncate"]!);
        Assert.IsFalse((bool)body["think"]!, "the existing options must survive");
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"model":"m","message":{"role":"assistant","content":"hi"},"done":true}""", System.Text.Encoding.UTF8, "application/x-ndjson"),
            };
        }
    }
}
