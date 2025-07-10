using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;
using VxHelix3;

namespace VxHelix3
{
	/// <summary>
	/// Handles key input when in Normal mode.
	/// </summary>
	internal sealed class NormalMode : IInputMode
	{
		public bool Handle(TypeCharCommandArgs args, ITextView view, IMultiSelectionBroker broker, IEditorOperations operations)
		{
			switch (args.TypedChar)
			{
				case 'i':
					broker.PerformActionOnAllSelections(_ =>
					{
						view.Selection.Clear();
					});
					ModeManager.Instance.EnterInsert();
					return true;

				case 'a':
					broker.PerformActionOnAllSelections(_ =>
					{
						if (!view.Selection.IsEmpty)
							view.Caret.MoveTo(view.Selection.End);
						view.Selection.Clear();
					});
					ModeManager.Instance.EnterInsert();
					return true;

				case 'w':
					broker.PerformActionOnAllSelections(_ =>
					{
						if (!view.Selection.IsEmpty)
							view.Caret.MoveTo(view.Selection.End);
						view.Selection.Clear();
						operations.MoveToNextWord(true);
					});
					return true;

				case 'b':
					broker.PerformActionOnAllSelections(_ =>
					{
						if (!view.Selection.IsEmpty)
							view.Caret.MoveTo(view.Selection.Start);
						view.Selection.Clear();
						operations.MoveToPreviousWord(true);
					});
					return true;

				case 'd':
					using (var edit = view.TextBuffer.CreateEdit())
					{
						broker.PerformActionOnAllSelections(_ =>
						{
							if (!view.Selection.IsEmpty)
								edit.Delete(view.Selection.StreamSelectionSpan.SnapshotSpan);
						});
						edit.Apply();
					}
					broker.PerformActionOnAllSelections(_ => view.Selection.Clear());
					return true;

				case 'c':
					var starts = new List<SnapshotPoint>();
					using (var edit = view.TextBuffer.CreateEdit())
					{
						broker.PerformActionOnAllSelections(_ =>
						{
							if (!view.Selection.IsEmpty)
							{
								starts.Add(view.Selection.Start.Position);
								edit.Delete(view.Selection.StreamSelectionSpan.SnapshotSpan);
							}
							else
							{
								starts.Add(view.Caret.Position.BufferPosition);
							}
						});
						edit.Apply();
					}

					var currentSnapshot = view.TextBuffer.CurrentSnapshot;
					starts = starts.Select(p => p.TranslateTo(currentSnapshot, PointTrackingMode.Positive)).ToList();

					broker.ClearSecondarySelections();
					if (starts.Count > 0)
					{
						view.Caret.MoveTo(starts[0]);
						for (var i = 1; i < starts.Count; i++)
							broker.AddSelection(new Microsoft.VisualStudio.Text.Selection(
								new VirtualSnapshotPoint(starts[i]),
								new VirtualSnapshotPoint(starts[i])
							));
					}
					ModeManager.Instance.EnterInsert();
					return true;

				case (char)0x1b:
					return true;
			}

			return true;
		}
	}
}