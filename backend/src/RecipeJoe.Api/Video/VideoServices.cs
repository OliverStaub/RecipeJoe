using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using RecipeJoe.Api.Import;
using RecipeJoe.Api.Imports;

namespace RecipeJoe.Api.Video;

internal static class VideoServices
{
    public static IServiceCollection AddVideo(this IServiceCollection services)
    {
        services.AddOptions<VideoSourceOptions>().BindConfiguration("VideoSource");
        services.AddOptions<LlmOptions>().BindConfiguration("Llm").ValidateOnStart();
        services.AddSingleton<IValidateOptions<LlmOptions>, LlmOptionsValidator>();

        services.AddScoped<IVideoSource>(sp =>
            sp.GetRequiredService<IOptions<VideoSourceOptions>>().Value.Provider switch
            {
                VideoSourceProvider.Fake => ActivatorUtilities.CreateInstance<FakeVideoSource>(sp),
                // The YoutubeExplode adapter arrives with ticket 07; until then a real video can't be read.
                _ => new UnavailableVideoSource(),
            }
        );
        services.AddSingleton<IChatClient>(sp => LlmClientFactory.Create(sp.GetRequiredService<IOptions<LlmOptions>>().Value));
        services.AddScoped<RecipeExtractor>();
        services.AddKeyedScoped<IImportPath, VideoImportPath>(ImportKind.Video);
        return services;
    }

    private sealed class UnavailableVideoSource : IVideoSource
    {
        public Task<Result<VideoContent, ImportFailure>> LoadAsync(Uri url, CancellationToken cancellationToken) =>
            Task.FromResult(Result<VideoContent, ImportFailure>.Fail(ImportFailure.Unreachable));
    }
}
