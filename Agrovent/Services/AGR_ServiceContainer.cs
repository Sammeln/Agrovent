// AGR_ServiceContainer.cs в основном проекте
using Microsoft.Extensions.DependencyInjection;

namespace Agrovent
{
    public static class AGR_ServiceContainer
    {
        private static IServiceProvider _serviceProvider;
        private static IServiceCollection _services;

        public static void Initialize(Action<IServiceCollection> configureServices = null)
        {
            _services = new ServiceCollection();

            // Базовая конфигурация
            _services.ConfigureServices();

            // Дополнительная конфигурация от вызывающего кода
            configureServices?.Invoke(_services);

            _serviceProvider = _services.BuildServiceProvider();
        }

        public static T GetService<T>() where T : class
        {
            if (_serviceProvider == null)
                throw new InvalidOperationException("ServiceProvider не инициализирован. Вызовите Initialize() перед использованием.");

            return _serviceProvider.GetService<T>();
        }

        public static object GetService(Type serviceType)
        {
            if (_serviceProvider == null)
                throw new InvalidOperationException("ServiceProvider не инициализирован. Вызовите Initialize() перед использованием.");

            return _serviceProvider.GetService(serviceType);
        }

        public static IServiceProvider GetServiceProvider() => _serviceProvider;

        public static IServiceScope CreateScope() => _serviceProvider.CreateScope();
    }
}