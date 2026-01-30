using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Agrovent.DAL;
using Agrovent.DAL.Repositories;
using Agrovent.Services;
using Microsoft.EntityFrameworkCore;
using Agrovent.Infrastructure.Interfaces;
using System.Reflection;
using System.IO;
using Xarial.XCad.Documents;
using Agrovent.ViewModels.TaskPane;
using Agrovent.DAL.Services;
using Agrovent.Infrastructure.Services;
using Agrovent.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Agrovent.ViewModels.Windows;

namespace Agrovent
{
    public static class ServiceConfiguration
    {
        public static IServiceCollection ConfigureServices(this IServiceCollection services)
        {
            // 1. Конфигурация
            var appStr = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var configuration = new ConfigurationBuilder()
                .SetBasePath(appStr)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            services.AddSingleton<IConfiguration>(configuration);

            services.Configure<AGR_FileStorageConfig>(configuration.GetSection("FileStorage")); // Предполагаем, что в appsettings.json есть секция "FileStorage"
            // Регистрация AGR_FileStorageConfig как Singleton (можно и transient/scoped, в зависимости от нужд)
            services.AddSingleton(provider => provider.GetRequiredService<IOptions<AGR_FileStorageConfig>>().Value);

            // 2. Логирование
            services.AddLogging(configure =>
            {
                configure.AddConsole();
                configure.AddDebug();
                configure.SetMinimumLevel(LogLevel.Debug);
            });

            // 3. База данных
            services.AddDbContext<DataContext>(options =>
            {
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
                options.EnableSensitiveDataLogging();
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            });

            // 4. Репозитории
            services.AddScoped<IAGR_ComponentRepository, ComponentRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IAGR_ComponentVersionService, ComponentVersionService>();

            // 5. Сервисы
            services.AddScoped<IAGR_ComponentViewModelFactory, AGR_ComponentViewModelFactory>();
            services.AddScoped<IComponentDataService, ComponentDataService>();
            services.AddScoped<IAGR_CommandService, AGR_CommandService>(provider =>
               new AGR_CommandService(
                   provider.GetRequiredService<ILogger<AGR_CommandService>>(),
                   provider.GetRequiredService<IAGR_ComponentVersionService>(),
                   provider // Передаем IServiceProvider
               ));

            // 6. ViewModels (если нужно)
            services.AddScoped<AGR_TaskPaneViewModel>();
            services.AddTransient<AGR_ComponentRegistryVM>();
            services.AddTransient<AGR_ProjectExplorerVM>();
            // services.AddTransient<AGR_AssemblyComponentVM>();
            // services.AddTransient<AGR_PartComponentVM>();

            return services;
        }
    }
}