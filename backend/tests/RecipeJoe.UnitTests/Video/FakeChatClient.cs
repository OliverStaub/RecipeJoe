using Microsoft.Extensions.AI;

namespace RecipeJoe.UnitTests.Video;

/// <summary>An <see cref="IChatClient"/> that replies with canned text, or throws, like a provider would.</summary>
internal sealed class FakeChatClient(Func<ChatResponse> reply) : IChatClient
{
    public static FakeChatClient Replying(string json) => new(() => new ChatResponse(new ChatMessage(ChatRole.Assistant, json)));

    public static FakeChatClient Throwing(Exception exception) => new(() => throw exception);

    /// <summary>Every request sent, in order.</summary>
    public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Requests.Add([.. messages]);
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
