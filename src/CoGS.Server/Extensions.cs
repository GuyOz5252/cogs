using CoGS.Core;
using CoGS.Core.Pipeline;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CoGS.Server;

public static class Extensions
{
    extension(WebApplicationBuilder builder)
    {
        public ComponentRegistration AddComponent<TComponent>(string componentName)
            where TComponent : ProcessorComponentBase
        {
            builder.Services.AddSingleton<TComponent>();
            return new ComponentRegistration
            {
                ComponentType = typeof(TComponent),
                Name = componentName,
            };
        }
    }
}
