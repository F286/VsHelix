using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace VsHelix
{
	internal static class StatusBarHelper
	{
		public static void ShowMode(ModeManager.EditorMode mode, string extra = "")
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var status = ServiceProvider.GlobalProvider.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
			var text = mode switch
			{
				ModeManager.EditorMode.Normal => "NOR",
				ModeManager.EditorMode.Insert => "INS",
				ModeManager.EditorMode.Visual => "VIS",
				ModeManager.EditorMode.Search => "SCH",
				ModeManager.EditorMode.Goto => "GTO",
				_ => mode.ToString().ToUpperInvariant(),
			};
			if (!string.IsNullOrEmpty(extra))
				status?.SetText($"{text} {extra}");
			else
				status?.SetText($"{text} ");
		}
	}
}
