
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Text;
using Agrovent.Services;
using Agrovent.ViewModels;
using Agrovent.Views.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xarial.XCad.SolidWorks;
using Xarial.XCad.UI.Commands;

namespace Agrovent
{
    [ComVisible(true)]
    public class AgroventAddin : SwAddInEx
    {
        #region DI Services
        private ILogger<AgroventAddin> _logger;
        #endregion

        #region TaskPaneVM

        TaskPaneVM _TaskPaneMV;

        #endregion

        public override void OnConnect()
        {
            //Инициализация контейнера сервисов
            InitDI();

            //Получение сервисов из контейнера
            _logger = AGR_ServiceContainer.GetService<ILogger<AgroventAddin>>();

            //решение проблемы с Behaviour
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            //Инициализация группы команд в солиде
            CommandManager.AddCommandGroup<AGR_Commands_e>().CommandClick += OnCommandClickExecute;

            //инициализация модели представления TaskPane
            InitTaskPane();

            _logger.LogDebug("AddIn успешно добавлено.");

        }



        public override void OnDisconnect()
        {
            // Cleanup code here
        }

        private void OnCommandClickExecute(AGR_Commands_e command)
        {
            switch (command)
            {
                case AGR_Commands_e.Command1:
                    // Handle Command1
                    break;
                case AGR_Commands_e.Command2:
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
        private void InitTaskPane()
        {
            var _taskPaneVM = AGR_ServiceContainer.GetService<TaskPaneVM>();
            var taskPaneView = this.CreateTaskPaneWpf<TaskPaneView>();

            taskPaneView.Control.DataContext = _taskPaneVM;
            taskPaneView.IsActive = true;
            taskPaneView.Control.Focus();

            _TaskPaneMV = _taskPaneVM;
        }
    }


    public enum AGR_Commands_e
    {
        Command1,
        Command2
    }

}
