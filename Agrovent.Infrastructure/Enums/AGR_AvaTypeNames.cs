using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroventInfrastructure.Enums
{
    public static class AGR_AvaTypeNames
    {
        public const string Production = "Продукция";
        public const string Component = "Комплектующие";
        public const string Purchased = "Товар";
        public const string VirtualComponent = "Постоянная часть";
        public const string DontBuy = "(Не закупать)";
        public const string NA = "Прочее";
        public const string AllTypes = "Все типы";
    }

    public static class AGR_ComponentTypeNames
    {
        public const string Assembly = "Сборочные единицы";
        public const string Part = "Детали";
        public const string SheetMetallPart = "Листовые детали";
        public const string Purchased = "Покупное";
        public const string NA = "Прочее";
        public const string AllTypes = "Все типы";
    }
}
