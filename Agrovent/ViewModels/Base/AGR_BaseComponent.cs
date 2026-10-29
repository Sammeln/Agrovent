using System.IO;
using Agrovent.Infrastructure.Extensions;
using Agrovent.ViewModels.Properties;
using Xarial.XCad.SolidWorks.Data;
using Xarial.XCad.SolidWorks.Documents;
using Agrovent.Infrastructure.Interfaces;
using System.Drawing;
using Xarial.XCad.SolidWorks;
using Agrovent.ViewModels.Components;
using System.Runtime.InteropServices;
using Agrovent.Properties;
using AgroventInfrastructure.Entities.Components;
using System.Windows.Input;
using Agrovent.DAL;
using Agrovent.ViewModels.Windows;
using Agrovent.Views.Windows;
using Microsoft.Extensions.Logging;
using AgroventInfrastructure.Enums;
using System.Collections.ObjectModel;
using Xarial.XCad.Base;
using Xarial.XCad.Documents.Extensions;
using Xarial.XCad.Documents;
using Agrovent.ViewModels.TaskPane;
using Agrovent.ViewModels.Specification;
using Microsoft.Extensions.DependencyInjection;
using Agrovent.Infrastructure.Commands;
using AgroventInfrastructure.Interfaces.Components.Base;
using AgroventInfrastructure.Interfaces.Properties;
using AgroventInfrastructure.Interfaces;
using AgroventInfrastructure.Interfaces.Components;

namespace Agrovent.ViewModels.Base
{
    public class AGR_BaseComponent : BaseViewModel, IAGR_BaseComponent
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AGR_TaskPaneViewModel _taskPane;

        #region FIELDS

        // mDocument может обнулиться после закрытия файла — весь код ниже
        // рассчитан на то, что после этого VM продолжает жить на кэшированных данных.
        internal ISwDocument3D? mDocument;

        // ВНИМАНИЕ: эти two свойства больше не используются в геттерах данных (PartNumber, Article, ...).
        // Они оставлены только для внутренних методов, которым реально нужен живой документ
        // прямо здесь и сейчас (например, RefreshFromDocument, ComputePreviewImageBytes).
        // Обращение к ним всегда должно быть обёрнуто в try/catch(COMException) и предварённой проверкой IsAlive,
        // т.к. между проверкой IsAlive и следующим обращением к COM документ теоретически может закрыться.
        internal ISwConfiguration? mConfiguration => (mDocument != null && mDocument.IsAlive) ? mDocument.Configurations.Active : null;
        internal ISwCustomPropertiesCollection? mProperties => mConfiguration?.Properties;

        #endregion

        #region IsAlive

        // Отражает текущее состояние связи VM с живым SW-документом.
        // false означает: документ закрыт, все данные ниже — последнее известное
        // состояние из кэша, запись в файл больше не производится.
        private bool _isAlive = true;
        public bool IsAlive
        {
            get => _isAlive;
            private set => Set(ref _isAlive, value);
        }

        #endregion

        #region CACHE (единственный источник данных для биндингов)

        private string _partNumber = "";
        private string _article = "";
        private int _version;
        private Guid? _storageGuid;
        private AGR_AvaType_e _avaType = AGR_AvaType_e.Component;
        private AGR_ComponentType_e _componentType = AGR_ComponentType_e.NA;
        private string _configName = "";

        #endregion

        #region CTOR

        public AGR_BaseComponent(ISwDocument3D swDocument3D)
        {
            _scopeFactory = AGR_ServiceContainer.GetService<IServiceScopeFactory>();
            _taskPane = AGR_ServiceContainer.GetService<AGR_TaskPaneViewModel>();
            mDocument = swDocument3D;

            // Единственная подписка, которая гарантированно переводит VM
            // в "безопасный" режим при закрытии документа, независимо от того,
            // успел ли отработать AGR_DocumentHandler.
            mDocument.Destroyed += OnDocumentDestroyed;

            // Инициализируем ComponentType через кэширующий сеттер (создаёт PropertiesCollection)
            RefreshFromDocument();
        }

        #endregion

        #region SYNC WITH DOCUMENT

        private void OnDocumentDestroyed(IXDocument doc)
        {
            IsAlive = false;
            mDocument = null;
        }

        /// <summary>
        /// Единая точка чтения данных из живого SW-документа в кэш ViewModel.
        /// Вызывается явно: в конструкторе, и по требованию — например, после
        /// команды "Обновить свойства файлов", когда свойства могли измениться извне.
        /// Биндинги WPF никогда не читают COM напрямую — только это кэш.
        /// </summary>
        public void RefreshFromDocument()
        {
            if (mDocument == null || !mDocument.IsAlive)
            {
                IsAlive = false;
                return;
            }

            try
            {
                var props = mDocument.Configurations.Active.Properties;

                // «Обозначение» - Custom файла (до миграции файлов - активная конфигурация), свойство не создаётся
                _partNumber = mDocument.AGR_ReadPartNumber();

                _article = AvaArticle != null
                    ? AvaArticle.Article.ToString()
                    : props.AGR_TryGetProp(AGR_PropertyNames.Article)?.Value?.ToString() ?? "";

                // Версия и папка в хранилище - из маркера (свойство «Версия» больше не пишется). Нет маркера -
                // версия 0 (не сохранён), GUID не трогаем: его могла проставить загрузка из БД.
                ReadMarker();
                var avaTypeRaw = props.AGR_TryGetProp(AGR_PropertyNames.AvaType)?.Value?.ToString();
                _avaType = string.IsNullOrEmpty(avaTypeRaw)
                    ? AGR_AvaType_e.Component
                    : (AGR_AvaType_e)TryParseInt(avaTypeRaw);

                _configName = mDocument.Configurations.Active?.Name ?? "";

                _componentType = _avaType == AGR_AvaType_e.Purchased
                    ? AGR_ComponentType_e.Purchased
                    : mDocument.ComponentType();
            }
            catch (COMException)
            {
                // Документ умер между проверкой IsAlive и обращением к COM.
                // Оставляем последнее известное состояние в кэше как есть.
                IsAlive = false;
                return;
            }

            RebuildPropertiesCollection(_componentType);

            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(ConfigName));
            OnPropertyChanged(nameof(Extension));
            OnPropertyChanged(nameof(PartNumber));
            OnPropertyChanged(nameof(Article));
            OnPropertyChanged(nameof(Version));
            OnPropertyChanged(nameof(StorageGuid));
            OnPropertyChanged(nameof(AvaType));
            OnPropertyChanged(nameof(ComponentType));
            OnPropertyChanged(nameof(IsPurchased));
        }

        /// <summary>
        /// Best-effort запись значения свойства в файл. Если документ уже закрыт —
        /// молча ничего не делает: значение уже применено в кэш, UI консистентен,
        /// а запись в реальный файл в этой ситуации и не должна происходить.
        /// </summary>
        private void TryWriteProperty(string propName, object value)
        {
            if (mDocument == null || !mDocument.IsAlive) return;

            try
            {
                var prop = mDocument.Configurations.Active.Properties.AGR_TryGetProp(propName);
                if (prop != null) prop.Value = value;
            }
            catch (COMException)
            {
                // Документ закрылся прямо во время записи — переводим VM в неживое
                // состояние, дальнейшие обращения к mDocument прекратятся.
                IsAlive = false;
            }
        }

        /// <summary>Версия и GUID из маркера файла. Маркер повреждён или отсутствует - версия 0.
        /// Маркер от другого «Обозначения» (обозначение очищено/выдано заново) - новый компонент: версия 0, GUID сброшен.</summary>
        private void ReadMarker()
        {
            var markers = AGR_ServiceContainer.GetService<Agrovent.Services.IAGR_MarkerService>();
            if (markers != null
                && markers.TryRead(mDocument!, out var marker, out _) == Agrovent.Services.AGR_MarkerReadResult_e.Ok)
            {
                if (string.Equals(marker!.PartNumber, _partNumber, StringComparison.Ordinal))
                {
                    _version = marker.Version;
                    _storageGuid = marker.StorageGuid;
                }
                else
                {
                    _version = 0;
                    _storageGuid = null;
                }
            }
            else
            {
                _version = 0;
            }
        }

        /// <summary>«Обозначение» пишется только на вкладку Custom файла; из конфигураций удаляется,
        /// чтобы не было двух разных значений (так же делает миграция).</summary>
        private void TryWritePartNumber(string value)
        {
            if (mDocument == null || !mDocument.IsAlive) return;

            try
            {
                var prop = mDocument.Properties.AGR_TryGetProp(AGR_PropertyNames.Partnumber);
                if (prop != null) prop.Value = value;

                if (mDocument.Model.GetConfigurationNames() is object[] configs)
                {
                    foreach (var config in configs.OfType<string>())
                    {
                        var mgr = mDocument.Model.Extension.get_CustomPropertyManager(config);
                        if (mgr?.GetNames() is object[] names
                            && names.OfType<string>().Any(n => string.Equals(n, AGR_PropertyNames.Partnumber, StringComparison.OrdinalIgnoreCase)))
                            mgr.Delete2(AGR_PropertyNames.Partnumber); 
                    }
                }
            }
            catch (COMException)
            {
                IsAlive = false;
            }
        }

        private static int TryParseInt(object value)
        {
            if (value == null) return 0;
            try
            {
                return Convert.ToInt32(value);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        #endregion

        #region PROPS

        public ISwDocument3D SwDocument => mDocument;

        public string Name { get => Path.GetFileNameWithoutExtension(mDocument?.Path); }

        public string ConfigName => _configName;

        public string Extension { get => Path.GetExtension(mDocument?.Path) ?? ""; }

        public string PartNumber
        {
            get => _partNumber;
            set
            {
                _partNumber = value;
                OnPropertyChanged(nameof(PartNumber));
                PartnumberChanged?.Invoke(this, new EventArgs());
                TryWritePartNumber(value);
            }
        }

        public string Article
        {
            get => _article;
            set
            {
                _article = value;
                OnPropertyChanged(nameof(Article));
                TryWriteProperty(AGR_PropertyNames.Article, value);
            }
        }

        public int Version
        {
            get => _version;
            set
            {
                _version = value;
                OnPropertyChanged(nameof(Version));
            }
        }

        // Из маркера файла или из БД (загрузка панели, сохранение); в свойства файла не пишется
        public Guid? StorageGuid
        {
            get => _storageGuid;
            set
            {
                _storageGuid = value;
                OnPropertyChanged(nameof(StorageGuid));
            }
        }
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
                if (value != null)
                {
                    Article = value.Article.ToString();
                    OnPropertyChanged(nameof(Article));
                }
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

        #region EditState
        private AGR_EditState_e _EditState = AGR_EditState_e.Unknown;
        public AGR_EditState_e EditState
        {
            get => _EditState;
            set => Set(ref _EditState, value);
        }
        #endregion

        #region EditLockedBy
        private string? _EditLockedBy;
        public string? EditLockedBy
        {
            get => _EditLockedBy;
            set => Set(ref _EditLockedBy, value);
        }
        #endregion

        #region HasMyLocks
        private bool _HasMyLocks;
        public bool HasMyLocks
        {
            get => _HasMyLocks;
            set => Set(ref _HasMyLocks, value);
        }
        #endregion
        public IAGR_PropertiesCollection PropertiesCollection { get; set; }

        public AGR_ComponentType_e ComponentType
        {
            get => _componentType;
            set
            {
                _componentType = value;
                RebuildPropertiesCollection(value);
                OnPropertyChanged(nameof(ComponentType));

                // Если тип меняется программно (а не через RefreshFromDocument),
                // всё так же best-effort пишем его как производную от AvaType в файл —
                // этим занимается сеттер AvaType, здесь отдельной записи не требуется.
            }
        }

        private void RebuildPropertiesCollection(AGR_ComponentType_e componentType)
        {
            if (mDocument == null || !mDocument.IsAlive)
            {
                // Документ закрыт — не пытаемся строить коллекцию свойств заново,
                // оставляем то, что уже было посчитано ранее (если было).
                return;
            }

            try
            {
                switch (componentType)
                {
                    case AGR_ComponentType_e.Assembly:
                    PropertiesCollection = new AGR_BasePropertiesCollection(mDocument);
                    break;
                    case AGR_ComponentType_e.Part:
                    PropertiesCollection = new AGR_PartPropertiesCollection(mDocument);
                    break;
                    case AGR_ComponentType_e.SheetMetallPart:
                    PropertiesCollection = new AGR_SheetPartPropertiesCollection(mDocument);
                    //PropertiesCollection.UpdateProperties();
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
            }
            catch (COMException)
            {
                IsAlive = false;
                return;
            }

            OnPropertyChanged(nameof(PropertiesCollection));
        }

        public AGR_AvaType_e AvaType
        {
            get => _avaType;
            set
            {
                _avaType = value;
                TryWriteProperty(AGR_PropertyNames.AvaType, (int)value);

                var newComponentType = value == AGR_AvaType_e.Purchased
                    ? AGR_ComponentType_e.Purchased
                    : (mDocument != null && mDocument.IsAlive ? mDocument.ComponentType() : _componentType);

                ComponentType = newComponentType;
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

        protected virtual byte[]? ComputePreviewImageBytes()
        {
            try
            {
                var app = AGR_ServiceContainer.GetService<ISwApplication>();

                string? filePath = FilePath;
                string activeConfig = ConfigName ?? "Default"; // Используем "Default", если ConfigName null

                if (string.IsNullOrEmpty(filePath))
                {
                    return null;
                }

                object? com = app.Sw.GetPreviewBitmap(filePath, activeConfig);
                if (com == null)
                {
                    return Resources.NonePreview;
                }

                stdole.StdPicture? pic = com as stdole.StdPicture;
                if (pic == null)
                {
                    return null;
                }

                var bmp = Bitmap.FromHbitmap((IntPtr)pic.Handle);

                ImageConverter converter = new ImageConverter();
                return (byte[])converter.ConvertTo(bmp, typeof(byte[]));
            }
            catch (COMException comEx)
            {
                return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
        public void PrecomputePreview()
        {
            // Просто обращаемся к свойству, чтобы оно вычислилось в текущем потоке (ожидается UI-поток)
            _ = Preview;
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
                var dataContext = AGR_ServiceContainer.GetService<DataContext>();
                var logger = AGR_ServiceContainer.GetService<ILogger<AGR_SelectAvaArticleVM>>();

                var selectVm = new AGR_SelectAvaArticleVM(dataContext, logger);
                selectVm.SelectedAvaType = AGR_AvaTypeNames.AllTypes;
                selectVm.SearchText = PartNumber;

                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm };

                selectView.ShowActivated = true;
                selectView.ShowDialog();

                if (selectVm.IsDialogResultAccepted == true && selectVm.SelectedArticle != null)
                {
                    AvaArticle = selectVm.SelectedArticle;
                }
            }
            catch (Exception ex)
            {
                // сюда стоит добавить логгер, например через AGR_ServiceContainer.GetService<ILogger<AGR_BaseComponent>>()
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
                return;
            }

            try
            {
                var swApp = AGR_ServiceContainer.GetService<ISwApplication>();
                if (swApp == null)
                {
                    return;
                }

                var openDoc = swApp.Documents.FirstOrDefault(x => x.Path == filePath);
                if (openDoc != null)
                {
                    swApp.Documents.Active = openDoc as ISwDocument;
                }
                else
                {
                    var newDoc = swApp.Documents.Open(filePath, Xarial.XCad.Documents.Enums.DocumentState_e.ReadOnly);
                }
            }
            catch (Exception ex)
            {
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
                return;
            }

            try
            {
                var swApp = AGR_ServiceContainer.GetService<ISwApplication>();
                if (swApp == null)
                {
                    return;
                }

                var activeDoc = swApp.Documents.Active;
                if (!(activeDoc is ISwAssembly swAssembly))
                {
                    return;
                }
                var assemblyFolderPath = Path.GetDirectoryName(swAssembly.Path);
                if (string.IsNullOrEmpty(assemblyFolderPath))
                {
                    assemblyFolderPath = @"D:\Работа";
                }
                var destFilePath = Path.Combine(assemblyFolderPath, Path.GetFileName(filePath));
                var destDrawPath = Path.ChangeExtension(destFilePath, ".slddrw");
                IXDocument3D? compDoc = swApp.Documents.FirstOrDefault(x => x.Title == fileTitle) as IXDocument3D;
                if (compDoc == null)
                {
                    if (File.Exists(destFilePath))
                    {
                        File.SetAttributes(destFilePath, FileAttributes.Normal);
                    }

                    if (File.Exists(destDrawPath))
                    {
                        File.SetAttributes(destDrawPath, FileAttributes.Normal);
                    }

                    File.Copy(filePath, destFilePath, true);
                    if (File.Exists(drawPath))
                    {
                        File.Copy(drawPath, destDrawPath, true);
                    }

                    compDoc = swApp.Documents.PreCreateFromPath(destFilePath) as IXDocument3D;
                    if (compDoc == null)
                    {
                        return;
                    }
                }

                var xComp = swAssembly.Configurations.Active.Components.PreCreate<IXComponent>();
                if (xComp == null)
                {
                    return;
                }

                xComp.ReferencedDocument = compDoc;

                swAssembly.Configurations.Active.Components.Add(xComp);

                xComp.Select(false);

                swApp.Sw.RunCommand(1993, "");
            }
            catch (Exception ex)
            {
            }
        }
        #endregion

        #region ShowDetailsCommand
        private ICommand _ShowDetailsCommand;
        public ICommand ShowDetailsCommand => _ShowDetailsCommand
            ??= new RelayCommand(OnShowDetailsCommandExecuted, CanShowDetailsCommandExecute);

        private bool CanShowDetailsCommandExecute(object p) =>
            !string.IsNullOrEmpty(PartNumber) && Version > 0;

        private async void OnShowDetailsCommandExecuted(object p)
        {
            var unitOfWork = AGR_ServiceContainer.GetService<IUnitOfWork>();

            try
            {
                if (ComponentType == AGR_ComponentType_e.Assembly)
                {
                    var vm = new AGR_AssemblyEditVM(PartNumber, Version, Name, unitOfWork,
                        AGR_ServiceContainer.GetService<ILogger<AGR_AssemblyEditVM>>());
                    //await vm.InitializeAsync(PartNumber, Version);
                    var view = new AGR_SpecificationWindow { DataContext = vm, Topmost = true, Title = $"Подробная информация" };
                    view.Show();
                    OnPropertyChanged(nameof(vm.Components));
                }
                else
                {
                    var vm = new AGR_ComponentEditVM(PartNumber, Version, unitOfWork,
                        AGR_ServiceContainer.GetService<ILogger<AGR_ComponentEditVM>>());
                    var view = new SaveConfirmationView { DataContext = vm, Topmost = true, Title = $"Подробная информация" };
                    view.Show();
                }
            }
            catch (Exception ex)
            {
                // сюда стоит добавить логгер, например через AGR_ServiceContainer.GetService<ILogger<AGR_BaseComponent>>()
            }
        }
        #endregion

        #region SetNewPartnumberCommand
        private ICommand _SetNewPartnumberCommand;
        public ICommand SetNewPartnumberCommand => _SetNewPartnumberCommand
            ??= new RelayCommand(OnSetNewPartnumberCommandExecuted, CanSetNewPartnumberCommandExecute);
        private bool CanSetNewPartnumberCommandExecute(object p) => true;
        private async void OnSetNewPartnumberCommandExecuted(object p)
        {
            // Свой изолированный скоуп на одну операцию — свой DataContext,
            // не завязанный на "вечный" _componentVersionService из корневого провайдера.
            using var scope = _scopeFactory.CreateScope();
            var versionService = scope.ServiceProvider.GetRequiredService<IAGR_ComponentVersionService>();

            try
            {
                var newComponent = await versionService.CreateNewComponent(this);
                if (newComponent != null && !string.IsNullOrWhiteSpace(newComponent.PartNumber))
                {
                    // Сработает существующий сеттер: OnPropertyChanged + PartnumberChanged + запись в свойство SW-документа
                    PartNumber = newComponent.PartNumber;
                }
            }
            catch (Exception ex)
            {
                // сюда стоит добавить логгер, например через scope.ServiceProvider.GetService<ILogger<AGR_BaseComponent>>()
            }
        }
        #endregion

        #endregion

        public event EventHandler? PartnumberChanged;
    }

}

