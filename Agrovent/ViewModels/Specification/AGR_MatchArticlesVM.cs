using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Agrovent.DAL;
using Agrovent.Infrastructure.Commands;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.ViewModels.Base;
using AgroventInfrastructure.Entities.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agrovent.ViewModels.Specification
{
    /// <summary>
    /// Один найденный вариант AvaArticle для конкретного компонента спецификации.
    /// Отображается как RadioButton в правой части строки DataGrid.
    /// </summary>
    public class AGR_ArticleCandidateVM : BaseViewModel
    {
        public IAGR_AvaArticleModel AvaArticle { get; }

        private readonly AGR_ArticleMatchItemVM _owner;

        public AGR_ArticleCandidateVM(IAGR_AvaArticleModel avaArticle, AGR_ArticleMatchItemVM owner, bool isPartNumberMatch, bool isNameMatch)
        {
            AvaArticle = avaArticle ?? throw new ArgumentNullException(nameof(avaArticle));
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            IsPartNumberMatch = isPartNumberMatch;
            IsNameMatch = isNameMatch;
        }

        private bool _isSelected;
        /// <summary>
        /// Т.к. вариантов на строку несколько, а подходит только один - при выборе
        /// одного кандидата сбрасываем выбор у остальных кандидатов этой же строки.
        /// Это заменяет собой поведение RadioButton.GroupName, которое ненадёжно
        /// работает в виртуализированном DataGrid с динамическим набором строк.
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (!Set(ref _isSelected, value)) return;
                if (value)
                {
                    _owner.NotifyCandidateSelected(this);
                }
            }
        }

        public string DisplayArticle => $"№{AvaArticle.Article}";
        public string DisplayName => AvaArticle.Name ?? string.Empty;
        public string DisplayPartNumber => AvaArticle.PartNumber ?? string.Empty;

        /// <summary>Совпадение по партномеру - используется в UI для подсветки лучшего варианта.</summary>
        public bool IsPartNumberMatch { get; }
        /// <summary>Совпадение по наименованию - используется в UI для подсветки варианта.</summary>
        public bool IsNameMatch { get; }
    }

    /// <summary>
    /// Строка окна подбора: компонент спецификации + список найденных для него кандидатов.
    /// </summary>
    public class AGR_ArticleMatchItemVM : BaseViewModel
    {
        /// <summary>Ссылка на исходный элемент спецификации (тот же объект, что в Components
        /// основного окна). Присвоение AvaArticle здесь напрямую обновит основную таблицу.</summary>
        public AGR_SpecificationItemVM SpecificationItem { get; }

        public ObservableCollection<AGR_ArticleCandidateVM> Candidates { get; } = new ObservableCollection<AGR_ArticleCandidateVM>();

        public AGR_ArticleMatchItemVM(AGR_SpecificationItemVM specItem)
        {
            SpecificationItem = specItem ?? throw new ArgumentNullException(nameof(specItem));
        }

        public string Name => SpecificationItem.Name;
        public string PartNumber => SpecificationItem.PartNumber;
        public AGR_ComponentType_e ComponentType => SpecificationItem.ComponentType;
        public int Quantity => SpecificationItem.Quantity;

        public bool HasCandidates => Candidates.Count > 0;
        public bool NoCandidatesFound => !HasCandidates;

        private AGR_ArticleCandidateVM _selectedCandidate;
        public AGR_ArticleCandidateVM SelectedCandidate
        {
            get => _selectedCandidate;
            private set
            {
                Set(ref _selectedCandidate, value);
                OnPropertyChanged(nameof(IsResolved));
            }
        }

        public bool IsResolved => SelectedCandidate != null;

        /// <summary>Выбирает кандидата по умолчанию: уже назначенный артикул, если он есть
        /// среди найденных вариантов, иначе - единственный найденный вариант.</summary>
        public void PreselectDefault()
        {
            var existingArticle = SpecificationItem.AvaArticle;

            AGR_ArticleCandidateVM preselect = existingArticle != null
                ? Candidates.FirstOrDefault(c => c.AvaArticle.Article == existingArticle.Article)
                : null;

            if (preselect == null && Candidates.Count == 1)
            {
                preselect = Candidates[0];
            }

            if (preselect != null)
            {
                preselect.IsSelected = true; // вызовет NotifyCandidateSelected -> SelectedCandidate
            }
        }

        public void NotifyCandidateSelected(AGR_ArticleCandidateVM candidate)
        {
            foreach (var c in Candidates)
            {
                if (!ReferenceEquals(c, candidate) && c.IsSelected)
                {
                    c.IsSelected = false;
                }
            }
            SelectedCandidate = candidate;
        }

        /// <summary>Снять выбор для этой строки (кнопка "Без артикула" на строке).</summary>
        public void ClearSelection()
        {
            foreach (var c in Candidates) c.IsSelected = false;
            SelectedCandidate = null;
        }
    }

    /// <summary>
    /// ViewModel окна "Подобрать артикулы": по каждому компоненту спецификации без
    /// назначенного AvaArticle ищет в справочнике подходящие варианты (по PartNumber и/или
    /// Name) и позволяет пользователю быстро выбрать нужный вариант для каждого компонента.
    /// Изменения применяются к исходным AGR_SpecificationItemVM только по нажатию "Сохранить".
    /// </summary>
    public class AGR_MatchArticlesVM : BaseViewModel
    {
        private readonly IEnumerable<AGR_SpecificationItemVM> _sourceComponents;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AGR_MatchArticlesVM> _logger;

        #region CTOR
        public AGR_MatchArticlesVM(
            IEnumerable<AGR_SpecificationItemVM> components,
            IServiceScopeFactory scopeFactory,
            ILogger<AGR_MatchArticlesVM> logger)
        {
            _sourceComponents = components ?? throw new ArgumentNullException(nameof(components));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger;

            Items = new ObservableCollection<AGR_ArticleMatchItemVM>();
            GroupedItemsView = CollectionViewSource.GetDefaultView(Items);
            UpdateGroupedView();

            // Загрузка справочника и подбор кандидатов выполняются асинхронно;
            // окно сразу показывается с оверлеем IsLoading (см. BaseViewModel).
            _ = LoadAsync();
        }
        #endregion

        #region PROPS
        private ObservableCollection<AGR_ArticleMatchItemVM> _items;
        public ObservableCollection<AGR_ArticleMatchItemVM> Items
        {
            get => _items;
            private set => Set(ref _items, value);
        }

        public ICollectionView GroupedItemsView { get; private set; }

        private int _unresolvedCount;
        public int UnresolvedCount
        {
            get => _unresolvedCount;
            private set
            {
                if (Set(ref _unresolvedCount, value))
                {
                    OnPropertyChanged(nameof(HasUnresolved));
                }
            }
        }

        /// <summary>Есть ли строки без выбранного варианта - для показа предупреждения в UI.</summary>
        public bool HasUnresolved => UnresolvedCount > 0;

        private bool _dialogSaved;
        public bool DialogSaved
        {
            get => _dialogSaved;
            private set => Set(ref _dialogSaved, value);
        }
        #endregion

        #region Загрузка справочника и подбор кандидатов
        private async Task LoadAsync()
        {
            IsLoading = true;
            LoadingStatus = "Загрузка справочника артикулов...";
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

                // Загружаем весь справочник AvaArticle один раз (аналогично AGR_SelectAvaArticleVM)
                // и дальше подбираем совпадения в памяти: один запрос к БД вместо запроса
                // на каждый компонент - и, соответственно, быстрее.
                var allArticles = await dataContext.AvaArticles
                    .Where(x => x.ArchiveType != null 
                            && !x.ArchiveType.Contains("50177") 
                            && !string.IsNullOrEmpty(x.Type)
                            && !string.IsNullOrWhiteSpace(x.Type)
                            && x.Type != null)
                    .AsNoTracking()
                    .ToListAsync();

                LoadingStatus = "Подбор артикулов по компонентам...";

                // Подбираем варианты только для компонентов, у которых артикул ещё не назначен -
                // остальные уже обработаны через "Установить артикул" и повторный подбор не нужен.
                var targets = _sourceComponents
                    .Where(c => c.AvaArticle == null)
                    .ToList();

                var items = targets
                    .Select(comp => BuildMatchItem(comp, allArticles))
                    .OrderBy(i => i.ComponentType)
                    .ThenBy(i => i.Name)
                    .ToList();

                foreach (var item in items)
                {
                    item.PropertyChanged += Item_PropertyChanged;
                }

                Items = new ObservableCollection<AGR_ArticleMatchItemVM>(items);
                GroupedItemsView = CollectionViewSource.GetDefaultView(Items);
                UpdateGroupedView();
                OnPropertyChanged(nameof(GroupedItemsView));

                RecalculateUnresolvedCount();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при подборе артикулов для компонентов спецификации.");
                MessageBox.Show(
                    "Не удалось выполнить подбор артикулов. Подробности см. в логе.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AGR_ArticleMatchItemVM.IsResolved))
            {
                RecalculateUnresolvedCount();
            }
        }

        private void RecalculateUnresolvedCount()
        {
            UnresolvedCount = Items.Count(i => !i.IsResolved);
        }

        /// <summary>Строит строку окна подбора для одного компонента: ищет кандидатов
        /// в справочнике и предвыбирает вариант по умолчанию.</summary>
        private static AGR_ArticleMatchItemVM BuildMatchItem(AGR_SpecificationItemVM comp, List<AvaArticleModel> allArticles)
        {
            var item = new AGR_ArticleMatchItemVM(comp);

            var name = comp.Name?.Trim();
            var partNumber = comp.PartNumber?.Trim();

            bool IsPartNumberMatch(AvaArticleModel a) =>
                !string.IsNullOrEmpty(partNumber)
                && !string.IsNullOrEmpty(a.PartNumber)
                && string.Equals(a.PartNumber.Trim(), partNumber, StringComparison.OrdinalIgnoreCase);

            bool IsNameMatch(AvaArticleModel a) =>
                !string.IsNullOrEmpty(name)
                && a.Name?.Count() > 1
                && !string.IsNullOrEmpty(a.Name)
                && (a.Name.Contains(name, StringComparison.OrdinalIgnoreCase)
                    || name.Contains(a.Name, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(name) || !string.IsNullOrEmpty(partNumber))
            {
                var ranked = allArticles
                    .Where(a => IsPartNumberMatch(a) || IsNameMatch(a))
                    .OrderByDescending(IsPartNumberMatch) // точные совпадения по партномеру - в начале списка
                    .ThenBy(a => a.Name)
                    .Take(25); // не показываем неограниченный список - обычно первых кандидатов достаточно

                foreach (var a in ranked)
                {
                    item.Candidates.Add(new AGR_ArticleCandidateVM(a, item, IsPartNumberMatch(a), IsNameMatch(a)));
                }
            }

            item.PreselectDefault();
            return item;
        }
        #endregion

        private void UpdateGroupedView()
        {
            if (GroupedItemsView == null) return;

            GroupedItemsView.GroupDescriptions.Clear();
            GroupedItemsView.GroupDescriptions.Add(
                new PropertyGroupDescription(nameof(AGR_ArticleMatchItemVM.ComponentType)));

            GroupedItemsView.SortDescriptions.Clear();
            GroupedItemsView.SortDescriptions.Add(
                new SortDescription(nameof(AGR_ArticleMatchItemVM.ComponentType), ListSortDirection.Ascending));
            GroupedItemsView.SortDescriptions.Add(
                new SortDescription(nameof(AGR_ArticleMatchItemVM.Name), ListSortDirection.Ascending));
        }

        #region SaveCommand
        private ICommand _saveCommand;
        public ICommand SaveCommand => _saveCommand ??= new RelayCommand(OnSaveExecuted);
        private void OnSaveExecuted(object p)
        {
            // Применяем выбранные варианты к исходным элементам спецификации.
            // AGR_SpecificationItemVM.AvaArticle сам обновит _component.AvaArticle
            // и вызовет OnPropertyChanged для Article/ArticleName/PartnumberOrArticle,
            // поэтому основная таблица спецификации обновится автоматически - без
            // необходимости пересоздавать коллекцию Components.
            foreach (var item in Items)
            {
                if (item.SelectedCandidate != null)
                {
                    item.SpecificationItem.AvaArticle = item.SelectedCandidate.AvaArticle;
                }
            }

            DialogSaved = true;

            var view = p as Window;
            view?.Close();
        }
        #endregion

        #region CancelCommand
        private ICommand _cancelCommand;
        public ICommand CancelCommand => _cancelCommand ??= new RelayCommand(OnCancelExecuted);
        private void OnCancelExecuted(object p)
        {
            // Ничего не применяем - весь выбор в этом окне отбрасывается.
            DialogSaved = false;

            var view = p as Window;
            view?.Close();
        }
        #endregion

        #region ClearSelectionCommand (кнопка "Без артикула" в строке)
        private ICommand _clearSelectionCommand;
        public ICommand ClearSelectionCommand => _clearSelectionCommand
            ??= new RelayCommand<AGR_ArticleMatchItemVM>(OnClearSelectionExecuted);
        private void OnClearSelectionExecuted(AGR_ArticleMatchItemVM item)
        {
            item?.ClearSelection();
        }
        #endregion
    }
}
