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

            // 6. ViewModels (если нужно)
            // services.AddTransient<AGR_AssemblyComponentVM>();
            // services.AddTransient<AGR_PartComponentVM>();

            return services;
        }
    }
}