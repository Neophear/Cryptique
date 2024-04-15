using Cryptique.Data.TableStorage;
using Microsoft.Extensions.DependencyInjection;

namespace Cryptique.Data.Extensions;

public static class AddDataLayerExtension
{
    public static IServiceCollection AddDataLayer(this IServiceCollection services)
    {
        services.AddSingleton<IMessageRepository, MessageRepository>();
        return services;
    }
}
