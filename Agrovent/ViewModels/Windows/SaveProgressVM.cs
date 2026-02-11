// File: ViewModels/Windows/SaveProgressVM.cs
using Agrovent.ViewModels.Base;
using Agrovent.Infrastructure.Commands; // Для RelayCommand
using Microsoft.Extensions.Logging; // Для ILogger (опционально)
using System;
using System.Collections.ObjectModel;
using System.IO; // Для SaveFileDialog
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32; // Для SaveFileDialog

namespace Agrovent.ViewModels.Windows
{
    public class SaveProgressVM : BaseViewModel
    {
        private readonly ILogger<SaveProgressVM>? _logger; // Опционально
        private readonly object _lock = new object(); // Для потокобезопасности коллекции лога
        private readonly SynchronizationContext? _uiContext; // Сохраняем SynchronizationContext UI-потока

        public SaveProgressVM(ILogger<SaveProgressVM>? logger = null, SynchronizationContext? uiContext = null)
        {
            _logger = logger;
            _uiContext = uiContext; // Получаем SynchronizationContext из UI-потока

            LogMessages = new ObservableCollection<string>();
            CloseCommand = new RelayCommand(OnCloseCommandExecuted, CanCloseCommandExecute);
            SaveLogCommand = new RelayCommand(OnSaveLogCommandExecuted, CanSaveLogCommandExecute);
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
        public ICommand CloseCommand { get; }
        private bool CanCloseCommandExecute(object p) => true; // Всегда можно закрыть
        private void OnCloseCommandExecuted(object p)
        {
            // Закрытие окна будет обработано в View
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region SaveLogCommand
        public ICommand SaveLogCommand { get; }
        private bool CanSaveLogCommandExecute(object p) => IsFinished; // Доступна только после завершения
        private void OnSaveLogCommandExecuted(object p)
        {
            var dialog = new SaveFileDialog
            {
                FileName = "SaveLog.txt",
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

        // Метод для добавления сообщения в лог (потокобезопасный)
        public void AddLogMessage(string message)
        {
            lock (_lock)
            {
                // Используем сохранённый SynchronizationContext для вызова в UI-потоке
                if (_uiContext != null)
                {
                    _uiContext.Send(_ => LogMessages.Add(message), null); // Send - синхронный вызов в UI-потоке
                    // Или Post - асинхронный вызов в UI-потоке
                    // _uiContext.Post(_ => LogMessages.Add(message), null);
                }
                else
                {
                    // Если SynchronizationContext не установлен, пытаемся использовать Dispatcher текущего потока
                    // (работает, если вызов происходит из UI-потока)
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => LogMessages.Add(message));
                }
            }
            _logger?.LogDebug(message); // Также логируем через ILogger
        }

        // Метод для завершения процесса (вызывается извне после сохранения)
        public void SetFinished()
        {
            IsFinished = true;
        }
    }
}