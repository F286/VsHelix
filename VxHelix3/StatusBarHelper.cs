using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace VxHelix3
{
    internal static class StatusBarHelper
    {
        public static void ShowMode(ModeManager.EditorMode mode)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var status = ServiceProvider.GlobalProvider.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
            var text = mode switch
            {
                ModeManager.EditorMode.Normal => "NOR",
                ModeManager.EditorMode.Insert => "INS",
                _ => mode.ToString().ToUpperInvariant(),
            };
            status?.SetText($"{text} ");
        }
    }
}
