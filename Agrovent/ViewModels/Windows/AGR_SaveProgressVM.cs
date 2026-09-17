// File: ViewModels/Windows/SaveProgressVM.cs
using System.Collections.ObjectModel;
using System.IO; // Для SaveFileDialog
using System.Windows;
using System.Windows.Input;
using Agrovent.Infrastructure.Commands; // Для RelayCommand
using Agrovent.ViewModels.Base;
using AgroventInfrastructure.Interfaces; // Для SaveFileDialog
using Microsoft.Extensions.Logging; // Для ILogger (опционально)
using Microsoft.Win32;
using Xarial.XCad.SolidWorks;

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
        public AGR_SaveProgressVM()
        {
                
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

        #region CancelationToken

        /// <summary>
        /// Свойство на отмену прогресса по не обходимости
        /// </summary>
        private CancellationTokenSource _CancelationToken;
        public CancellationTokenSource CancelationToken
        {
            get => _CancelationToken;
            set => Set(ref _CancelationToken, value);
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
            // Пока процесс не завершён - кнопка работает ТОЛЬКО как "Отмена".
            // Важно: проверка "!IsFinished" должна срабатывать при КАЖДОМ клике,
            // а не только при первом. Раньше повторный клик (когда отмена уже
            // запрошена, но IsFinished ещё false) проваливался в view.Close() -
            // окно закрывалось, а фоновая операция как ни в чём не бывало
            // продолжала работать, потому что она никак не привязана к окну.
            if (!IsFinished)
            {
                if (CancelationToken != null && !CancelationToken.IsCancellationRequested)
                {
                    CancelationToken.Cancel();
                    AddLogMessage("⚠️ Отмена операции пользователем...");
                }
                return; // окно закроется только когда IsFinished == true
            }

            var view = p as Window;
            view?.Close();
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