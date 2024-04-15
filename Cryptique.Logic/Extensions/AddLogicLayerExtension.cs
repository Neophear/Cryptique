using Microsoft.Extensions.DependencyInjection;

namespace Cryptique.Logic.Extensions;

public static class AddLogicLayerExtension
{
    public static IServiceCollection AddLogicLayer(this IServiceCollection services)
    {
        services.AddSingleton<IMessageService, MessageService>();
        return services;
    }
}
