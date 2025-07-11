using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Extensibility.Editor;
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
					// Save the current selections before entering insert mode.
					SelectionManager.Instance.SaveSelections(broker);

					// For the 'insert' command, we move the caret to the start of each selection.
					broker.PerformActionOnAllSelections(selection =>
					{
						var targetPoint = selection.Selection.Start;
						// Move the caret to the start of the selection, collapsing it.
						selection.MoveTo(targetPoint, false, PositionAffinity.Successor);
					});
					ModeManager.Instance.EnterInsert();
					return true;

				case 'a':
					// Save the current selections before entering insert mode.
					SelectionManager.Instance.SaveSelections(broker);

					broker.PerformActionOnAllSelections(selection =>
					{
						// For the 'append' command, we determine the target position for each caret.
						var targetPoint = selection.Selection.End;

						// If the selection is empty (it's a caret), we need to move one character to the right.
						if (selection.Selection.IsEmpty)
						{
							var currentPos = targetPoint.Position;
							var line = currentPos.GetContainingLine();

							// We only move the caret if it's not at the end of the line's content.
							// If it is at the end, appending should start right there.
							if (currentPos < line.End)
							{
								targetPoint = new VirtualSnapshotPoint(currentPos + 1);
							}
						}

						// MoveTo() collapses the current selection and moves the caret to the specified point.
						// We use the overload that takes a boolean to indicate we are NOT extending the selection,
						// which effectively collapses it at the new target point.
						selection.MoveTo(targetPoint, false, PositionAffinity.Successor);
					});
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
                                                selection.PerformAction(PredefinedSelectionTransformations.ClearSelection);
                                                selection.PerformAction(PredefinedSelectionTransformations.MoveToPreviousCaretPosition);
                                        });
                                        return true;

                                case 'j':
                                        broker.PerformActionOnAllSelections(selection =>
                                        {
                                                selection.PerformAction(PredefinedSelectionTransformations.ClearSelection);
                                                selection.PerformAction(PredefinedSelectionTransformations.MoveToNextLine);
                                        });
                                        return true;

                                case 'k':
                                        broker.PerformActionOnAllSelections(selection =>
                                        {
                                                selection.PerformAction(PredefinedSelectionTransformations.ClearSelection);
                                                selection.PerformAction(PredefinedSelectionTransformations.MoveToPreviousLine);
                                        });
                                        return true;

                                case 'l':
                                        broker.PerformActionOnAllSelections(selection =>
                                        {
                                                selection.PerformAction(PredefinedSelectionTransformations.ClearSelection);
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

				case 'd':
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
					// After the edit is applied, the selections are automatically collapsed
					// at the start of the deleted region by the editor. No further action is needed.
					return true;

				case 'c':
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
					// After the edit is applied, the selections are automatically collapsed
					// at the start of the deleted region by the editor.
					ModeManager.Instance.EnterInsert();
					return true;

			}

			return true;
		}
	}
}