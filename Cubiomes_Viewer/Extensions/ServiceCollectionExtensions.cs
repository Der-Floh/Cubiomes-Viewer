using System.Diagnostics.CodeAnalysis;

using Avalonia.Controls;

using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers a view and the view model it shows, so that the view locator builds this view for that view model.</summary>
        public IServiceCollection AddView<TViewModel, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TView>()
            where TViewModel : IViewModelBase
            where TView : Control
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddTransient<TView>();
            services.AddSingleton(new ViewRegistration(typeof(TViewModel), provider => provider.GetRequiredService<TView>()));
            return services;
        }
    }
}
