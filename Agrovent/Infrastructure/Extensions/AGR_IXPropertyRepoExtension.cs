using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Xarial.XCad.Base;
using Xarial.XCad.Data;

namespace Agrovent.Infrastructure.Extensions
{
    public static class AGR_IXPropertyRepoExtension
    {
        public static IXProperty? AGR_TryGetProp(this IXPropertyRepository repo, string propertyName)
        {
            if (repo is null) return null;
            try
            {
                IXProperty xProperty = repo.GetOrPreCreate(propertyName);
                if (!xProperty.IsCommitted) xProperty.Commit(CancellationToken.None);
                return xProperty;
            }
            catch (Exception)
            {
                //MessageBox.Show(ex.Message.ToString());
                throw;
            }
        }

        /// <summary>Значение свойства или null, если свойства нет. В отличие от AGR_TryGetProp не создаёт свойство.</summary>
        public static string? AGR_TryReadProp(this IXPropertyRepository repo, string propertyName)
        {
            if (repo is null) return null;
            return repo.TryGet(propertyName, out var prop) ? prop.Value?.ToString() : null;
        }

        /// <summary>
        /// «Обозначение» (этап 6.4): живёт на вкладке Custom файла (её читает и add-in развёрток).
        /// До миграции файлов оно может быть только в конфигурации - тогда берём из активной конфигурации.
        /// </summary>
        public static string AGR_ReadPartNumber(this Xarial.XCad.Documents.IXDocument3D doc)
        {
            var fileLevel = doc.Properties.AGR_TryReadProp(AgroventInfrastructure.Enums.AGR_PropertyNames.Partnumber);
            if (!string.IsNullOrWhiteSpace(fileLevel)) return fileLevel.Trim();
            return doc.Configurations.Active?.Properties.AGR_TryReadProp(AgroventInfrastructure.Enums.AGR_PropertyNames.Partnumber)?.Trim() ?? "";
        }
    }
}
