using RecipeJoe.Api.Video;

namespace RecipeJoe.UnitTests.Video;

[TestClass]
public sealed class LlmOptionsValidatorTests
{
    private static bool IsValid(LlmOptions options) => new LlmOptionsValidator().Validate(null, options).Succeeded;

    [TestMethod]
    public void Defaults_are_OpenRouter_with_a_three_minute_timeout()
    {
        var options = new LlmOptions { ApiKey = "sk-test" };

        Assert.AreEqual(LlmProvider.OpenRouter, options.Provider);
        Assert.IsTrue(IsValid(options));
        Assert.AreEqual(TimeSpan.FromMinutes(3), options.Timeout);
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
        Assert.IsFalse(IsValid(new LlmOptions { ApiKey = "k", Model = " " }));
        Assert.IsFalse(IsValid(new LlmOptions { ApiKey = "k", BaseUrl = "not a url" }));
        Assert.IsFalse(IsValid(new LlmOptions { ApiKey = "k", Timeout = TimeSpan.Zero }));
        Assert.IsFalse(IsValid(new LlmOptions { Provider = (LlmProvider)99 }));
    }
}
