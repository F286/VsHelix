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
using System.Text.Json; // For JSON serialization

namespace VsHelix
{
	/// <summary>
	/// Record type for yanked items (replaces tuple for reliable JSON serialization).
	/// </summary>
	internal record YankItem(string Text, bool IsLinewise);

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
					// Use PerformActionOnAllSelections to modify selections in place.
					// This is less disruptive and helps preserve caret context better than
					// clearing and re-adding selections.
					broker.PerformActionOnAllSelections(transformer =>
					{
						var snapshot = view.TextSnapshot;
						var currentSelection = transformer.Selection;
						var startLine = currentSelection.Start.Position.GetContainingLine();
						var endLine = currentSelection.End.Position.GetContainingLine();

						// A linewise selection is now defined as covering the line's content,
						// but NOT the line break. This prevents adjacent selections from merging.
						bool isAlreadyLinewise = currentSelection.Start.Position == startLine.Start &&
													 currentSelection.End.Position == endLine.End;

						VirtualSnapshotPoint newStart, newEnd;

						if (!isAlreadyLinewise)
						{
							// Select the current line(s) fully, up to the end of the text content.
							newStart = new VirtualSnapshotPoint(startLine.Start);
							newEnd = new VirtualSnapshotPoint(endLine.End);
						}
						else
						{
							// If it's already a line selection, extend to the next line.
							if (endLine.LineNumber + 1 < snapshot.LineCount)
							{
								var nextLine = snapshot.GetLineFromLineNumber(endLine.LineNumber + 1);
								newStart = new VirtualSnapshotPoint(startLine.Start);
								newEnd = new VirtualSnapshotPoint(nextLine.End);
							}
							else
							{
								// Already at the last line, do not extend further.
								newStart = new VirtualSnapshotPoint(startLine.Start);
								newEnd = new VirtualSnapshotPoint(endLine.End);
							}
						}

						// Create the new selection span, preserving the original direction.
						var newSpan = new VirtualSnapshotSpan(newStart, newEnd);

						// Use MoveTo to set the new selection span.
						transformer.MoveTo(newSpan.Start, false, PositionAffinity.Successor);
						transformer.MoveTo(newSpan.End, true, PositionAffinity.Successor);
					});
					return true;

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
					Paste(view, broker);
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
			// A stable list of selections is needed before modification.
			var selections = broker.AllSelections.ToList();
			if (!selections.Any()) return;

			using (var edit = view.TextBuffer.CreateEdit())
			{
				// Iterate over the stable list. Order doesn't strictly matter since
				// all deletions are based on the snapshot before the edit is applied,
				// but reverse is a safe pattern.
				foreach (var sel in selections.OrderByDescending(s => s.Start.Position))
				{
					if (sel.IsEmpty) continue;

					var spanToDelete = new SnapshotSpan(sel.Start.Position, sel.End.Position);

					// If the selection is linewise (based on our new definition),
					// we must extend the span to include the line break for a clean delete.
					if (IsLinewiseSelection(sel, view.TextSnapshot))
					{
						var endLine = sel.End.Position.GetContainingLine();
						// Only extend if there is a line break to include.
						if (endLine.End.Position < endLine.EndIncludingLineBreak.Position)
						{
							spanToDelete = new SnapshotSpan(sel.Start.Position, endLine.EndIncludingLineBreak);
						}
					}
					edit.Delete(spanToDelete);
				}
				edit.Apply();
			}

			// After deletion, the editor collapses selections. We should ensure they are
			// collapsed to a zero-width caret at the start of the deleted region.
			broker.PerformActionOnAllSelections(transformer =>
			{
				// Collapse to start: move both anchor and active to the start point.
				var start = transformer.Selection.Start;
				transformer.MoveTo(start, false, PositionAffinity.Successor);
			});
		}

		/// <summary>
		/// Determines if a selection is linewise.
		/// UPDATED: A linewise selection now spans from the exact start of a line to the
		/// exact end of its content (excluding the line break). This prevents adjacent
		/// linewise selections from being merged by the editor.
		/// </summary>
		private bool IsLinewiseSelection(Microsoft.VisualStudio.Text.Selection s, ITextSnapshot snapshot)
		{
			if (s.IsEmpty || s.Start.IsInVirtualSpace || s.End.IsInVirtualSpace)
			{
				return false;
			}

			var startLine = s.Start.Position.GetContainingLine();
			var endLine = s.End.Position.GetContainingLine();

			// The new definition for a linewise selection.
			return s.Start.Position == startLine.Start && s.End.Position == endLine.End;
		}

		/// <summary>
		/// Yanks the current selections to the internal register and system clipboard.
		/// </summary>
		private void YankSelections(ITextView view, IMultiSelectionBroker broker)
		{
			var snapshot = view.TextSnapshot;
			var selections = broker.AllSelections.ToList();
			if (selections.Count == 0)
				return;

			List<YankItem> yankRegister = new List<YankItem>();

			var concatenated = new StringBuilder();
			foreach (var sel in selections)
			{
				var span = new SnapshotSpan(sel.Start.Position, sel.End.Position);
				var text = span.GetText();
				var isLinewise = IsLinewiseSelection(sel, snapshot);

				// If the selection was linewise, we must add the newline back to the yanked text,
				// since our selection definition now excludes it.
				if (isLinewise)
				{
					text += Environment.NewLine;
				}

				yankRegister.Add(new YankItem(text, isLinewise));
				concatenated.Append(text);
			}

			// Prepare clean concatenated text for plain clipboard
			var clipboardText = concatenated.ToString();
			if (yankRegister.Any(r => r.IsLinewise) && !clipboardText.EndsWith(Environment.NewLine))
			{
				clipboardText += Environment.NewLine;
			}

			// Use DataObject for multiple formats
			var dataObject = new DataObject();
			dataObject.SetText(clipboardText); // Plain text for external apps

			// Custom format: Serialize the register as JSON
			string json = JsonSerializer.Serialize(yankRegister);
			dataObject.SetData("MyVsHelixYankFormat", json);

			try
			{
				Clipboard.SetDataObject(dataObject, true);
			}
			catch
			{
				// Clipboard operations can fail if another process is holding it.
				// It's good practice to wrap this in a try-catch block.
			}
		}

		/// <summary>
		/// Pastes from the internal register if available, or from clipboard with custom format fallback,
		/// or basic text fallback.
		/// </summary>
		private void Paste(ITextView view, IMultiSelectionBroker broker)
		{
			var currentSelections = broker.AllSelections.ToList();
			if (currentSelections.Count == 0)
				return;

			List<YankItem> items = null;

			// Check clipboard for custom format or plain text
			IDataObject dataObject = Clipboard.GetDataObject();
			if (dataObject != null)
			{
				if (dataObject.GetDataPresent("MyVsHelixYankFormat"))
				{
					// Deserialize custom data
					string json = (string)dataObject.GetData("MyVsHelixYankFormat");
					items = JsonSerializer.Deserialize<List<YankItem>>(json);
				}
				else if (dataObject.GetDataPresent(DataFormats.Text))
				{
					// Basic fallback: Treat as single item
					string text = (string)dataObject.GetData(DataFormats.Text);
					bool isLinewise = text.EndsWith(Environment.NewLine);
					items = new List<YankItem> { new YankItem(text, isLinewise) };
				}
			}

			if (items == null || items.Count == 0)
				return;

			// Perform the paste (distributed if multiple items)
			using (var edit = view.TextBuffer.CreateEdit())
			{
				for (int i = 0; i < currentSelections.Count; i++)
				{
					var sel = currentSelections[i];
					var yankIndex = i % items.Count; // Cycle if more selections than items
					var item = items[yankIndex];

					SnapshotPoint insertionPoint;
					if (item.IsLinewise)
					{
						var line = sel.End.Position.GetContainingLine();
						insertionPoint = line.EndIncludingLineBreak; // New line below
					}
					else
					{
						insertionPoint = sel.End.Position; // Inline after
					}

					edit.Insert(insertionPoint.Position, item.Text);
				}
				edit.Apply();
			}

			// Optional: Move carets to end of pasted text (implement if desired)
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