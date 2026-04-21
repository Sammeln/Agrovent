// File: ViewModels/Windows/SaveProgressVM.cs
using Agrovent.ViewModels.Base;
using Agrovent.Infrastructure.Commands; // Для RelayCommand
using Microsoft.Extensions.Logging; // Для ILogger (опционально)
using System;
using System.Collections.ObjectModel;
using System.IO; // Для SaveFileDialog
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Xarial.XCad.SolidWorks; // Для SaveFileDialog

namespace Agrovent.ViewModels.Windows
{
    public class AGR_SaveProgressVM : BaseViewModel, IAGR_SaveProgressVM
    {
        private readonly ILogger<AGR_SaveProgressVM>? _logger; // Опционально
        private readonly string SaveProductName;
        public AGR_SaveProgressVM(ILogger<AGR_SaveProgressVM>? logger = null)
        {
            _logger = logger;

            var _app = AGR_ServiceContainer.GetService<ISwAddInEx>();
            SaveProductName = _app?.Application.Documents.Active?.Title ?? "";


            LogMessages = new ObservableCollection<string>();
        }

        #region Properties

        #region LogMessages
        private ObservableCollection<string> _logMessages;
        public ObservableCollection<string> LogMessages
        {
            get => _logMessages;
            set => Set(ref _logMessages, value);
        }
        #endregion

        #region IsFinished
        private bool _isFinished;
        public bool IsFinished
        {
            get => _isFinished;
            set
            {
                if (Set(ref _isFinished, value))
                {
                    // Уведомляем команды о возможном изменении CanExecute
                    //((RelayCommand)CloseCommand).NotifyCanExecuteChanged();
                    //((RelayCommand)SaveLogCommand).NotifyCanExecuteChanged();
                }
            }
        }
        #endregion

        #endregion

        #region Commands

        #region CloseCommand
        private ICommand _CloseCommand;
        public ICommand CloseCommand => _CloseCommand
            ??= new RelayCommand(OnCloseCommandExecuted, CanCloseCommandExecute);
        private bool CanCloseCommandExecute(object p) => true; // Всегда можно закрыть
        private void OnCloseCommandExecuted(object p)
        {
            var view = p as Window;
            if (view != null)
            {
                view.Close();
                return;
            }
            // Закрытие окна будет обработано в View
            //CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region SaveLogCommand
        private ICommand _SaveLogCommand;
        public ICommand SaveLogCommand => _SaveLogCommand
            ??= new RelayCommand(OnSaveLogCommandExecuted, CanSaveLogCommandExecute);
        private bool CanSaveLogCommandExecute(object p) => IsFinished; // Доступна только после завершения
        private void OnSaveLogCommandExecuted(object p)
        {
            var dialog = new SaveFileDialog
            {
                FileName = $"SaveLog {SaveProductName}.txt",
                DefaultExt = ".txt",
                Filter = "Text documents (.txt)|*.txt|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllLines(dialog.FileName, LogMessages);
                    _logger?.LogInformation($"Лог сохранен в файл: {dialog.FileName}");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Ошибка при сохранении лога в файл.");
                    MessageBox.Show($"Ошибка при сохранении лога: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        #endregion

        #endregion

        // Событие для запроса закрытия окна
        public event EventHandler? CloseRequested;

        // Метод для добавления сообщения в лог
        public void AddLogMessage(string message)
        {
            // Добавляем напрямую, так как вызывается из UI-потока
            LogMessages.Add(message);
            _logger?.LogDebug(message); // Также логируем через ILogger
        }

        // Метод для завершения процесса (вызывается извне после сохранения)
        public void SetFinished()
        {
            IsFinished = true;
        }
    }
}