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
}
