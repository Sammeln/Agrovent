// File: ViewModels/Windows/AGR_ComponentEditVM.cs
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Agrovent.DAL;
using Agrovent.Infrastructure.Commands;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Components;
using Agrovent.Views.Windows;
using AgroventInfrastructure;
using AgroventInfrastructure.Entities.Components;
using AgroventInfrastructure.Enums;
using AgroventInfrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Xarial.XCad.Documents.Extensions;
using Xarial.XCad.SolidWorks;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels.Windows
{
    /// <summary>
    /// VM для SaveConfirmationView в режиме редактирования уже сохранённого
    /// в БД компонента (деталь/покупное) — без версии, без экспорта, без живого документа SW.
    /// </summary>
    public class AGR_ComponentEditVM : BaseViewModel
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger? _logger;
        private ComponentVersion _entity;

        private readonly List<ComponentFile> _newFiles = new();
        private readonly List<ComponentFile> _removedFiles = new();

        // Параметры отложенной загрузки: конструктор больше не запускает InitializeAsync сам —
        // это делает View по событию Loaded, чтобы окно успело отобразиться с оверлеем прогресса.
        private string _pendingPartNumber;
        private int _pendingVersion;

        public bool? DialogResult { get; set; }
        public bool IsEditMode => true;
        public bool PropertiesReadOnly => false;
        public AGR_ComponentEditVM(string partNumber, int version, IUnitOfWork unitOfWork, ILogger? logger = null)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger;

            BlankProperties = new ObservableCollection<AGR_ComponentPropertyEditVM>();
            Files = new ObservableCollection<AGR_ComponentFileEditVM>();

            _pendingPartNumber = partNumber;
            _pendingVersion = version;
        }
        public AGR_ComponentEditVM(AGR_ComponentRegistryItemVM registryItem, IUnitOfWork unitOfWork, ILogger? logger = null)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger;

            BlankProperties = new ObservableCollection<AGR_ComponentPropertyEditVM>();
            Files = new ObservableCollection<AGR_ComponentFileEditVM>();

            _pendingPartNumber = registryItem.RawPartNumber;
            _pendingVersion = registryItem.Version;
        }

        #region Инициализация
        // Вызывается из Window_Loaded окна SaveConfirmationView — окно уже видно, показан оверлей прогресса.
        public Task InitializeAsync() => InitializeAsync(_pendingPartNumber, _pendingVersion);

        public async Task InitializeAsync(string partNumber, int version)
        {
            try
            {
                IsLoading = true;
                LoadingStatus = "Загрузка компонента...";

                _entity = await _unitOfWork.ComponentRepository.GetComponentVersionForEdit(partNumber, version);
                if (_entity == null)
                {
                    _logger?.LogWarning($"Не найдена версия компонента {partNumber} v{version} для редактирования.");
                    ErrorMessages = "Не удалось загрузить компонент из БД.";
                    HasErrors = true;
                    return;
                }

                Preview = _entity.PreviewImage;
                OnPropertyChanged(nameof(ComponentName));
                OnPropertyChanged(nameof(PartNumber));
                OnPropertyChanged(nameof(ConfigName));
                OnPropertyChanged(nameof(ComponentType));
                OnPropertyChanged(nameof(IsPart));
                OnPropertyChanged(nameof(IsSheetMetal));
                OnPropertyChanged(nameof(IsPurchased));
                OnPropertyChanged(nameof(IsProduced));
                OnPropertyChanged(nameof(PropertiesIsVisible));

                _baseAvaTypeEnum = _entity.AvaType;
                OnPropertyChanged(nameof(BaseAvaTypeEnum));

                if (_entity.Material?.HasMaterial == true)
                {
                    _baseMaterial = _entity.Material.MaterialAvaArticle != null
                        ? new AGR_Material(_entity.Material.MaterialAvaArticle)
                        : new AGR_Material(_entity.Material.BaseMaterial);
                }
                OnPropertyChanged(nameof(MaterialName));

                if (_entity.Material?.HasPaint == true && _entity.Material.PaintAvaArticle != null)
                {
                    _paint = new AGR_Paint(_entity.Material.PaintAvaArticle);
                }
                _noPaint = _entity.Material?.HasPaint != true;
                OnPropertyChanged(nameof(ColorName));
                OnPropertyChanged(nameof(NoPaint));

                if (_entity.AvaArticle != null) _avaArticle = _entity.AvaArticle;
                _noArticle = _entity.AvaArticle == null;
                OnPropertyChanged(nameof(Article));
                OnPropertyChanged(nameof(NoArticle));

                foreach (var prop in _entity.Properties)
                    BlankProperties.Add(new AGR_ComponentPropertyEditVM(prop));

                foreach (var file in _entity.Files)
                    Files.Add(new AGR_ComponentFileEditVM(file, _entity.HashSum));

                //ValidateComponent();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Ошибка при загрузке компонента {partNumber} v{version} для редактирования.");
                ErrorMessages = "Ошибка загрузки: " + ex.Message;
                HasErrors = true;
            }
            finally
            {
                IsLoading = false;
                LoadingStatus = string.Empty;
            }
        }
        #endregion

        #region Component Info
        public string ComponentName => _entity?.Name ?? string.Empty;
        public string PartNumber => _entity?.Component?.PartNumber ?? string.Empty;
        public string ConfigName => _entity?.ConfigName ?? string.Empty;
        public AGR_ComponentType_e ComponentType => _entity?.ComponentType ?? default;

        private byte[] _preview;
        public byte[] Preview
        {
            get => _preview;
            set => Set(ref _preview, value);
        }
        #endregion

        #region Material
        private IAGR_Material _baseMaterial;
        public IAGR_Material BaseMaterial
        {
            get => _baseMaterial;
            set
            {
                if (Set(ref _baseMaterial, value))
                {
                    _entity.Material ??= new ComponentMaterial { ComponentVersionId = _entity.Id };
                    _entity.Material.BaseMaterial = value?.Name;
                    if (_entity.Material.BaseMaterialCount == 0) _entity.Material.BaseMaterialCount = 1;
                    _entity.Material.MaterialAvaArticleID = (value?.AvaModel as AvaArticleModel)?.Article;
                    OnPropertyChanged(nameof(MaterialName));
                    ValidateComponent();
                }
            }
        }
        public string MaterialName =>
            BaseMaterial != null ? $"{BaseMaterial.AvaModel?.Article} {BaseMaterial.Name}".Trim() : string.Empty;
        #endregion

        #region Paint
        private IAGR_Material? _paint;
        public IAGR_Material? Paint
        {
            get => _paint;
            set
            {
                if (Set(ref _paint, value))
                {
                    _entity.Material ??= new ComponentMaterial { ComponentVersionId = _entity.Id };
                    _entity.Material.Paint = value?.Name;
                    _entity.Material.PaintAvaArticleID = (value?.AvaModel as AvaArticleModel)?.Article;
                    OnPropertyChanged(nameof(ColorName));
                    ValidateComponent();
                }
            }
        }

        private bool _noPaint;
        public bool NoPaint
        {
            get => _noPaint;
            set
            {
                Set(ref _noPaint, value);
                if (value) Paint = null;
                ValidateComponent();
            }
        }

        public string ColorName =>
            Paint != null ? $"{Paint.AvaModel?.Article} {Paint.Name}".Trim() : string.Empty;
        #endregion

        #region Article
        private IAGR_AvaArticleModel? _avaArticle;
        public IAGR_AvaArticleModel? AvaArticle
        {
            get => _avaArticle;
            set
            {
                Set(ref _avaArticle, value);
                if (value is AvaArticleModel model)
                {
                    _entity.AvaArticleArticle = model.Article;
                }
                else
                {
                    _entity.AvaArticleArticle = null;
                }
                OnPropertyChanged(nameof(Article));
                ValidateComponent();
            }
        }

        private bool _noArticle;
        public bool NoArticle
        {
            get => _noArticle;
            set
            {
                Set(ref _noArticle, value);
                if (value) AvaArticle = null;
                ValidateComponent();
            }
        }
        public string Article =>
            AvaArticle != null ? $"{AvaArticle.Article} {AvaArticle.Name}".Trim() : string.Empty;
        #endregion

        #region AvaType
        private AGR_AvaType_e _baseAvaTypeEnum;
        public AGR_AvaType_e BaseAvaTypeEnum
        {
            get => _baseAvaTypeEnum;
            set
            {
                if (Set(ref _baseAvaTypeEnum, value))
                {
                    _entity.AvaType = value;
                    OnPropertyChanged(nameof(PropertiesIsVisible));
                    OnPropertyChanged(nameof(IsPurchased));
                    OnPropertyChanged(nameof(IsProduced));
                    ValidateComponent();
                }
            }
        }
        #endregion

        public bool IsPart => ComponentType == AGR_ComponentType_e.Part || ComponentType == AGR_ComponentType_e.SheetMetallPart;
        public bool IsSheetMetal => ComponentType == AGR_ComponentType_e.SheetMetallPart;
        public bool IsPurchased => ComponentType == AGR_ComponentType_e.Purchased || BaseAvaTypeEnum == AGR_AvaType_e.DontBuy;
        public bool IsProduced => !IsPurchased; // отсутствовало в оригинальной AGR_SaveConfirmationVM — добавлено для рабочих биндингов
        public string PropertiesIsVisible => IsPurchased ? "Collapsed" : "Visible";

        #region BlankProperties (редактируемые)
        private ObservableCollection<AGR_ComponentPropertyEditVM> _blankProperties;
        public ObservableCollection<AGR_ComponentPropertyEditVM> BlankProperties
        {
            get => _blankProperties;
            set => Set(ref _blankProperties, value);
        }
        #endregion

        #region Files
        private ObservableCollection<AGR_ComponentFileEditVM> _files;
        public ObservableCollection<AGR_ComponentFileEditVM> Files
        {
            get => _files;
            set => Set(ref _files, value);
        }
        #endregion

        #region Errors
        private string _errorMessages = string.Empty;
        public string ErrorMessages
        {
            get => _errorMessages;
            set => Set(ref _errorMessages, value);
        }

        private bool _hasErrors;
        public bool HasErrors
        {
            get => _hasErrors;
            set => Set(ref _hasErrors, value);
        }
        #endregion

        private void ValidateComponent()
        {
            var errors = new List<string>();

            if (!IsPurchased)
            {
                if (string.IsNullOrWhiteSpace(MaterialName)) errors.Add(AGR_SaveConfirmationErrors.NoMaterial);
                if (!NoPaint && string.IsNullOrWhiteSpace(ColorName)) errors.Add(AGR_SaveConfirmationErrors.NoColor);
                if (!NoArticle && string.IsNullOrWhiteSpace(Article)) errors.Add(AGR_SaveConfirmationErrors.NoArticle);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(Article)) errors.Add(AGR_SaveConfirmationErrors.NoArticle);
            }

            HasErrors = errors.Any();
            ErrorMessages = string.Join("\n", errors);
        }

        #region COMMANDS

        #region SelectMaterialCommand
        private ICommand _SelectMaterialCommand;
        public ICommand SelectMaterialCommand => _SelectMaterialCommand ??= new RelayCommand(_ => SelectMaterial(), _ => IsPart);
        private void SelectMaterial()
        {
            try
            {
                var dataContext = AGR_ServiceContainer.GetService<DataContext>();
                var logger = AGR_ServiceContainer.GetService<ILogger<AGR_SelectAvaArticleVM>>();
                var selectVm = new AGR_SelectAvaArticleVM(dataContext, logger) { SelectedAvaType = "Товар" };
                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm, ShowActivated = true, Topmost = true };
                selectView.ShowDialog();

                if (selectVm.IsDialogResultAccepted == true && selectVm.SelectedArticle != null)
                    BaseMaterial = new AGR_Material(selectVm.SelectedArticle);
            }
            catch (Exception ex) { _logger?.LogError(ex, "Ошибка при выборе материала"); }
        }
        #endregion

        #region SelectColorCommand
        private ICommand _SelectColorCommand;
        public ICommand SelectColorCommand => _SelectColorCommand ??= new RelayCommand(_ => SelectColor(), _ => IsProduced);
        private void SelectColor()
        {
            try
            {
                var dataContext = AGR_ServiceContainer.GetService<DataContext>();
                var logger = AGR_ServiceContainer.GetService<ILogger<AGR_SelectAvaArticleVM>>();
                var selectVm = new AGR_SelectAvaArticleVM(dataContext, logger) { SearchText = "Краска порошковая" };
                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm, ShowActivated = true };
                selectView.ShowDialog();

                if (selectVm.IsDialogResultAccepted == true && selectVm.SelectedArticle != null)
                {
                    Paint = new AGR_Paint(selectVm.SelectedArticle);
                    NoPaint = false;
                }
            }
            catch (Exception ex) { _logger?.LogError(ex, "Ошибка при выборе покрытия"); }
        }
        #endregion

        #region SelectArticleCommand
        private ICommand _SelectArticleCommand;
        public ICommand SelectArticleCommand => _SelectArticleCommand ??= new RelayCommand(_ => SelectArticle(), _ => !NoArticle);
        private void SelectArticle()
        {
            try
            {
                var dataContext = AGR_ServiceContainer.GetService<DataContext>();
                var logger = AGR_ServiceContainer.GetService<ILogger<AGR_SelectAvaArticleVM>>();
                var selectVm = new AGR_SelectAvaArticleVM(dataContext, logger)
                {
                    SearchText = ComponentName,
                    SelectedAvaType = IsPurchased ? AGR_AvaTypeNames.Purchased : AGR_AvaTypeNames.Component
                };
                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm, ShowActivated = true };
                selectView.ShowDialog();

                if (selectVm.IsDialogResultAccepted == true && selectVm.SelectedArticle != null)
                    AvaArticle = selectVm.SelectedArticle;
            }
            catch (Exception ex) { _logger?.LogError(ex, "Ошибка при выборе артикула"); }
        }
        #endregion

        #region Файлы: Open / Replace / Add / Remove
        private ICommand _OpenFileCommand;
        public ICommand OpenFileCommand => _OpenFileCommand ??= new RelayCommand<AGR_ComponentFileEditVM>(f =>
        {
            try
            {
                var path = f.ResolveStoragePath();
                if (File.Exists(path))
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                else
                    _logger?.LogWarning($"Файл не найден на диске: {path}");
            }
            catch (Exception ex) { _logger?.LogError(ex, "Ошибка при открытии файла"); }
        });

        private ICommand _ReplaceFileCommand;
        public ICommand ReplaceFileCommand => _ReplaceFileCommand ??= new RelayCommand<AGR_ComponentFileEditVM>(f =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Выберите новый файл" };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var targetPath = f.ResolveStoragePath();
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Copy(dlg.FileName, targetPath, true);
                f.UpdateFromSource(dlg.FileName);
            }
            catch (Exception ex) { _logger?.LogError(ex, "Ошибка при замене файла"); }
        });

        private ICommand _AddFileCommand;
        public ICommand AddFileCommand => _AddFileCommand ??= new RelayCommand(_ =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Добавить файл" };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var storageFolder = Path.Combine(AGR_Options.StorageRootFolderPath, _entity.HashSum.ToString("D10"));
                Directory.CreateDirectory(storageFolder);
                var targetPath = Path.Combine(storageFolder, Path.GetFileName(dlg.FileName));
                File.Copy(dlg.FileName, targetPath, true);

                var newFile = new ComponentFile
                {
                    ComponentVersionId = _entity.Id,
                    FileType = InferFileType(dlg.FileName),
                    FilePath = dlg.FileName,
                    FileSize = new FileInfo(dlg.FileName).Length,
                    LastModified = File.GetLastWriteTime(dlg.FileName)
                };

                Files.Add(new AGR_ComponentFileEditVM(newFile, _entity.HashSum));
                _newFiles.Add(newFile);
            }
            catch (Exception ex) { _logger?.LogError(ex, "Ошибка при добавлении файла"); }
        });

        private ICommand _RemoveFileCommand;
        public ICommand RemoveFileCommand => _RemoveFileCommand ??= new RelayCommand<AGR_ComponentFileEditVM>(f =>
        {
            Files.Remove(f);
            if (_newFiles.Contains(f.Entity))
                _newFiles.Remove(f.Entity); // ещё не сохранён — просто убираем из списка на добавление
            else
                _removedFiles.Add(f.Entity); // существующий в БД — пометить на удаление при Save
        });

        private static AGR_FileType_e InferFileType(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".slddrw" => AGR_FileType_e.StorageDrawing,
                _ => AGR_FileType_e.StorageModel
            };
        }
        #endregion

        #region SaveCommand
        private ICommand _SaveCommand;
        public ICommand SaveCommand => _SaveCommand ??= new RelayCommand(OnSaveExecuted, _ => !HasErrors);
        private async void OnSaveExecuted(object p)
        {
            try
            {
                foreach (var file in _newFiles)
                    await _unitOfWork.ComponentRepository.AddComponentFileAsync(file);

                foreach (var file in _removedFiles)
                    await _unitOfWork.ComponentRepository.RemoveComponentFileAsync(file.Id);

                // Правки материала/покрытия/артикула/свойств уже отслеживаются EF Core
                // напрямую на сущностях (_entity, _entity.Material, ComponentProperty) — просто сохраняем.
                await _unitOfWork.CompleteAsync();

                DialogResult = true;
                (p as Window)?.Close();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при сохранении изменений компонента в БД.");
                ErrorMessages = "Не удалось сохранить изменения: " + ex.Message;
                HasErrors = true;
            }
        }
        #endregion

        #region CancelCommand
        private ICommand _CancelCommand;
        public ICommand CancelCommand => _CancelCommand ??= new RelayCommand(p =>
        {
            DialogResult = false;
            (p as Window)?.Close();
        });
        #endregion

        #region UpdateFromModelCommand
        private ICommand _UpdateFromModelCommand;
        public ICommand UpdateFromModelCommand => _UpdateFromModelCommand
            ??= new RelayCommand(OnUpdateFromModelExecuted, _ => true); // заготовка есть только у Part / SheetMetal

        private void OnUpdateFromModelExecuted(object p)
        {
            if (_entity == null) return;

            var modelFile = Files.FirstOrDefault(f => f.FileType == AGR_FileType_e.StorageModel);
            var modelPath = modelFile?.ResolveStoragePath();

            if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
            {
                _logger?.LogWarning($"Не найден файл модели в хранилище для {PartNumber}.");
                ErrorMessages = "Файл модели в хранилище не найден.";
                HasErrors = true;
                return;
            }

            var swApp = AGR_ServiceContainer.GetService<ISwApplication>();
            bool wasAlreadyOpen = swApp.Documents.Any(x => x.Path == modelPath);
            ISwDocument3D? doc = null;

            try
            {
                doc = (swApp.Documents.FirstOrDefault(x => x.Path == modelPath)
                    ?? swApp.Documents.Open(modelPath, Xarial.XCad.Documents.Enums.DocumentState_e.Silent)) as ISwDocument3D;

                if (doc == null)
                {
                    _logger?.LogWarning($"Не удалось открыть файл модели: {modelPath}");
                    return;
                }

                // Обёртка над открытой моделью — сама построит нужный PropertiesCollection
                var modelComponent = new AGR_PartComponentVM(doc);
                var modelProperties = modelComponent.PropertiesCollection?.Properties;

                int updated = 0;
                int added = 0;

                if (modelProperties != null)
                {
                    // Идём от свойств МОДЕЛИ, а не от того, что уже есть в БД —
                    // так подтягиваются и те свойства, которых в БД ещё нет
                    // (например, если при первом сохранении что-то пошло не так).
                    foreach (var fileProp in modelProperties)
                    {
                        if (fileProp?.Value == null || string.IsNullOrEmpty(fileProp.Name))
                            continue;

                        var propVm = BlankProperties.FirstOrDefault(pv => pv.Name == fileProp.Name);

                        if (propVm != null)
                        {
                            propVm.Value = fileProp.Value.ToString(); // пишет и в propVm, и в _entity
                            updated++;
                        }
                        else
                        {
                            // Свойства в БД нет — заводим новую ComponentProperty и добавляем
                            // и в сущность (для сохранения EF Core через SaveCommand), и в VM (для отображения).
                            var newProp = new ComponentProperty
                            {
                                ComponentVersionId = _entity.Id,
                                Name = fileProp.Name,
                                Value = fileProp.Value.ToString()
                            };

                            _entity.Properties.Add(newProp);
                            BlankProperties.Add(new AGR_ComponentPropertyEditVM(newProp));
                            added++;
                        }
                    }
                }

                //_logger?.LogInformation($"Обновлено {updated} и добавлено {added} свойств заготовки из модели {modelPath} для {PartNumber}.");
                ValidateComponent();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Ошибка обновления свойств из модели {modelPath}.");
                ErrorMessages = "Не удалось обновить свойства из модели: " + ex.Message;
                HasErrors = true;
            }
            finally
            {

            }
        }
        #endregion

        #endregion
    }
}
