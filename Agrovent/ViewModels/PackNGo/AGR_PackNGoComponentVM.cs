using System;
using System.IO;
using System.Windows.Input;
using Agrovent.Infrastructure.Commands;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces.Components;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.ViewModels.Base;

namespace Agrovent.ViewModels.PackNGo
{
    /// <summary>
    /// Общее ("разделяемое") состояние одного уникального файла компонента (детали или сборки)
    /// внутри диалога Pack'n'Go. Если один и тот же файл встречается в дереве сборки несколько раз
    /// (например, стандартная деталь сразу в двух подсборках), для него создаётся ровно один
    /// экземпляр этого класса - на него ссылаются все строки таблицы (AGR_PackNGoNodeVM),
    /// представляющие эти вхождения. Благодаря этому редактирование значения в одной строке сразу
    /// отражается во всех остальных строках с тем же компонентом (см. AGR_PackNGoVM.GetOrCreateComponentVM).
    /// </summary>
    public class AGR_PackNGoComponentVM : BaseViewModel
    {
        /// <summary>
        /// Обёрнутый "боевой" компонент - тот же самый разделяемый экземпляр, который используется
        /// во всём приложении (кэш IAGR_ViewModelCacheService, см. AGR_ComponentExtension.AGR_BaseComponent()).
        /// ВАЖНО: в диалоге Pack'n'Go мы НИКОГДА не пишем в Component.PartNumber напрямую - это
        /// был бы открытый ИСХОДНЫЙ документ, а не копия. Партномер, который пользователь видит и
        /// редактирует в таблице, хранится отдельно (см. PartNumber ниже) и переносится в реальный
        /// файл уже после копирования - см. AGR_PackNGoVM.ApplyPartNumbersToCopy.
        /// </summary>
        public IAGR_BaseComponent Component { get; }

        public AGR_PackNGoComponentVM(IAGR_BaseComponent component)
        {
            Component = component ?? throw new ArgumentNullException(nameof(component));

            // Инициализация "по тихому" - через backing-поля, а не через публичные сеттеры,
            // чтобы не запустить сброс/каскад прямо при построении дерева. Значение PartNumber -
            // это снимок состояния исходного документа на момент открытия диалога.
            _saveAsName = component.Name;
            _partNumber = component.PartNumber;
        }

        #region Отображаемые (только для чтения) свойства компонента

        public string Name => Component.Name;
        public string Extension => Component.Extension;
        public AGR_ComponentType_e ComponentType => Component.ComponentType;

        /// <summary>Путь к текущему файлу модели. Берём из IAGR_HasFile (кэшируется при создании
        /// VM компонента), а если компонент его не реализует - из живого SW-документа.</summary>
        public string FilePath => (Component as IAGR_HasFile)?.CurrentModelFilePath
                                   ?? Component.SwDocument?.Path
                                   ?? string.Empty;

        /// <summary>Есть ли у компонента чертёж (файл с тем же именем и расширением .slddrw).
        /// При сохранении он будет скопирован автоматически вместе с моделью и получит новое имя.</summary>
        public bool HasDrawing
        {
            get
            {
                if (string.IsNullOrEmpty(FilePath)) return false;
                var drawPath = Path.ChangeExtension(FilePath, ".slddrw");
                return !string.IsNullOrEmpty(drawPath) && File.Exists(drawPath);
            }
        }

        #endregion

        #region SaveAsName ("Сохранить как")

        private string _saveAsName;

        /// <summary>Имя файла без расширения, под которым компонент будет сохранён. По умолчанию
        /// совпадает с текущим именем файла. Редактирование пользователем сбрасывает PartNumber -
        /// тем самым пользователь сигнализирует, что это уже другая, изменённая деталь, для которой
        /// при сохранении нужно будет сгенерировать новый партномер и создать новую запись в БД.</summary>
        public string SaveAsName
        {
            get => _saveAsName;
            set
            {
                value ??= string.Empty;
                if (_saveAsName == value) return;

                _saveAsName = value;
                OnPropertyChanged(nameof(SaveAsName));

                // См. п.2 ТЗ: "Если это поле редактируется, то поле partnumber
                // того же компонента сбрасывается в null."
                ClearPartNumber();
            }
        }

        #endregion

        #region PartNumber / IsNewPartNumber

        private string _partNumber;

        /// <summary>Текущий партномер детали/сборки - значение, которое диалог планирует записать
        /// в КОПИЮ файла при сохранении (и, если он новый, под которым будет создана запись Component
        /// в БД). Это НЕ прямое зеркало Component.PartNumber - исходный (открытый) документ этот
        /// диалог не трогает вообще.</summary>
        public string PartNumber => _partNumber;

        /// <summary>True, если партномер пуст - строка подсвечивается в таблице светло-зелёным,
        /// а при сохранении для этого компонента будет сгенерирован новый партномер.</summary>
        public bool IsNewPartNumber => string.IsNullOrWhiteSpace(_partNumber);

        /// <summary>
        /// Помечает компонент как "новый" (партномер очищается, при сохранении будет сгенерирован
        /// заново). Вызывается и из контекстного меню ("Новый" по ПКМ на ячейке PartNumber), и из
        /// сеттера SaveAsName. Если значение действительно изменилось (было непустым), поднимает
        /// событие PartNumberCleared - по нему владелец диалога (AGR_PackNGoVM) каскадно помечает
        /// как новые все вышестоящие по дереву сборки.
        /// </summary>
        public void ClearPartNumber()
        {
            var wasEmpty = IsNewPartNumber;

            if (!string.IsNullOrEmpty(_partNumber))
            {
                _partNumber = string.Empty;
                OnPropertyChanged(nameof(PartNumber));
                OnPropertyChanged(nameof(IsNewPartNumber));
            }

            if (!wasEmpty)
            {
                PartNumberCleared?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Фиксирует сгенерированный при сохранении партномер (после того как для него
        /// уже создана запись Component в БД). Меняет только состояние диалога - в исходный
        /// открытый документ ничего не пишется; фактическая запись свойства произойдёт в
        /// уже СКОПИРОВАННЫЙ файл, см. AGR_PackNGoVM.ApplyPartNumbersToCopy.</summary>
        public void ApplyGeneratedPartNumber(string newPartNumber)
        {
            _partNumber = newPartNumber ?? string.Empty;
            OnPropertyChanged(nameof(PartNumber));
            OnPropertyChanged(nameof(IsNewPartNumber));
        }

        /// <summary>Срабатывает, когда PartNumber переходит из непустого состояния в пустое.
        /// Используется AGR_PackNGoVM для каскадного распространения "новизны" на все сборки,
        /// в которые прямо или косвенно входит этот компонент.</summary>
        public event EventHandler? PartNumberCleared;

        #region SetNewCommand (пункт "Новый" в контекстном меню ячейки PartNumber)

        private ICommand _setNewCommand;
        public ICommand SetNewCommand => _setNewCommand ??= new RelayCommand(_ => ClearPartNumber());

        #endregion

        #endregion

        #region IsChecked ("копировать этот компонент")

        private bool _isChecked = true;

        /// <summary>Признак "включить в копирование". Общий для всех вхождений одного файла в дереве -
        /// если один и тот же файл встречается в сборке дважды, копируется он всё равно один раз,
        /// поэтому логично, чтобы флаг был общим для всех строк с этим компонентом.</summary>
        public bool IsChecked
        {
            get => _isChecked;
            set => Set(ref _isChecked, value);
        }

        #endregion
    }
}