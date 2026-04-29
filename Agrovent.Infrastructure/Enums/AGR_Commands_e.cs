using Xarial.XCad.Base.Attributes;
using Xarial.XCad.UI.Commands.Attributes;
using Xarial.XCad.UI.Commands.Enums;

namespace Agrovent.Infrastructure.Enums
{
    public enum AGR_Commands_e
    {
        [Title("Информация / сохранить")]
        [CommandItemInfo(true, true, WorkspaceTypes_e.Assembly | WorkspaceTypes_e.Part, true, RibbonTabTextDisplay_e.TextBelow)]
        SaveComponent,

        [Title("Экспорт в Iges")]
        ExportToIges,

        [Title("Обновить свойства")]
        UpdateProperties,

        //[Title("Реестр КД")]
        //ComponentRegistry,

        [Title("Проводник проектов")]
        ProjectsExplorer,

        //1993
        [Title("Переместить компонент")]
        [CommandItemInfo(true, true, WorkspaceTypes_e.Assembly)]
        MoveComponentWithTriade,
        
        [Title("Тестовая команда")]
        TestCommand
    }
}