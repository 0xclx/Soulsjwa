using Microsoft.AspNetCore.OutputCaching;

namespace Soulsjwa.Api.Features.TwitchExtension.Services;

public static class TwitchExtensionPushRegistration
{
    /// <summary>
    /// Wraps whatever <see cref="IOutputCacheStore"/> is registered (the
    /// in-memory default today, a shared store later) in
    /// <see cref="ScoreboardChangeNotifyingCacheStore"/>, so every scoreboard
    /// eviction also reaches the push notifier. Call after <c>AddOutputCache</c>
    /// and after the notifier is registered.
    /// </summary>
    public static IServiceCollection DecorateOutputCacheStoreForPush(this IServiceCollection services)
    {
        var storeDescriptor = services.Single(d => d.ServiceType == typeof(IOutputCacheStore));
        services.Remove(storeDescriptor);
        services.AddSingleton<IOutputCacheStore>(sp => new ScoreboardChangeNotifyingCacheStore(
            CreateInner(sp, storeDescriptor),
            sp.GetRequiredService<ITwitchExtensionPushNotifier>()));
        return services;
    }

    private static IOutputCacheStore CreateInner(IServiceProvider sp, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is IOutputCacheStore instance)
            return instance;
        if (descriptor.ImplementationFactory is { } factory)
            return (IOutputCacheStore)factory(sp);
        return (IOutputCacheStore)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!);
    }
}
