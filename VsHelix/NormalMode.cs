using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;  // For JSON serialization
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Operations;

namespace VsHelix
{
	/// <summary>
	/// Record type for yanked items (used for reliable JSON serialization of yank registers).
	/// </summary>
	internal record YankItem(string Text, bool IsLinewise);

	/// <summary>
	/// Handles key input when in Normal mode. 
	/// Refactored to use a key-to-command map (Helix-style) for maintainability.
	/// </summary>
	internal sealed class NormalMode : IInputMode
	{
		/// <summary>
		/// Delegate for handling a Normal mode command.
		/// </summary>
		private delegate bool CommandHandler(TypeCharCommandArgs args, ITextView view, IMultiSelectionBroker broker, IEditorOperations operations);

		// Map from typed character to its command handler.
		private readonly Dictionary<char, CommandHandler> _commandMap;

		public NormalMode()
		{
			_commandMap = new Dictionary<char, CommandHandler>();

			// ** Movement commands keymap (single-key movements) **
			var movementCommands = new Dictionary<char, Action<ISelectionTransformer>>
			{
				// Basic cursor movements

				// Move left
				['h'] = sel =>
				{
					if (sel.Selection.IsEmpty)
					{
						sel.PerformAction(PredefinedSelectionTransformations.MoveToPreviousCaretPosition);
					}
					else
					{
						if (sel.Selection.IsReversed)
						{
							sel.PerformAction(PredefinedSelectionTransformations.ClearSelection);
							sel.PerformAction(PredefinedSelectionTransformations.MoveToPreviousCaretPosition);
						}
						else
						{
							sel.PerformAction(PredefinedSelectionTransformations.ClearSelection);
							sel.PerformAction(PredefinedSelectionTransformations.MoveToPreviousCaretPosition);
							sel.PerformAction(PredefinedSelectionTransformations.MoveToPreviousCaretPosition);
						}
					}
				},
				// Move right
				['l'] = sel =>
				{
					if (sel.Selection.IsEmpty)
					{
						sel.PerformAction(PredefinedSelectionTransformations.MoveToNextCaretPosition);
					}
					else
					{
						if (sel.Selection.IsReversed)
						{
							sel.PerformAction(PredefinedSelectionTransformations.ClearSelection);
							sel.PerformAction(PredefinedSelectionTransformations.MoveToNextCaretPosition);
						}
						else
						{
							sel.PerformAction(PredefinedSelectionTransformations.ClearSelection);
						}
					}
				},
				['j'] = sel =>
				{
					if (sel.Selection.IsEmpty)
					{
						sel.PerformAction(PredefinedSelectionTransformations.MoveToNextLine);
					}
					else
					{
						if (sel.Selection.IsReversed)
						{
							sel.PerformAction(PredefinedSelectionTransformations.ClearSelection);
							sel.PerformAction(PredefinedSelectionTransformations.MoveToNextLine);
						}
						else
						{
							sel.PerformAction(PredefinedSelectionTransformations.ClearSelection);
							sel.PerformAction(PredefinedSelectionTransformations.MoveToPreviousCaretPosition);
							sel.PerformAction(PredefinedSelectionTransformations.MoveToNextLine);
						}
					}
				},
				['k'] = sel =>
				{
					if (sel.Selection.IsEmpty)
					{
						sel.PerformAction(PredefinedSelectionTransformations.MoveToPreviousLine);
					}
					else
					{
						if (sel.Selection.IsReversed)
						{
							sel.PerformAction(PredefinedSelectionTransformations.ClearSelection);
							sel.PerformAction(PredefinedSelectionTransformations.MoveToPreviousLine);
						}
						else
						{
							sel.PerformAction(PredefinedSelectionTransformations.ClearSelection);
							sel.PerformAction(PredefinedSelectionTransformations.MoveToPreviousCaretPosition);
							sel.PerformAction(PredefinedSelectionTransformations.MoveToPreviousLine);
						}
					}
				},


				// Word-wise movements (clear selection then extend)
				['w'] = sel =>
				{
					sel.PerformAction(PredefinedSelectionTransformations.ClearSelection);
					sel.PerformAction(PredefinedSelectionTransformations.SelectToNextSubWord);
				},  // Next sub-word
				['W'] = sel =>
				{
					sel.PerformAction(PredefinedSelectionTransformations.ClearSelection);
					sel.PerformAction(PredefinedSelectionTransformations.SelectToNextWord);
				},  // Next Word
				['b'] = sel =>
				{
					sel.PerformAction(PredefinedSelectionTransformations.ClearSelection);
					sel.PerformAction(PredefinedSelectionTransformations.SelectToPreviousSubWord);
				},  // Previous sub-word
				['B'] = sel =>
				{
					sel.PerformAction(PredefinedSelectionTransformations.ClearSelection);
					sel.PerformAction(PredefinedSelectionTransformations.SelectToPreviousWord);
				}   // Previous Word
			};

			// Register all movement commands to the command map.
			foreach (var kvp in movementCommands)
			{
				_commandMap[kvp.Key] = (args, view, broker, ops) =>
				{
					broker.PerformActionOnAllSelections(kvp.Value);
					return true;
				};
			}

			// ** Other normal-mode commands **
			_commandMap['i'] = (args, view, broker, ops) =>
			{
				// Enter Insert mode at the start of each selection.
				SelectionManager.Instance.SaveSelections(broker);
				broker.PerformActionOnAllSelections(sel => MoveCaretToSelectionStart(sel));
				ModeManager.Instance.EnterInsert(view, broker);
				return true;
			};
			_commandMap['a'] = (args, view, broker, ops) =>
			{
				// Enter Insert mode at the end of each selection (append).
				SelectionManager.Instance.SaveSelections(broker);
				broker.PerformActionOnAllSelections(sel => MoveCaretToSelectionEnd(sel));
				ModeManager.Instance.EnterInsert(view, broker);
				return true;
			};
			_commandMap['o'] = (args, view, broker, ops) =>
			{
				// Open a new line *below* each selection and enter Insert mode.
				AddLine(view, broker, ops, above: false);
				ModeManager.Instance.EnterInsert(view, broker);
				return true;
			};
			_commandMap['O'] = (args, view, broker, ops) =>
			{
				// Open a new line *above* each selection and enter Insert mode.
				AddLine(view, broker, ops, above: true);
				ModeManager.Instance.EnterInsert(view, broker);
				return true;
			};
			_commandMap['/'] = (args, view, broker, ops) =>
			{
				SelectionManager.Instance.SaveSelections(broker);
				var spans = GetSearchDomain(view, broker);
				ModeManager.Instance.EnterSearch(view, broker, false, spans);
				return true;
			};
			_commandMap['s'] = (args, view, broker, ops) =>
			{
				SelectionManager.Instance.SaveSelections(broker);
				var spans = GetSearchDomain(view, broker);
				ModeManager.Instance.EnterSearch(view, broker, true, spans);
				return true;
			};
			_commandMap['x'] = (args, view, broker, ops) =>
			{
				// Extend selection to full lines, or extend linewise selection to the next line.
				broker.PerformActionOnAllSelections(sel => ExtendSelectionLinewise(sel, view));
				return true;
			};
			_commandMap['y'] = (args, view, broker, ops) =>
			{
				// Yank (copy) current selections.
				YankSelections(view, broker);
				return true;
			};
			_commandMap['d'] = (args, view, broker, ops) =>
			{
				// Delete selection (and yank unless Alt is held).
				return ExecuteDeleteCommand(view, broker, switchToInsert: false);
			};
			_commandMap['c'] = (args, view, broker, ops) =>
			{
				// Change (delete then enter Insert mode, and yank unless Alt is held).
				return ExecuteDeleteCommand(view, broker, switchToInsert: true);
			};
			_commandMap['u'] = (args, view, broker, ops) =>
			{
				// Undo last edit.
				//ops.Undo();
				return true;
			};
			_commandMap['U'] = (args, view, broker, ops) =>
			{
				// Redo last undone edit.
				//ops.Redo();
				return true;
			};
			_commandMap['p'] = (args, view, broker, ops) =>
			{
				// Paste from yank register/clipboard.
				Paste(view, broker);
				return true;
			};
			_commandMap['C'] = (args, view, broker, ops) =>
			{
				// Add a new caret below the last selection (multi-cursor).
				AddCaretBelowLastSelection(view, broker);
				return true;
			};
			_commandMap[','] = (args, view, broker, ops) =>
			{
				// Clear all secondary selections, keeping only the primary.
				broker.ClearSecondarySelections();
				return true;
			};
		}

		/// <summary>
		/// Handles a typed character command in Normal mode by dispatching to the appropriate action.
		/// </summary>
		public bool Handle(TypeCharCommandArgs args, ITextView view, IMultiSelectionBroker broker, IEditorOperations operations)
		{
			if (_commandMap.TryGetValue(args.TypedChar, out var handler))
			{
				// Found a command for this key – execute it.
				return handler(args, view, broker, operations);
			}

			// Unrecognized key in Normal mode: do nothing (but consume the input to prevent insertion).
			return true;
		}

		/// <summary>
		/// Executes a delete/change command: yanks selections (unless Alt is pressed), deletes them, and optionally enters Insert mode.
		/// </summary>
		/// <param name="switchToInsert">If true, enters Insert mode after deletion (for 'c' command).</param>
		private bool ExecuteDeleteCommand(ITextView view, IMultiSelectionBroker broker, bool switchToInsert)
		{
			bool altDown = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
			if (!altDown)
			{
				// Yank (copy) selections to register, unless Alt is held.
				YankSelections(view, broker);
			}

			DeleteSelection(view, broker);

			if (switchToInsert)
			{
				ModeManager.Instance.EnterInsert(view, broker);
			}
			return true;
		}

		private void MoveCaretToSelectionStart(ISelectionTransformer selection)
		{
			var startPoint = selection.Selection.Start;
			// Collapse selection to its start.
			selection.MoveTo(startPoint, false, PositionAffinity.Successor);
		}

		/// <summary>
		/// Moves the caret to the end of the selection, collapsing the selection.
		/// </summary>
		private void MoveCaretToSelectionEnd(ISelectionTransformer selection)
		{
			var endPoint = selection.Selection.End;
			// Collapse selection to its end.
			selection.MoveTo(endPoint, false, PositionAffinity.Successor);
		}

		/// <summary>
		/// Extends a selection to whole lines. If the selection is already linewise, extend it to include the next line.
		/// </summary>
		private void ExtendSelectionLinewise(ISelectionTransformer transformer, ITextView view)
		{
			var snapshot = view.TextSnapshot;
			var currentSelection = transformer.Selection;
			var startLine = currentSelection.Start.Position.GetContainingLine();
			var endLine = currentSelection.End.Position.GetContainingLine();

			// A linewise selection is defined as covering the full line content (excluding the line break).
			bool isAlreadyLinewise = currentSelection.Start.Position == startLine.Start &&
									  currentSelection.End.Position == endLine.End;

			VirtualSnapshotPoint newStart, newEnd;
			if (!isAlreadyLinewise)
			{
				// Not currently linewise: expand selection to cover the entire line(s) content.
				newStart = new VirtualSnapshotPoint(startLine.Start);
				newEnd = new VirtualSnapshotPoint(endLine.End);
			}
			else
			{
				// Already a full-line selection: extend to include the next line (if available).
				if (endLine.LineNumber + 1 < snapshot.LineCount)
				{
					var nextLine = snapshot.GetLineFromLineNumber(endLine.LineNumber + 1);
					newStart = new VirtualSnapshotPoint(startLine.Start);
					newEnd = new VirtualSnapshotPoint(nextLine.End);
				}
				else
				{
					// At last line – cannot extend further, so remain on the same lines.
					newStart = new VirtualSnapshotPoint(startLine.Start);
					newEnd = new VirtualSnapshotPoint(endLine.End);
				}
			}

			// Apply the new span (preserve original selection direction).
			var newSpan = new VirtualSnapshotSpan(newStart, newEnd);
			transformer.MoveTo(newSpan.Start, false, PositionAffinity.Successor);
			transformer.MoveTo(newSpan.End, true, PositionAffinity.Successor);
		}

		/// <summary>
		/// Inserts a new blank line above or below each selection (preserving indentation) and positions the caret on that new line.
		/// </summary>
		/// <param name="above">True to insert above each selection; false to insert below.</param>
		private void AddLine(ITextView view, IMultiSelectionBroker broker, IEditorOperations operations, bool above)
		{
			var snapshot = view.TextBuffer.CurrentSnapshot;
			var insertionPoints = new List<ITrackingPoint>();
			var indents = new List<string>();

			// Determine insertion points and indent strings for each selection.
			broker.PerformActionOnAllSelections(selection =>
			{
				var line = selection.Selection.ActivePoint.Position.GetContainingLine();
				var lineText = line.GetText();
				// Capture the leading whitespace (indentation) of the current line.
				var indent = lineText.Substring(0, lineText.Length - lineText.TrimStart().Length);
				indents.Add(indent);

				// Calculate position: start of line (for above) or end of line (for below).
				int pos = above ? line.Start.Position : line.End.Position;
				insertionPoints.Add(snapshot.CreateTrackingPoint(pos, PointTrackingMode.Negative));
			});

			// Insert newline(s) with proper indent at each calculated insertion point.
			using (var edit = view.TextBuffer.CreateEdit())
			{
				for (int i = 0; i < insertionPoints.Count; i++)
				{
					var tp = insertionPoints[i];
					var indent = indents[i];
					string textToInsert = above
						? indent + Environment.NewLine       // insert a new line above, with same indent
						: Environment.NewLine + indent;      // insert a new line below, with same indent
					edit.Insert(tp.GetPosition(snapshot), textToInsert);
				}
				edit.Apply();
			}

			// After insertion, move each selection’s caret to the beginning of the inserted line.
			var newSnapshot = view.TextBuffer.CurrentSnapshot;
			int index = 0;
			broker.PerformActionOnAllSelections(selection =>
			{
				// Calculate the caret position on the new line (after indent).
				int pos = insertionPoints[index].GetPosition(newSnapshot);
				string indent = indents[index++];
				int offset = above ? indent.Length : Environment.NewLine.Length + indent.Length;
				var caretPosition = new SnapshotPoint(newSnapshot, pos + offset);
				selection.MoveTo(new VirtualSnapshotPoint(caretPosition), false, PositionAffinity.Successor);
			});
		}

		/// <summary>
		/// Adds a new caret below the last selection, aligning it vertically with the original selection’s start/end.
		/// </summary>
		private void AddCaretBelowLastSelection(ITextView view, IMultiSelectionBroker broker)
		{
			// Find the bottom-most selection (last in document order).
			var bottomSelection = broker.AllSelections
										.OrderByDescending(s => s.End.Position.GetContainingLine().LineNumber)
										.ThenByDescending(s => s.End.Position.Position)
										.FirstOrDefault();
			if (bottomSelection == null)
				return;

			ITextSnapshot snapshot = view.TextSnapshot;
			var startPoint = bottomSelection.Start;
			var endPoint = bottomSelection.End;
			var startLine = startPoint.Position.GetContainingLine();
			var endLine = endPoint.Position.GetContainingLine();

			if (startLine.LineNumber != endLine.LineNumber)
			{
				// For simplicity, only duplicate carets for single-line selections.
				return;
			}

			// Get original line text and compute expanded (tab-expanded) text length for alignment.
			string originalLineText = startLine.GetText();
			string expandedText = originalLineText.Replace("\t", new string(' ', view.Options.GetTabSize()));

			// Calculate visual offsets of selection start and end within the line (accounting for tabs and virtual spaces).
			int startOffset = CalculateExpandedOffset(originalLineText.Substring(0, startPoint.Position - startLine.Start), view.Options.GetTabSize());
			int endOffset = CalculateExpandedOffset(originalLineText.Substring(0, endPoint.Position - startLine.Start), view.Options.GetTabSize());
			// Include virtual space in the offset calculations.
			startOffset += startPoint.VirtualSpaces;
			endOffset += endPoint.VirtualSpaces;

			// Find a line below that has enough length to accommodate the selection at these offsets.
			int nextLineNumber = endLine.LineNumber + 1;
			ITextSnapshotLine targetLine = null;
			int requiredOffset = Math.Max(startOffset, endOffset);
			while (nextLineNumber < snapshot.LineCount)
			{
				var candidateLine = snapshot.GetLineFromLineNumber(nextLineNumber);
				int candidateLength = CalculateExpandedOffset(candidateLine.GetText(), view.Options.GetTabSize());
				if (candidateLength >= requiredOffset)
				{
					targetLine = candidateLine;
					break;
				}
				nextLineNumber++;
			}
			if (targetLine == null)
			{
				// No suitable line found below; cannot add a caret aligned to the selection.
				return;
			}

			// Create points on the target line corresponding to the original selection's start/end visual offsets.
			var newStartPoint = CreatePointAtVisualOffset(targetLine, startOffset, view.Options.GetTabSize());
			var newEndPoint = CreatePointAtVisualOffset(targetLine, endOffset, view.Options.GetTabSize());

			// Form a new selection on the target line with the same orientation (direction) as the original.
			var newSelection = bottomSelection.IsReversed
				? new Microsoft.VisualStudio.Text.Selection(newEndPoint, newStartPoint)   // reversed selection
				: new Microsoft.VisualStudio.Text.Selection(newStartPoint, newEndPoint);  // forward selection

			broker.AddSelection(newSelection);
		}

		private System.Collections.Generic.List<SnapshotSpan> GetSearchDomain(ITextView view, IMultiSelectionBroker broker)
		{
			var spans = new System.Collections.Generic.List<SnapshotSpan>();
			foreach (var sel in broker.AllSelections)
			{
				if (!sel.IsEmpty)
				{
					spans.Add(new SnapshotSpan(sel.Start.Position, sel.End.Position));
				}
			}
			if (spans.Count == 0)
			{
				spans.Add(new SnapshotSpan(view.TextSnapshot, 0, view.TextSnapshot.Length));
			}
			return spans;
		}

		/// <summary>
		/// Calculates the expanded length of a text segment after replacing tabs with spaces (for alignment calculations).
		/// </summary>
		private int CalculateExpandedOffset(string text, int tabSize)
		{
			int expandedLength = 0;
			foreach (char c in text)
			{
				if (c == '\t')
				{
					// Each tab advances to the next multiple of tabSize.
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
		/// Creates a virtual snapshot point at a given visual offset into a line (accounts for tabs and returns a point with virtual spaces if needed).
		/// </summary>
		private VirtualSnapshotPoint CreatePointAtVisualOffset(ITextSnapshotLine line, int visualOffset, int tabSize)
		{
			string lineText = line.GetText();
			int currentVisualOffset = 0;
			int charOffset = 0;

			// Iterate through characters until reaching the desired visual offset.
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
					// Only advance char index if we haven't yet surpassed the target visual offset.
					charOffset++;
				}
			}

			// Any remaining offset distance is virtual space beyond end of line.
			int virtualSpaces = visualOffset - currentVisualOffset;
			if (virtualSpaces < 0) virtualSpaces = 0;

			// Create a snapshot point at the computed character position and attach virtual spaces.
			var basePoint = new SnapshotPoint(line.Snapshot, line.Start.Position + charOffset);
			return new VirtualSnapshotPoint(basePoint, virtualSpaces);
		}

		/// <summary>
		/// Deletes the content of all non-empty selections from the buffer.
		/// </summary>
		private void DeleteSelection(ITextView view, IMultiSelectionBroker broker)
		{
			// Take a stable snapshot of current selections before modifying the buffer.
			var selections = broker.AllSelections.ToList();
			if (!selections.Any())
				return;

			using (var edit = view.TextBuffer.CreateEdit())
			{
				// Delete each selection (process in reverse order for safety in overlapping scenarios).
				foreach (var sel in selections.OrderByDescending(s => s.Start.Position))
				{
					if (sel.IsEmpty) continue;

					// Determine span to delete.
					var spanToDelete = new SnapshotSpan(sel.Start.Position, sel.End.Position);
					// If selection covers whole line content (linewise), include the line break in deletion.
					if (IsLinewiseSelection(sel, view.TextSnapshot))
					{
						var endLine = sel.End.Position.GetContainingLine();
						if (endLine.End.Position < endLine.EndIncludingLineBreak.Position)
						{
							spanToDelete = new SnapshotSpan(sel.Start.Position, endLine.EndIncludingLineBreak);
						}
					}
					edit.Delete(spanToDelete);
				}
				edit.Apply();
			}

			// After deletion, collapse any remaining selections to a single caret at the start of the deleted range.
			broker.PerformActionOnAllSelections(transformer =>
			{
				var start = transformer.Selection.Start;
				transformer.MoveTo(start, false, PositionAffinity.Successor);
			});
		}

		/// <summary>
		/// Checks if a selection spans whole lines (from the start of a line to the end of that line’s content).
		/// </summary>
		private bool IsLinewiseSelection(Microsoft.VisualStudio.Text.Selection sel, ITextSnapshot snapshot)
		{
			if (sel.IsEmpty || sel.Start.IsInVirtualSpace || sel.End.IsInVirtualSpace)
				return false;

			var startLine = sel.Start.Position.GetContainingLine();
			var endLine = sel.End.Position.GetContainingLine();
			// True if the selection starts at line beginning and ends exactly at line content end.
			return sel.Start.Position == startLine.Start && sel.End.Position == endLine.End;
		}

		/// <summary>
		/// Yanks (copies) the current selections to both an internal register and the system clipboard.
		/// </summary>
		private void YankSelections(ITextView view, IMultiSelectionBroker broker)
		{
			var snapshot = view.TextSnapshot;
			var selections = broker.AllSelections.ToList();
			if (selections.Count == 0)
				return;

			var yankRegister = new List<YankItem>();
			var concatenatedText = new StringBuilder();

			foreach (var sel in selections)
			{
				// Get the text covered by the selection.
				var span = new SnapshotSpan(sel.Start.Position, sel.End.Position);
				string text = span.GetText();
				bool isLinewise = IsLinewiseSelection(sel, snapshot);

				// If selection was linewise, append a newline (since selection excluded the line break).
				if (isLinewise)
				{
					text += Environment.NewLine;
				}

				yankRegister.Add(new YankItem(text, isLinewise));
				concatenatedText.Append(text);
			}

			// Prepare clipboard data (add a trailing newline if any yanked content was linewise and missing final newline).
			string clipboardText = concatenatedText.ToString();
			if (yankRegister.Any(item => item.IsLinewise) && !clipboardText.EndsWith(Environment.NewLine))
			{
				clipboardText += Environment.NewLine;
			}

			// Create a DataObject to store both plain text and a custom format for yank register.
			var dataObject = new DataObject();
			dataObject.SetText(clipboardText);  // plain text format
												// Custom format: store the yank register as a JSON string.
			string json = JsonSerializer.Serialize(yankRegister);
			dataObject.SetData("MyVsHelixYankFormat", json);

			try
			{
				Clipboard.SetDataObject(dataObject, true);
			}
			catch
			{
				// Clipboard operation might fail if the clipboard is locked by another process. Swallow exceptions.
			}
		}

		/// <summary>
		/// Pastes text from the internal yank register if available; otherwise falls back to system clipboard text.
		/// </summary>
		private void Paste(ITextView view, IMultiSelectionBroker broker)
		{
			var currentSelections = broker.AllSelections.ToList();
			if (currentSelections.Count == 0)
				return;

			List<YankItem> pasteItems = null;

			// Try to retrieve our custom yank format from the clipboard, else use plain text.
			IDataObject dataObject = Clipboard.GetDataObject();
			if (dataObject != null)
			{
				if (dataObject.GetDataPresent("MyVsHelixYankFormat"))
				{
					// Our yank register format is present – use it.
					string json = (string)dataObject.GetData("MyVsHelixYankFormat");
					pasteItems = JsonSerializer.Deserialize<List<YankItem>>(json);
				}
				else if (dataObject.GetDataPresent(DataFormats.Text))
				{
					// Fallback: treat entire clipboard text as one yank item.
					string text = (string)dataObject.GetData(DataFormats.Text);
					bool isLinewise = text.EndsWith(Environment.NewLine);
					pasteItems = new List<YankItem> { new YankItem(text, isLinewise) };
				}
			}

			if (pasteItems == null || pasteItems.Count == 0)
				return;

			// Insert the pasted content at each selection. If more selections than yank items, cycle through yank items.
			using (var edit = view.TextBuffer.CreateEdit())
			{
				for (int i = 0; i < currentSelections.Count; i++)
				{
					var sel = currentSelections[i];
					var item = pasteItems[i % pasteItems.Count];

					SnapshotPoint insertionPoint;
					if (item.IsLinewise)
					{
						// Linewise paste: insert at end-of-line (so content goes on a new line below).
						var line = sel.End.Position.GetContainingLine();
						insertionPoint = line.EndIncludingLineBreak;
					}
					else
					{
						// Characterwise paste: insert at the selection end.
						insertionPoint = sel.End.Position;
					}

					edit.Insert(insertionPoint.Position, item.Text);
				}
				edit.Apply();
			}

			// (Optional enhancement: move each caret to the end of its pasted text, if desired.)
		}
	}
}
