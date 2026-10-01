using RecipeJoe.Api.Video;

namespace RecipeJoe.UnitTests.Video;

[TestClass]
public sealed class LlmOptionsValidatorTests
{
    private static bool IsValid(LlmOptions options) => new LlmOptionsValidator().Validate(null, options).Succeeded;

    [TestMethod]
    public void Defaults_are_valid_Ollama_with_a_three_minute_timeout()
    {
        var options = new LlmOptions();

        Assert.IsTrue(IsValid(options));
        Assert.AreEqual("gemma4:26b", options.ResolvedModel);
        Assert.AreEqual(new Uri("http://host.docker.internal:11434"), options.ResolvedBaseUrl);
        Assert.AreEqual(TimeSpan.FromMinutes(3), options.Timeout);
        Assert.AreEqual(32768, options.ContextTokens);
    }

    [TestMethod]
    public void OpenRouter_needs_an_api_key_and_defaults_its_model_and_endpoint()
    {
        var options = new LlmOptions { Provider = LlmProvider.OpenRouter };

        Assert.IsFalse(IsValid(options));

        options.ApiKey = "sk-test";
        Assert.IsTrue(IsValid(options));
        Assert.AreEqual("google/gemma-4-26b-a4b-it", options.ResolvedModel);
        Assert.AreEqual(new Uri("https://openrouter.ai/api/v1"), options.ResolvedBaseUrl);
    }

    [TestMethod]
    public void A_blank_model_a_relative_base_url_a_non_positive_timeout_or_context_and_an_unknown_provider_are_invalid()
    {
        Assert.IsFalse(IsValid(new LlmOptions { Model = " " }));
        Assert.IsFalse(IsValid(new LlmOptions { BaseUrl = "not a url" }));
        Assert.IsFalse(IsValid(new LlmOptions { Timeout = TimeSpan.Zero }));
        Assert.IsFalse(IsValid(new LlmOptions { Provider = (LlmProvider)99 }));
        Assert.IsFalse(IsValid(new LlmOptions { ContextTokens = 0 }));
    }
}
