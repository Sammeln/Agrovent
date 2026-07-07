using System.IO;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces.Properties;
using Agrovent.Infrastructure.Extensions;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.ViewModels.Properties;
using Xarial.XCad.SolidWorks.Data;
using Xarial.XCad.SolidWorks.Documents;
using Agrovent.Infrastructure.Interfaces;
using System.Drawing;
using Xarial.XCad.SolidWorks;
using Agrovent.ViewModels.Components;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell.Interop;
using Xarial.XCad.Base.Attributes;
using Agrovent.Properties;
using AgroventInfrastructure.Interfaces.Entities.Components;
using AgroventInfrastructure.Entities.Components;
using System.Windows.Input;
using AGR_PropManager.Infrastructure.Commands;
using Agrovent.DAL;
using Agrovent.ViewModels.Windows;
using Agrovent.Views.Windows;
using Microsoft.Extensions.Logging;
using AgroventInfrastructure.Enums;
using Xarial.XCad.Data;
using NPOI.SS.Formula.Functions;
using System.Collections.ObjectModel;
using Agrovent.ViewModels.Windows.Details;
using Agrovent.Views.Windows.Details;
using Xarial.XCad.Base;
using Xarial.XCad.Documents.Extensions;
using Xarial.XCad.Documents;
using System.Windows;
using Agrovent.ViewModels.TaskPane;

namespace Agrovent.ViewModels.Base
{
    public class AGR_BaseComponent : BaseViewModel, IAGR_BaseComponent
    {
        private readonly IAGR_ComponentVersionService _componentVersionService;
        private readonly AGR_TaskPaneViewModel _taskPane;

        #region FIELDS
        internal ISwDocument3D? mDocument;
        internal ISwConfiguration? mConfiguration => mDocument.IsAlive ? mDocument.Configurations.Active : null;
        internal ISwCustomPropertiesCollection? mProperties => mDocument.IsAlive ? mConfiguration.Properties : null;
        #endregion

        #region CTOR

        public AGR_BaseComponent(ISwDocument3D swDocument3D)
        {
            _componentVersionService = AGR_ServiceContainer.GetService<IAGR_ComponentVersionService>();
            _taskPane = AGR_ServiceContainer.GetService<AGR_TaskPaneViewModel>();
            mDocument = swDocument3D;
            ComponentType = mDocument.ComponentType();
        }
        #endregion

        #region PROPS

        public ISwDocument3D SwDocument => mDocument;
        public string Name { get => Path.GetFileNameWithoutExtension(mDocument?.Path); }
        public string ConfigName { get => mConfiguration?.Name ?? ""; }
        public string Extension { get => Path.GetExtension(mDocument?.Path) ?? ""; }
        public string PartNumber
        {
            get => mProperties?.AGR_TryGetProp(AGR_PropertyNames.Partnumber).Value.ToString() ?? "";
            set
            {
                mProperties.AGR_TryGetProp(AGR_PropertyNames.Partnumber).Value = value;
                OnPropertyChanged(nameof(PartNumber));
                PartnumberChanged?.Invoke(this, new EventArgs());
            }
        }
        public string Article
        {
            get
            {
                return AvaArticle != null ? AvaArticle.Article.ToString() : mProperties.AGR_TryGetProp(AGR_PropertyNames.Article).Value.ToString();
            }
            set
            {
                mProperties.AGR_TryGetProp(AGR_PropertyNames.Article).Value = value;
                OnPropertyChanged(nameof(Article));
            }
        }
        public int Version
        {
            get
            {
                var propValue = mProperties?.AGR_TryGetProp(AGR_PropertyNames.Version).Value ?? 0;
                try
                {
                    return Convert.ToInt32(propValue);
                }
                catch (Exception)
                {

                    return 0;
                }
            }
            set => mProperties.AGR_TryGetProp(AGR_PropertyNames.Version).Value = value;
        }
        public int? HashSum
        {
            get
            {
                var value = mProperties.AGR_TryGetProp(AGR_PropertyNames.HashSum).Value;
                if (!string.IsNullOrEmpty(value.ToString()))
                {
                    return Convert.ToInt32(value);
                }
                return 0;
            }

            set
            {
                mProperties.AGR_TryGetProp(AGR_PropertyNames.HashSum).Value = value;
                OnPropertyChanged(nameof(HashSum));
            }
        }

        public int ComponentHash => CalculateComponentHash();
        public bool IsLoaded { get; set; }

        #region Preview
        private byte[]? _Preview;
        public byte[]? Preview
        {
            get
            {
                if (_Preview == null)
                {
                    _Preview = ComputePreviewImageBytes();
                }
                return _Preview;
            }
            // Устанавливать можно только извне, если нужно переопределить
            protected set => _Preview = value;
        }
        #endregion
        public string FilePath => mDocument?.Path ?? "";

        #region Property - IAGR_AvaArticleModel _AvaArticle
        private IAGR_AvaArticleModel _AvaArticle;
        public IAGR_AvaArticleModel AvaArticle
        {
            get => _AvaArticle;
            set
            {
                Set(ref _AvaArticle, value);
            }
        }
        #endregion

        #region Property - ComponentVersion
        private ComponentVersion _ComponentVersion;
        public ComponentVersion ComponentVersion
        {
            get => _ComponentVersion;
            set => Set(ref _ComponentVersion, value);
        }
        #endregion

        #region IsInDatabase
        private AGR_ComponentDatabaseState_e _isInDatabase = AGR_ComponentDatabaseState_e.NotLoaded;
        public AGR_ComponentDatabaseState_e IsInDatabase
        {
            get => _isInDatabase;
            set => Set(ref _isInDatabase, value);
        }

        #endregion 
        public IAGR_PropertiesCollection PropertiesCollection { get; set; }
        public AGR_ComponentType_e ComponentType
        {
            get
            {
                if (!mDocument.IsAlive) return AGR_ComponentType_e.NA;
                return mDocument.ComponentType();
            }
                set
            {
                switch (value)
                {
                    case AGR_ComponentType_e.Assembly:
                    PropertiesCollection = new AGR_BasePropertiesCollection(mDocument);
                    break;
                    case AGR_ComponentType_e.Part:
                    PropertiesCollection = new AGR_PartPropertiesCollection(mDocument);
                    break;
                    case AGR_ComponentType_e.SheetMetallPart:
                    PropertiesCollection = new AGR_SheetPartPropertiesCollection(mDocument);
                    PropertiesCollection.UpdateProperties();
                    OnPropertyChanged(nameof(PropertiesCollection));
                    break;
                    case AGR_ComponentType_e.Purchased:
                    PropertiesCollection?.Properties.Clear();
                    break;
                    case AGR_ComponentType_e.NA:
                    PropertiesCollection = new AGR_BasePropertiesCollection(mDocument);
                    break;
                    default:
                    break;
                }
                OnPropertyChanged(nameof(ComponentType));
            }
        }
        public AGR_AvaType_e AvaType
        {
            get
            {
                if (!mDocument.IsAlive) return AGR_AvaType_e.NA;
                var val = mProperties.AGR_TryGetProp(AGR_PropertyNames.AvaType).Value;
                if (!string.IsNullOrEmpty(val.ToString()))
                {
                    return (AGR_AvaType_e)Convert.ToInt32(val);
                }
                return AGR_AvaType_e.Component;
            }

            set
            {
                mProperties.AGR_TryGetProp(AGR_PropertyNames.AvaType).Value = (int)value;

                if (value == AGR_AvaType_e.Purchased)
                {
                    ComponentType = AGR_ComponentType_e.Purchased;
                }
                else
                {
                    ComponentType = mDocument.ComponentType();
                }
                OnPropertyChanged(nameof(AvaType));
            }
        }

        #region Property - ParentAssemblies
        private ICollection<IAGR_ComponentRegistryItemVM>? _ParentAssemblies = new ObservableCollection<IAGR_ComponentRegistryItemVM>();
        
        public ICollection<IAGR_ComponentRegistryItemVM>? ParentAssemblies
        {
            get => _ParentAssemblies;
            set
            {
                Set(ref _ParentAssemblies, value);
                OnPropertyChanged(nameof(ParentAssembliesCount));
                OnPropertyChanged(nameof(CanUserEdit));
            }
        }
        #endregion
        public int ParentAssembliesCount => ParentAssemblies?.Count ?? 0;
        public bool CanUserEdit => !(ParentAssembliesCount <= 1);

        public bool IsPurchased => ComponentType != AGR_ComponentType_e.Part
                                && ComponentType != AGR_ComponentType_e.SheetMetallPart
                                && ComponentType != AGR_ComponentType_e.Assembly;

        #endregion

        #region METHODS
        public int CalculateComponentHash()
        {
            // Вычисляем хеш на основе важных свойств
            unchecked
            {
                int hash = 17;

                if (this is AGR_PartComponentVM part)
                {
                    //hash = hash + (component.Name?.GetHashCode(StringComparison.Ordinal) ?? 0);

                    string str = string.Empty;
                    double sum = 0d;

                    var swPart = part.SwDocument as ISwPart;

                    var dimensions = swPart.Dimensions.ToList();

                    if (dimensions != null && dimensions?.Count > 0)
                    {
                        foreach (var dim in swPart.Dimensions)
                        {
                            hash += dim.Value.GetHashCode();
                        }
                    }
                    foreach (var feat in swPart.Features)
                    {
                        str += feat.Name;
                    }
                    hash += HashString(str);
                }
                if (this is AGR_AssemblyComponentVM assembly)
                {
                    foreach (var item in assembly.GetChildComponents())
                    {
                        hash += item.Component.CalculateComponentHash();
                    }
                }

                return hash;


                //int hash = 17;
                //hash = hash * 23 + (component.Name?.GetHashCode(StringComparison.Ordinal) ?? 0);
                //hash = hash * 23 + (component.ConfigName?.GetHashCode(StringComparison.Ordinal) ?? 0);
                //hash = hash * 23 + (component.PartNumber?.GetHashCode(StringComparison.Ordinal) ?? 0);
                //hash = hash * 23 + component.ComponentType.GetHashCode();
                //hash = hash * 23 + component.AvaType.GetHashCode();
                //return hash;
            }
        }
        protected virtual byte[]? ComputePreviewImageBytes()
        {
            try
            {
                var app = AGR_ServiceContainer.GetService<ISwApplication>();

                string? filePath = FilePath;
                string activeConfig = ConfigName ?? "Default"; // Используем "Default", если ConfigName null

                if (string.IsNullOrEmpty(filePath))
                {
                    //_logger?.LogWarning("ComputePreviewImageBytes: FilePath is null or empty.");
                    return null;
                }

                // Вызов GetPreviewBitmap из UI-потока
                object? com = app.Sw.GetPreviewBitmap(filePath, activeConfig);
                if (com == null)
                {
                    return Resources.NonePreview;
                    //_logger?.LogWarning($"ComputePreviewImageBytes: GetPreviewBitmap returned null for {filePath}, config: {activeConfig}");
                    //return null;
                }

                stdole.StdPicture? pic = com as stdole.StdPicture;
                if (pic == null)
                {
                    //_logger?.LogWarning($"ComputePreviewImageBytes: GetPreviewBitmap returned unexpected type: {com.GetType()}");
                    return null;
                }

                var bmp = Bitmap.FromHbitmap((IntPtr)pic.Handle);
                // Освобождаем handle, так как FromHbitmap создает копию
                // Не обязательно вызывать DeleteObject(pic.Handle) здесь, так как FromHbitmap уже скопировал данные


                ImageConverter converter = new ImageConverter();
                return (byte[])converter.ConvertTo(bmp, typeof(byte[]));

                //using var ms = new MemoryStream();
                //bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png); // Или Jpeg, в зависимости от ваших предпочтений
                //bmp.Dispose(); // Уничтожаем Bitmap
                //return ms.ToArray();
            }
            catch (COMException comEx)
            {
                //_logger?.LogError(comEx, $"COM ошибка при получении превью для {FilePath}: {comEx.Message}");
                return null; // Возвращаем null в случае ошибки
            }
            catch (Exception ex)
            {
                //_logger?.LogError(ex, $"Ошибка при получении превью для {FilePath}: {ex.Message}");
                return null; // Возвращаем null в случае ошибки
            }
        }
        public void PrecomputePreview()
        {
            // Просто обращаемся к свойству, чтобы оно вычислилось в текущем потоке (ожидается UI-поток)
            _ = Preview;
        }
        public int HashString(string text)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in text)
                {
                    hash = hash * 31 + c;
                }
                return hash;
            }
        }

        #endregion

        #region COMMANDS

        #region SelectAvaArticleCommand
        private ICommand _SelectAvaArticleCommand;
        public ICommand SelectAvaArticleCommand => _SelectAvaArticleCommand
            ??= new RelayCommand(OnSelectAvaArticleCommandExecuted, CanSelectAvaArticleCommandExecute);
        private bool CanSelectAvaArticleCommandExecute(object p) => true;
        private void OnSelectAvaArticleCommandExecuted(object p)
        {
            try
            {
                //_logger?.LogDebug("Открытие окна выбора AvaArticle для компонента {PartNumber}", PartNumber);

                // Получаем IServiceProvider из вашего контейнера (предполагаем, что он доступен)
                // Это может быть AGR_ServiceContainer или другой способ получения провайдера.
                // Пример (может отличаться в вашем проекте):

                // Получаем нужные сервисы для VM
                var dataContext = AGR_ServiceContainer.GetService<DataContext>();
                var logger = AGR_ServiceContainer.GetService<ILogger<AGR_SelectAvaArticleVM>>();

                // Создаем ViewModel
                var selectVm = new AGR_SelectAvaArticleVM(dataContext, logger);
                selectVm.SelectedAvaType = AGR_AvaTypeNames.AllTypes;
                selectVm.SearchText = PartNumber;

                // Создаем View и устанавливаем DataContext
                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm };

                selectView.ShowActivated = true;
                // Открываем окно модально
                selectView.ShowDialog();

                // Если окно закрыто с результатом OK и элемент выбран
                if (selectVm.IsDialogResultAccepted == true && selectVm.SelectedArticle != null)
                {
                    // Присваиваем выбранный AvaArticleModel в BaseMaterial.AvaModel
                    AvaArticle = selectVm.SelectedArticle;
                    //_logger?.LogInformation("Выбран AvaArticle {Article} для компонента {PartNumber}", selectVm.SelectedArticle.Article, PartNumber);

                    // Обновляем свойства, если это влияет на них (например, BaseMaterialCount)
                    //Task.Run(async () => await UpdatePropertiesAsync()).ConfigureAwait(false); // Вызов асинхронного метода
                }
                else
                {
                    //_logger?.LogDebug("Окно выбора AvaArticle закрыто без выбора.");
                }
            }
            catch (Exception ex)
            {
                //_logger?.LogError(ex, "Ошибка при открытии окна выбора AvaArticle для компонента {PartNumber}", PartNumber);
            }
        }
        #endregion

        #region OpenComponentCommand
        private ICommand _OpenComponentCommand;
        public ICommand OpenComponentCommand => _OpenComponentCommand
            ??= new RelayCommand<AGR_ComponentRegistryItemVM>(OnOpenComponentCommandExecuted, CanOpenComponentCommandExecute);
        private bool CanOpenComponentCommandExecute(AGR_ComponentRegistryItemVM p) => p != null && !string.IsNullOrEmpty(p.StoragePath) && File.Exists(p.StoragePath);
        private void OnOpenComponentCommandExecuted(AGR_ComponentRegistryItemVM selectedItem)
        {
            if (selectedItem == null || string.IsNullOrEmpty(selectedItem.StoragePath)) return;

            var filePath = selectedItem.StoragePath;

            if (!File.Exists(filePath))
            {
                //_logger.LogWarning($"Команда 'Открыть': Файл не существует: {filePath}");
                return;
            }

            try
            {
                // Получаем ISwApplication
                var swApp = AGR_ServiceContainer.GetService<ISwApplication>();
                if (swApp == null)
                {
                    //_logger.LogError("Команда 'Открыть': Не удалось получить ISwApplication.");
                    return;
                }

                // Проверяем, открыт ли документ
                var openDoc = swApp.Documents.FirstOrDefault(x => x.Path == filePath);
                if (openDoc != null)
                {
                    // Документ уже открыт, делаем его активным
                    swApp.Documents.Active = openDoc as ISwDocument;

                    //_logger.LogDebug($"Команда 'Открыть': Документ уже открыт, активирован: {filePath}");
                }
                else
                {
                    // Документ не открыт, открываем
                    var newDoc = swApp.Documents.Open(filePath, Xarial.XCad.Documents.Enums.DocumentState_e.ReadOnly);// PreCreateFromPath(filePath);
                    //newDoc.Commit(CancellationToken.None);
                    //_logger.LogDebug($"Команда 'Открыть': Документ открыт: {filePath}");
                }
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, $"Команда 'Открыть': Ошибка при открытии файла {filePath}");
            }
        }
        #endregion

        #region AddToAssemblyCommand
        private ICommand _AddToAssemblyCommand;
        public ICommand AddToAssemblyCommand => _AddToAssemblyCommand
            ??= new RelayCommand<AGR_ComponentRegistryItemVM>(OnAddToAssemblyCommandExecuted, CanAddToAssemblyCommandExecute);
        private bool CanAddToAssemblyCommandExecute(AGR_ComponentRegistryItemVM p)
        {
            if (p == null || string.IsNullOrEmpty(p.StoragePath) || !File.Exists(p.StoragePath)) return false;

            // Проверяем, активен ли сборочный документ
            var swApp = AGR_ServiceContainer.GetService<ISwApplication>();
            if (swApp == null) return false;

            var activeDoc = swApp.Documents.Active;
            return activeDoc is ISwAssembly;
        }
        private void OnAddToAssemblyCommandExecuted(AGR_ComponentRegistryItemVM selectedItem)
        {
            if (selectedItem == null || string.IsNullOrEmpty(selectedItem.StoragePath)) return;

            var filePath = selectedItem.StoragePath;
            var drawPath = Path.ChangeExtension(filePath, ".slddrw");
            var fileTitle = Path.GetFileNameWithoutExtension(filePath);

            if (!File.Exists(filePath))
            {
                //_logger.LogWarning($"Команда 'Добавить в сборку': Файл не существует: {filePath}");
                return;
            }

            try
            {
                var swApp = AGR_ServiceContainer.GetService<ISwApplication>();
                if (swApp == null)
                {
                    //_logger.LogError("Команда 'Добавить в сборку': Не удалось получить ISwApplication.");
                    return;
                }

                // Проверяем, активен ли сборочный документ
                var activeDoc = swApp.Documents.Active;
                if (!(activeDoc is ISwAssembly swAssembly))
                {
                    //_logger.LogWarning("Команда 'Добавить в сборку': Активный документ не является сборкой.");
                    return;
                }
                //Папка текущей сборки
                var assemblyFolderPath = Path.GetDirectoryName(swAssembly.Path);
                if (string.IsNullOrEmpty(assemblyFolderPath))
                {
                    assemblyFolderPath = @"D:\Работа";
                }
                //Путь к файлу компонента для копирования из хранилища
                var destFilePath = Path.Combine(assemblyFolderPath, Path.GetFileName(filePath));
                var destDrawPath = Path.ChangeExtension(destFilePath, ".slddrw");
                // Проверяем, открыт ли документ компонента
                IXDocument3D? compDoc = swApp.Documents.FirstOrDefault(x => x.Title == fileTitle) as IXDocument3D;
                if (compDoc == null)
                {
                    //Проверяем есть ли файл в рабочей папке, если есть убираем для чтения
                    if (File.Exists(destFilePath))
                    {
                        File.SetAttributes(destFilePath, FileAttributes.Normal);
                    }

                    //Проверяем есть ли файл чертежа в рабочей папке, если есть убираем для чтения
                    if (File.Exists(destDrawPath))
                    {
                        File.SetAttributes(destDrawPath, FileAttributes.Normal);
                    }

                    // Нельзя открывать документы из центрального хранилища, копируем файл в папку с активной сборкой
                    File.Copy(filePath, destFilePath, true);
                    //Если есть чертеж копируемого  компонента, то копируем его в папку тоже
                    if (File.Exists(drawPath))
                    {
                        File.Copy(drawPath, destDrawPath, true);
                    }


                    // Документ не открыт, открываем его
                    compDoc = swApp.Documents.PreCreateFromPath(destFilePath) as IXDocument3D;
                    if (compDoc == null)
                    {
                        //_logger.LogError($"Команда 'Добавить в сборку': Не удалось открыть документ компонента: {destFilePath}");
                        return;
                    }
                    //compDoc.Commit(CancellationToken.None);
                    //_logger.LogDebug($"Команда 'Добавить в сборку': Документ компонента открыт: {destFilePath}");
                }

                // Создаем шаблон компонента
                var xComp = swAssembly.Configurations.Active.Components.PreCreate<IXComponent>();
                if (xComp == null)
                {
                    //_logger.LogError($"Команда 'Добавить в сборку': Не удалось создать шаблон компонента для {destFilePath}");
                    return;
                }

                // Устанавливаем ссылку на документ
                xComp.ReferencedDocument = compDoc;

                // Добавляем в сборку
                swAssembly.Configurations.Active.Components.Add(xComp);
                //_logger.LogDebug($"Команда 'Добавить в сборку': Компонент добавлен в сборку: {filePath}");

                // Выделяем компонент
                xComp.Select(false);

                // Запускаем внутреннюю команду для перемещения компонента (Move Component)
                // 1993 - это ID команды "Move Component"
                swApp.Sw.RunCommand(1993, "");


            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, $"Команда 'Добавить в сборку': Ошибка при добавлении файла {filePath} в сборку.");
            }
        }
        #endregion

        #region ShowDetailsCommand
        private ICommand _ShowDetailsCommand;
        public ICommand ShowDetailsCommand => _ShowDetailsCommand
            ??= new RelayCommand<AGR_ComponentRegistryItemVM>(OnShowDetailsCommandExecuted, CanShowDetailsCommandExecute);
        private bool CanShowDetailsCommandExecute(AGR_ComponentRegistryItemVM p) => p != null; // Всегда доступна, если элемент выбран
        private void OnShowDetailsCommandExecuted(AGR_ComponentRegistryItemVM selectedItem)
        {
            if (selectedItem == null) return;

            // Создаем и открываем окно с деталями

            var unitOfWork = AGR_ServiceContainer.GetService<IUnitOfWork>();

            var detailsVM = new AGR_ComponentDetailsVM(selectedItem, unitOfWork); // Предполагаем, что ViewModel будет создана
            var detailsView = new AGR_ComponentDetailsView { DataContext = detailsVM };

            var window = new Window
            {
                Title = $"Детали: {selectedItem.Name} ({selectedItem.PartNumber})",
                Content = detailsView,
                Width = 800,
                Height = 600,
                ResizeMode = ResizeMode.CanResizeWithGrip
            };

            window.ShowDialog(); // Открываем модально
        }
        #endregion





        #region SetNewPartnumberCommand
        private ICommand _SetNewPartnumberCommand;
        public ICommand SetNewPartnumberCommand => _SetNewPartnumberCommand
            ??= new RelayCommand(OnSetNewPartnumberCommandExecuted, CanSetNewPartnumberCommandExecute);
        private bool CanSetNewPartnumberCommandExecute(object p) => true;
        private async void OnSetNewPartnumberCommandExecuted(object p)
        {
            await _componentVersionService.CreateNewComponent(this);
        }
        #endregion 

        #endregion
       
        public event EventHandler? PartnumberChanged;
    }


}
