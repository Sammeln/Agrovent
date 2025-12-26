// File: Services/AGR_CommandService.cs
using Agrovent.DAL.Entities.Components; // Если нужно для AvaArticle
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Extensions; // Для AGR_TryGetProp и т.д.
using Agrovent.Infrastructure.Interfaces;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.ViewModels.Base;
using Microsoft.Extensions.Logging;
using System.Threading;
using Xarial.XCad.Data;
using Xarial.XCad.Documents;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.Services
{
    public class AGR_CommandService : IAGR_CommandService
    {
        private readonly ILogger<AGR_CommandService> _logger;
        private readonly IAGR_ComponentVersionService _componentVersionService; // Возможно, нужен для AvaArticle

        public AGR_CommandService(ILogger<AGR_CommandService> logger, IAGR_ComponentVersionService componentVersionService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _componentVersionService = componentVersionService ?? throw new ArgumentNullException(nameof(componentVersionService));
        }

        public async Task<bool> UpdatePropertiesAsync()
        {
            return false;
            //if (iComponent == null)
            //{
            //    _logger.LogWarning("UpdatePropertiesAsync called with null document.");
            //    return false;
            //}
            //var component = iComponent as AGR_BaseComponent;
            //var config = component.mConfiguration;
            //var props = config.Properties;

            //try
            //{
            //    _logger.LogDebug($"Starting property update for document: {component.mDocument.Title}");

                // 1. Обновление базовых свойств (Наименование, Обозначение, Признак, Расширение, Путь файла)
                // Эти свойства, скорее всего, управляются извне или из базы данных.
                // Предположим, что их значения нужно получить из соответствующего ViewModel или базы данных.
                // Пример (псевдокод - нужно интегрировать с логикой получения актуальных значов):
                // var componentData = await _componentVersionService.GetComponentByPartNumber(...);
                // props[AGR_PropertyNames.Name].Value = componentData.Name;
                // props[AGR_PropertyNames.Partnumber].Value = componentData.PartNumber;
                // props[AGR_PropertyNames.AvaType].Value = componentData.AvaType.ToString();
                // props[AGR_PropertyNames.Extension].Value = Path.GetExtension(document.Path);
                // props[AGR_PropertyNames.FilePath].Value = document.Path; // Не рекомендуется хранить путь в файле, но как пример

                // 2. Обновление массы
                //await UpdateMassPropertyAsync(config);

                // 3. Обновление специфических свойств для деталей
                //if (document is ISwPart)
                //{
                //    await UpdatePartSpecificPropertiesAsync(document, config);
                //}
                //else if (document is ISwAssembly)
                //{
                //    // Для сборок может быть логика обновления AvaArticle на основе дочерних компонентов
                //    // Это сложнее и требует отдельного обсуждения/реализации
                //    // await UpdateAssemblyAvaArticleAsync(document, props);
                //}

                // 4. Подтверждение изменений свойств
                //foreach (var prop in props)
                //{
                //    if (!prop.IsCommitted)
                //    {
                //        await prop.Commit(CancellationToken.None);
                //    }
                //}

            //    _logger.LogDebug($"Successfully updated properties for document: {component.mDocument.Title}");
            //    return true;
            //}
            //catch (Exception ex)
            //{
            //    _logger.LogError(ex, $"Error updating properties for document: {component.mDocument.Title}");
            //    return false;
            //}
        }

        private async Task UpdateMassPropertyAsync(ISwConfiguration configuration)
        {
            ISwDocument3D document3D = configuration.OwnerDocument as ISwDocument3D;

            var massProp = configuration.Properties.GetOrPreCreate(AGR_PropertyNames.BlankMass); // Или другое имя для итоговой массы

            if (massProp != null)
            {
                var massPrp = document3D.Evaluation.PreCreateMassProperty();
                if (!massPrp.IsCommitted) massPrp.Commit(CancellationToken.None);
                var mass = massPrp.Mass; // Получаем массу в кг

                // Форматирование значения массы (например, 2 знака после запятой)
                massProp.Value = Math.Round(mass, 3).ToString(); // SolidWorks свойства обычно строковые
                _logger.LogDebug($"Updated mass property to: {mass} kg");
            }
            else
            {
                _logger.LogWarning($"Could not access SolidWorks specific property for mass update in document: {document3D.Title}");
            }
        }

        private async Task UpdatePartSpecificPropertiesAsync(ISwDocument3D document, IXConfiguration configuration)
        {
            // Пример: обновление длины/ширины/толщины заготовки для листовых деталей
            // Эта логика может быть сложной и зависеть от геометрии.
            // Псевдокод:
            /*
            if (IsSheetMetalPart(document)) // Нужно реализовать метод определения листовой детали
            {
                var length = GetSheetMetalLength(document); // Нужно реализовать
                var width = GetSheetMetalWidth(document);  // Нужно реализовать
                var thickness = GetSheetMetalThickness(document); // Используем существующий метод или получаем из геометрии

                var props = configuration.Properties;
                var lengthProp = props.GetOrPreCreate(AGR_PropertyNames.BlankLen);
                var widthProp = props.GetOrPreCreate(AGR_PropertyNames.BlankWid);
                var thickProp = props.GetOrPreCreate(AGR_PropertyNames.BlankThick);

                lengthProp.Value = length.ToString();
                widthProp.Value = width.ToString();
                thickProp.Value = thickness.ToString();

                await lengthProp.Commit(CancellationToken.None);
                await widthProp.Commit(CancellationToken.None);
                await thickProp.Commit(CancellationToken.None);

                _logger.LogDebug($"Updated sheet metal properties for: {document.Title}");
            }
            */
            // Для обычных деталей, возможно, только масса обновляется в этом методе.
        }

        // Пример метода для обновления AvaArticle в сборке (псевдокод)
        /*
        private async Task UpdateAssemblyAvaArticleAsync(ISwDocument3D document, IXPropertiesCollection properties)
        {
            // Получить AvaArticle из дочерних компонентов (например, через AvaModel)
            // Это требует обхода структуры сборки и логики агрегации
            // string avaArticle = AggregateAvaArticleFromChildren(document);
            // var avaProp = properties.GetOrPreCreate(AGR_PropertyNames.AvaArticle); // Предполагаем, что такое свойство есть
            // avaProp.Value = avaArticle;
            // await avaProp.Commit(CancellationToken.None);
        }
        */

        // Пример метода определения листовой детали (псевдокод)
        /*
        private bool IsSheetMetalPart(ISwDocument3D document)
        {
            // Использовать API SolidWorks для проверки наличия листовых библиотечных элементов
            // var swPart = document as ISwPart;
            // var swApp = swPart.Sw;
            // var feat = swPart.IGetFirstFeature();
            // ... анализ features ...
            return false; // Пока заглушка
        }
        */
    }
}