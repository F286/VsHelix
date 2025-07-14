using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace VsHelix
{
	internal static class CaretHelper
	{
		public static void DisplayInsertCarets(ITextView view, IMultiSelectionBroker broker)
		{
foreach (var sel in broker.AllSelections)
{
view.Caret.MoveTo(sel.ActivePoint.Position, PositionAffinity.Successor);
}
view.Options.SetOptionValue(DefaultTextViewOptions.OverwriteModeId, false);
		}
	}
}
