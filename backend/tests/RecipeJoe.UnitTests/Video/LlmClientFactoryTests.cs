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
    public void An_unknown_provider_is_not_supported() =>
        Assert.ThrowsExactly<NotSupportedException>(() => LlmClientFactory.Create(new LlmOptions { Provider = (LlmProvider)99, ApiKey = "sk-test" }));
}
