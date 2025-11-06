
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Agrovent.Services;
using Microsoft.Extensions.DependencyInjection;
using Xarial.XCad.SolidWorks;
using Xarial.XCad.UI.Commands;

namespace Agrovent
{
    [ComVisible(true)]
    public class AgroventAddin : SwAddInEx
    {
        public override void OnConnect()
        {
            CommandManager.AddCommandGroup<Commands_e>().CommandClick += OnCommandClickExecute;
            // Initialization code here
        }

        private void OnCommandClickExecute(Commands_e command)
        {
            InitDI();

            switch (command)
            {
                case Commands_e.Command1:
                    // Handle Command1
                    break;
                case Commands_e.Command2:
                    // Handle Command2
                    break;
                
                default:
                    break;
            }
        }

        private void InitDI()
        {
            AGR_ServiceContainer.Initialize(services =>
            {
                // Дополнительная регистрация специфичная для нашего Add-in
                services.AddSingleton<AgroventAddin>(this);

                // Можно добавить конфигурацию из файла
                // services.AddSingleton<IConfiguration>(LoadConfiguration());
            });
        }

        public override void OnDisconnect()
        {
            // Cleanup code here
        }
    }


    public enum Commands_e
    {
        Command1,
        Command2
    }

}
