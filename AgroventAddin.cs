
using System.Runtime.InteropServices;
using Xarial.XCad.SolidWorks;
using Xarial.XCad.UI.Commands;

namespace Agrovent
{
    [ComVisible(true)]
    public class AgroventAddin : SwAddInEx
    {
        public override void OnConnect()
        {
            CommandManager.AddCommandGroup<Commands_e>().CommandClick += OnCommandClickExecute;
            // Initialization code here
        }

        private void OnCommandClickExecute(Commands_e command)
        {
            switch (command)
            {
                case Commands_e.Command1:
                    // Handle Command1
                    break;
                case Commands_e.Command2:
                    // Handle Command2
                    break;
                
                default:
                    break;
            }
        }
        public override void OnDisconnect()
        {
            // Cleanup code here
        }
    }


    public enum Commands_e
    {
        Command1,
        Command2
    }

}
