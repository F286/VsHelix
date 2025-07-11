using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;
using System.Windows.Input;
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
					// Save the current selections before entering insert mode.
					SelectionManager.Instance.SaveSelections(broker);

					// For the 'insert' command, we move the caret to the start of each selection.
					broker.PerformActionOnAllSelections(selection => MoveCaretToSelectionStart(selection));
					ModeManager.Instance.EnterInsert();
					return true;

				case 'a':
					// Save the current selections before entering insert mode.
					SelectionManager.Instance.SaveSelections(broker);

					// For the 'append' command, move the caret to the end of the selection.
					broker.PerformActionOnAllSelections(selection => MoveCaretToSelectionEnd(selection));
					ModeManager.Instance.EnterInsert();
					return true;

				case 'w':
					broker.PerformActionOnAllSelections(selection =>
					{
						selection.PerformAction(PredefinedSelectionTransformations.ClearSelection);
						selection.PerformAction(PredefinedSelectionTransformations.SelectToNextSubWord);
					});
					return true;

				case 'h':
					broker.PerformActionOnAllSelections(selection =>
					{
						selection.PerformAction(PredefinedSelectionTransformations.MoveToPreviousCaretPosition);
					});
					return true;

				case 'j':
					broker.PerformActionOnAllSelections(selection =>
					{
						selection.PerformAction(PredefinedSelectionTransformations.MoveToNextLine);
					});
					return true;

				case 'k':
					broker.PerformActionOnAllSelections(selection =>
					{
						selection.PerformAction(PredefinedSelectionTransformations.MoveToPreviousLine);
					});
					return true;

				case 'l':
					broker.PerformActionOnAllSelections(selection =>
					{
						selection.PerformAction(PredefinedSelectionTransformations.MoveToNextCaretPosition);
					});
					return true;

				case 'W':
					broker.PerformActionOnAllSelections(selection =>
					{
						selection.PerformAction(PredefinedSelectionTransformations.ClearSelection);
						selection.PerformAction(PredefinedSelectionTransformations.SelectToNextWord);
					});
					return true;

				case 'b':
					broker.PerformActionOnAllSelections(selection =>
					{
						selection.PerformAction(PredefinedSelectionTransformations.ClearSelection);
						selection.PerformAction(PredefinedSelectionTransformations.SelectToPreviousSubWord);
					});
					return true;

				case 'B':
					broker.PerformActionOnAllSelections(selection =>
					{
						selection.PerformAction(PredefinedSelectionTransformations.ClearSelection);
						selection.PerformAction(PredefinedSelectionTransformations.SelectToPreviousWord);
					});
					return true;

				case 'x':
					broker.PerformActionOnAllSelections(selection =>
					{
						int length = selection.Selection.Extent.Length;
						selection.PerformAction(PredefinedSelectionTransformations.ExpandSelectionToEntireLine);
						if (length == selection.Selection.Extent.Length) // no change then..
						{
							selection.PerformAction(PredefinedSelectionTransformations.SelectToNextLine);
							selection.PerformAction(PredefinedSelectionTransformations.ExpandSelectionToEntireLine);
						}
						// TODO: Some slight bugs here on detecting if the currently selected line is 'fully selected' for some cases
					});
					return true;

				case 'd':
					DeleteSelection(view, broker);
					// After the edit is applied, the selections are automatically collapsed
					// at the start of the deleted region by the editor. No further action is needed.
					return true;

				case 'c':
					DeleteSelection(view, broker);
					// After the edit is applied, the selections are automatically collapsed
					// at the start of the deleted region by the editor.
					ModeManager.Instance.EnterInsert();
					return true;

				case 'C':
					// TODO: This should add an addional caret, on the line right below the lowest caret that exists for any selection
					return true;
			}

			return true;
		}

		/// <summary>
		/// Deletes the content of all non-empty selections.
		/// </summary>
		/// <param name="view">The text view.</param>
		/// <param name="broker">The multi-selection broker.</param>
		private void DeleteSelection(ITextView view, IMultiSelectionBroker broker)
		{
			// Create a single edit to group all deletions into one undo transaction.
			using (var edit = view.TextBuffer.CreateEdit())
			{
				// Iterate over each selection managed by the broker.
				broker.PerformActionOnAllSelections(transformer =>
				{
					var currentSelection = transformer.Selection;

					if (!currentSelection.IsEmpty)
					{
						// Create a new SnapshotSpan from the selection's start and end points.
						var spanToDelete = new SnapshotSpan(currentSelection.Start.Position, currentSelection.End.Position);
						edit.Delete(spanToDelete);
					}
				});
				// Apply all queued deletions to the buffer.
				edit.Apply();
			}
		}

		/// <summary>
		/// Moves the caret to the start of the selection, collapsing it.
		/// </summary>
		/// <param name="selection">The selection transformer for a single selection.</param>
		private void MoveCaretToSelectionStart(ISelectionTransformer selection)
		{
			var targetPoint = selection.Selection.Start;
			// Move the caret to the start of the selection, collapsing it.
			selection.MoveTo(targetPoint, false, PositionAffinity.Successor);
		}

		/// <summary>
		/// Moves the caret to the end of the selection, collapsing it.
		/// </summary>
		/// <param name="selection">The selection transformer for a single selection.</param>
		private void MoveCaretToSelectionEnd(ISelectionTransformer selection)
		{
			var targetPoint = selection.Selection.End;
			// Move the caret to the end of the selection, collapsing it.
			selection.MoveTo(targetPoint, false, PositionAffinity.Successor);
		}
	}
}