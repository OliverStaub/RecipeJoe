using Microsoft.Extensions.AI;

namespace RecipeJoe.UnitTests.Video;

/// <summary>An <see cref="IChatClient"/> that replies with canned text, or throws, like a provider would.</summary>
internal sealed class FakeChatClient(Func<ChatResponse> reply) : IChatClient
{
    public static FakeChatClient Replying(string json) => new(() => new ChatResponse(new ChatMessage(ChatRole.Assistant, json)));

    /// <summary>Answers each call with the next reply in turn (the last one repeats).</summary>
    public static FakeChatClient ReplyingInTurn(params string[] jsons)
    {
        var calls = 0;
        return new FakeChatClient(() => new ChatResponse(new ChatMessage(ChatRole.Assistant, jsons[Math.Min(calls++, jsons.Length - 1)])));
    }

    public static FakeChatClient Throwing(Exception exception) => new(() => throw exception);

    /// <summary>Every request sent, in order.</summary>
    public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

    /// <summary>The options of every request, in order.</summary>
    public List<ChatOptions?> RequestOptions { get; } = [];

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Requests.Add([.. messages]);
        RequestOptions.Add(options);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(reply());
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
