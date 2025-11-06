using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agrovent.Services
{
    public static class ServiceContainer
    {
        private static IServiceProvider? _serviceProvider;
        private static readonly object _lock = new object();

        /// <summary>
        /// Получить сервис из контейнера
        /// </summary>
        public static T GetService<T>() where T : notnull
        {
            EnsureInitialized();
            return _serviceProvider.GetRequiredService<T>();
        }

        /// <summary>
        /// Получить сервис из контейнера (может быть null)
        /// </summary>
        public static T? GetServiceOrNull<T>() where T : class
        {
            EnsureInitialized();
            return _serviceProvider.GetService<T>();
        }

        /// <summary>
        /// Инициализация контейнера (вызывается один раз)
        /// </summary>
        public static void Initialize(Action<IServiceCollection>? configureServices = null)
        {
            lock (_lock)
            {
                if (_serviceProvider != null)
                    return; // Уже инициализирован

                var services = new ServiceCollection();

                // Регистрация базовых сервисов
                RegisterCoreServices(services);

                // Дополнительная конфигурация от вызывающего кода
                configureServices?.Invoke(services);

                _serviceProvider = services.BuildServiceProvider();
            }
        }

        /// <summary>
        /// Освобождение ресурсов (вызывается при закрытии SolidWorks)
        /// </summary>
        public static void Dispose()
        {
            lock (_lock)
            {
                if (_serviceProvider is IDisposable disposable)
                    disposable.Dispose();
                _serviceProvider = null;
            }
        }

        private static void EnsureInitialized()
        {
            if (_serviceProvider == null)
                Initialize(); // Инициализация с настройками по умолчанию
        }

        private static void RegisterCoreServices(IServiceCollection services)
        {
            // Логирование
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            //// Основные сервисы приложения
            //services.AddSingleton<ICommandService, CommandService>();
            //services.AddSingleton<IComponentRepoService, ComponentRepoService>();
            //services.AddTransient<IDataContext, DataContext>();

            //// ViewModels (если нужны в DI)
            //services.AddTransient<ComponentsSpecificationViewModel>();
            //services.AddTransient<TaskPaneVM>();
        }
    }
}
