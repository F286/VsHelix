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
            status?.SetText($"-- {mode.ToString().ToLowerInvariant()} --");
        }
    }
}
