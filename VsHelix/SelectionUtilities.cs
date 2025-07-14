using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Utilities;

namespace VsHelix
{
	internal static class SelectionUtilities
	{
		/// <summary>
		/// Returns the span covered by a selection, expanding empty selections to one character when possible.
		/// </summary>
		public static SnapshotSpan GetEffectiveSpan(Selection sel, ITextSnapshot snapshot)
		{
			if (!sel.IsEmpty)
			{
				return new SnapshotSpan(sel.Start.Position, sel.End.Position);
			}

			int pos = sel.ActivePoint.Position.Position;
			if (pos < snapshot.Length)
			{
				return new SnapshotSpan(snapshot, pos, 1);
			}
			return new SnapshotSpan(snapshot, pos, 0);
		}
	}
}
