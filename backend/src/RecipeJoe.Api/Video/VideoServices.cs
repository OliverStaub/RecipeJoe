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

        services.AddHttpClient<YoutubeExplodeVideoSource>();
        services.AddScoped<IVideoSource>(sp =>
            sp.GetRequiredService<IOptions<VideoSourceOptions>>().Value.Provider switch
            {
                VideoSourceProvider.Fake => ActivatorUtilities.CreateInstance<FakeVideoSource>(sp),
                _ => sp.GetRequiredService<YoutubeExplodeVideoSource>(),
            }
        );
        services.AddSingleton<IChatClient>(sp => LlmClientFactory.Create(sp.GetRequiredService<IOptions<LlmOptions>>().Value));
        services.AddScoped<RecipeExtractor>();
        services.AddKeyedScoped<IImportPath, VideoImportPath>(ImportKind.Video);
        return services;
    }
}
