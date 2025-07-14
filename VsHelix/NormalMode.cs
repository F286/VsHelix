using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Text;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Operations;
using VsHelix;

namespace VsHelix
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

				case 'o':
					AddLine(view, broker, operations, above: false);
					ModeManager.Instance.EnterInsert();
					return true;

				case 'O':
					AddLine(view, broker, operations, above: true);
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
					{
						var snapshot = view.TextBuffer.CurrentSnapshot;
						var currentSelections = broker.AllSelections.ToList();
						var newSelections = new List<Microsoft.VisualStudio.Text.Selection>();

						foreach (var s in currentSelections)
						{
							var startLine = s.Start.Position.GetContainingLine();
							var endLine = s.End.Position.GetContainingLine();

							bool includesBreak = s.Start.Position == startLine.Start &&
									s.End.Position == endLine.EndIncludingLineBreak;

							var newStart = new VirtualSnapshotPoint(new SnapshotPoint(snapshot, startLine.Start.Position));
							VirtualSnapshotPoint newEnd;

							if (!includesBreak)
							{
								newEnd = new VirtualSnapshotPoint(new SnapshotPoint(snapshot, endLine.EndIncludingLineBreak.Position));
							}
							else if (endLine.LineNumber + 1 < snapshot.LineCount)
							{
								var nextLine = snapshot.GetLineFromLineNumber(endLine.LineNumber + 1);
								newEnd = new VirtualSnapshotPoint(new SnapshotPoint(snapshot, nextLine.EndIncludingLineBreak.Position));
							}
							else
							{
								newEnd = new VirtualSnapshotPoint(new SnapshotPoint(snapshot, endLine.EndIncludingLineBreak.Position));
							}

							var span = new VirtualSnapshotSpan(newStart, newEnd);
							newSelections.Add(new Microsoft.VisualStudio.Text.Selection(span, s.IsReversed));
						}

						if (newSelections.Any())
						{
							broker.ClearSecondarySelections();
							var first = newSelections.First();
							view.Selection.Select(new SnapshotSpan(first.Start.Position, first.End.Position), first.IsReversed);
							foreach (var sel in newSelections.Skip(1))
							{
								broker.AddSelection(sel);
							}
						}
						return true;
					}

				case 'y':
					YankSelections(view, broker);
					return true;

				case 'd':
					YankSelections(view, broker);
					DeleteSelection(view, broker);
					// After the edit is applied, the selections are automatically collapsed
					// at the start of the deleted region by the editor. No further action is needed.
					return true;

				case 'c':
					YankSelections(view, broker);
					DeleteSelection(view, broker);
					// After the edit is applied, the selections are automatically collapsed
					// at the start of the deleted region by the editor.
					ModeManager.Instance.EnterInsert();
					return true;

				case 'p':
					PasteFromClipboard(view, broker);
					return true;

				case 'C':
					AddCaretBelowLastSelection(view, broker);
					return true;

				case ',':
					broker.ClearSecondarySelections();
					return true;
			}

			return true;
		}

		private void AddCaretBelowLastSelection(ITextView view, IMultiSelectionBroker broker)
		{
			// Find the bottom-most selection
			var bottomSelection = broker.AllSelections
				.OrderByDescending(s => s.End.Position.GetContainingLine().LineNumber)
				.ThenByDescending(s => s.End.Position.Position)
				.FirstOrDefault();

			if (bottomSelection == null)
			{
				return;
			}

			ITextSnapshot snapshot = view.TextSnapshot;
			var startPoint = bottomSelection.Start;
			var endPoint = bottomSelection.End;

			var startLine = startPoint.Position.GetContainingLine();
			var endLine = endPoint.Position.GetContainingLine();

			if (startLine.LineNumber != endLine.LineNumber)
			{
				// For now, only handle single-line selections.
				return;
			}

			// Get the text from the original line
			string originalLineText = startLine.GetText();
			// Calculate tab-expanded text lengths
			string expandedText = originalLineText.Replace("\t", new string(' ', view.Options.GetTabSize()));

			// Calculate visual offsets (accounting for tabs)
			int startOffset = CalculateExpandedOffset(
				originalLineText.Substring(0, startPoint.Position - startLine.Start),
				view.Options.GetTabSize());

			int endOffset = CalculateExpandedOffset(
				originalLineText.Substring(0, endPoint.Position - startLine.Start),
				view.Options.GetTabSize());

			// Add virtual spaces
			startOffset += startPoint.VirtualSpaces;
			endOffset += endPoint.VirtualSpaces;

			// Find a suitable line below that has enough length
			int nextLineNumber = endLine.LineNumber + 1;
			ITextSnapshotLine nextLine = null;
			int maxNeededOffset = Math.Max(startOffset, endOffset);

			while (nextLineNumber < snapshot.LineCount)
			{
				var candidateLine = snapshot.GetLineFromLineNumber(nextLineNumber);
				string candidateText = candidateLine.GetText();

				// Calculate the expanded length of this line
				int expandedLineLength = CalculateExpandedOffset(candidateText, view.Options.GetTabSize());

				// Check if this line has enough length to accommodate our selection
				if (expandedLineLength >= maxNeededOffset)
				{
					nextLine = candidateLine;
					break;
				}

				// Line is too short, try the next one
				nextLineNumber++;
			}

			if (nextLine == null)
			{
				return; // No suitable line found
			}

			// Create positions on the next line at the same visual offsets
			var newStartPoint = CreatePointAtVisualOffset(nextLine, startOffset, view.Options.GetTabSize());
			var newEndPoint = CreatePointAtVisualOffset(nextLine, endOffset, view.Options.GetTabSize());

			// Create new selection with the same direction as the original
			var newSelection = bottomSelection.IsReversed
				? new Microsoft.VisualStudio.Text.Selection(newEndPoint, newStartPoint)
				: new Microsoft.VisualStudio.Text.Selection(newStartPoint, newEndPoint);

			broker.AddSelection(newSelection);
		}

		/// <summary>
		/// Calculates the expanded length of text after replacing tabs with spaces.
		/// </summary>
		private int CalculateExpandedOffset(string text, int tabSize)
		{
			int expandedLength = 0;
			foreach (char c in text)
			{
				if (c == '\t')
				{
					// Calculate how many spaces this tab represents
					int spacesForTab = tabSize - (expandedLength % tabSize);
					expandedLength += spacesForTab;
				}
				else
				{
					expandedLength++;
				}
			}
			return expandedLength;
		}

		/// <summary>
		/// Creates a virtual point at a specific visual offset on a line, accounting for tabs.
		/// </summary>
		private VirtualSnapshotPoint CreatePointAtVisualOffset(ITextSnapshotLine line, int visualOffset, int tabSize)
		{
			string lineText = line.GetText();
			int currentVisualOffset = 0;
			int charOffset = 0;

			// Calculate which character position corresponds to the visual offset
			while (charOffset < lineText.Length && currentVisualOffset < visualOffset)
			{
				if (lineText[charOffset] == '\t')
				{
					int spacesForTab = tabSize - (currentVisualOffset % tabSize);
					currentVisualOffset += spacesForTab;
				}
				else
				{
					currentVisualOffset++;
				}

				if (currentVisualOffset <= visualOffset)
				{
					charOffset++;
				}
			}

			// Calculate virtual spaces needed
			int virtualSpaces = visualOffset - currentVisualOffset;
			if (virtualSpaces < 0) virtualSpaces = 0;

			// Create the point at the appropriate position
			var snapshotPoint = new SnapshotPoint(line.Snapshot, line.Start.Position + charOffset);
			return new VirtualSnapshotPoint(snapshotPoint, virtualSpaces);
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

						// If the span exactly covers one or more whole lines,
						// extend it to include the trailing line break of the last line.
						var startLine = spanToDelete.Start.GetContainingLine();
						var endLine = spanToDelete.End.GetContainingLine();
						if (spanToDelete.Start == startLine.Start && spanToDelete.End == endLine.End)
						{
							spanToDelete = new SnapshotSpan(spanToDelete.Start, endLine.EndIncludingLineBreak);
						}

						edit.Delete(spanToDelete);
					}
				});
				// Apply all queued deletions to the buffer.
				edit.Apply();
			}
		}

		/// <summary>
		/// Copies the current selections to the clipboard.
		/// </summary>
		private void YankSelections(ITextView view, IMultiSelectionBroker broker)
		{
			var snapshot = view.TextSnapshot;
			var selections = broker.AllSelections.ToList();
			if (selections.Count == 0)
				return;

			var builder = new StringBuilder();
			for (int i = 0; i < selections.Count; i++)
			{
				var sel = selections[i];
				var span = new SnapshotSpan(sel.Start.Position, sel.End.Position);
				builder.Append(span.GetText());
				if (i < selections.Count - 1)
					builder.AppendLine();
			}

			Clipboard.SetText(builder.ToString());
		}

		/// <summary>
		/// Pastes clipboard text after each selection.
		/// Inserts on a new line when the text ends with a newline.
		/// </summary>
		private void PasteFromClipboard(ITextView view, IMultiSelectionBroker broker)
		{
			if (!Clipboard.ContainsText())
				return;

			string text = Clipboard.GetText();
			bool linewise = text.EndsWith("\r\n") || text.EndsWith("\n");

			using (var edit = view.TextBuffer.CreateEdit())
			{
				broker.PerformActionOnAllSelections(sel =>
				{
					var point = sel.Selection.End.Position;
					if (linewise)
					{
						var line = point.GetContainingLine();
						point = line.EndIncludingLineBreak;
					}
					edit.Insert(point.Position, text);
				});

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

		/// <summary>
		/// Inserts a new blank line above or below each selection and moves the caret to it.
		/// </summary>
		/// <param name="view">The text view.</param>
		/// <param name="broker">The multi-selection broker.</param>
		/// <param name="above">True to insert above, false for below.</param>
		private void AddLine(ITextView view, IMultiSelectionBroker broker, IEditorOperations operations, bool above)
		{
			var snapshot = view.TextBuffer.CurrentSnapshot;
			var insertionPoints = new List<ITrackingPoint>();
			var indents = new List<string>();

			broker.PerformActionOnAllSelections(selection =>
			{
				var line = selection.Selection.ActivePoint.Position.GetContainingLine();
				var lineText = line.GetText();
				var indent = lineText.Substring(0, lineText.Length - lineText.TrimStart().Length);
				indents.Add(indent);

				int pos = above ? line.Start.Position : line.End.Position;
				var trackingMode = PointTrackingMode.Negative;
				insertionPoints.Add(snapshot.CreateTrackingPoint(pos, trackingMode));
			});

			using (var edit = view.TextBuffer.CreateEdit())
			{
				int i = 0;
				foreach (var tp in insertionPoints)
				{
					var indent = indents[i++];
					var text = above ? indent + Environment.NewLine : Environment.NewLine + indent;
					edit.Insert(tp.GetPosition(snapshot), text);
				}
				edit.Apply();
			}

			var newSnapshot = view.TextBuffer.CurrentSnapshot;
			int index = 0;
			broker.PerformActionOnAllSelections(selection =>
			{
				var pos = insertionPoints[index].GetPosition(newSnapshot);
				var indent = indents[index];
				index++;
				var offset = above ? indent.Length : Environment.NewLine.Length + indent.Length;
				selection.MoveTo(new VirtualSnapshotPoint(new SnapshotPoint(newSnapshot, pos + offset)), false, PositionAffinity.Successor);
			});
		}
	}
}