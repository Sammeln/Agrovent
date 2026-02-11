// App.xaml.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using System.Windows;
using AGR_PropManager.ViewModels;
using AGR_PropManager.Views;
using Agrovent.DAL.Entities.Components;
using Agrovent.DAL;

namespace AGR_PropManager
{
    public partial class App : Application
    {
        internal IHost _host;

        public App()
        {
            var builder = Host.CreateApplicationBuilder(); // Создаем Builder

            // Настройка конфигурации (если используете appsettings.json)
            builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            // Настройка сервисов
            builder.Services.AddDbContext<DataContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddTransient<MainWindowViewModel>();
            builder.Services.AddTransient<MainWindow>();

            _host = builder.Build(); // Строим Host
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            await _host.StartAsync();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await _host.StopAsync();
            _host.Dispose();
            base.OnExit(e);
        }
    }
}