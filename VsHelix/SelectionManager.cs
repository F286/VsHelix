using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.Collections.Generic;
using System.Linq;

namespace VsHelix
{
	/// <summary>
	/// A data structure to hold the tracking points and direction of a single selection.
	/// </summary>
	internal class TrackedSelection
	{
		public ITrackingPoint AnchorPoint { get; }
		public ITrackingPoint ActivePoint { get; }
		public bool IsReversed { get; }

		public TrackedSelection(Selection selection, ITextSnapshot snapshot)
		{
			AnchorPoint = snapshot.CreateTrackingPoint(selection.Start.Position, PointTrackingMode.Positive);
			ActivePoint = snapshot.CreateTrackingPoint(selection.End.Position, PointTrackingMode.Positive);
			//IsReversed = selection.IsReversed;
		}
	}

	/// <summary>
	/// Manages saving and restoring editor selections across text buffer changes.
	/// This allows modes to preserve selection state when temporarily switching to another mode (e.g., Insert).
	/// </summary>
	internal sealed class SelectionManager
	{
		// Singleton pattern for easy global access.
		private static readonly SelectionManager instance = new SelectionManager();
		public static SelectionManager Instance => instance;

		private List<TrackedSelection> selectionsToRestore;

		// Private constructor to enforce singleton pattern.
		private SelectionManager() { }

		/// <summary>
		/// Gets a value indicating whether there are selections saved and waiting to be restored.
		/// </summary>
		public bool HasSavedSelections => selectionsToRestore != null && selectionsToRestore.Any();

		/// <summary>
		/// Captures the current selections from the editor and stores them as tracking points.
		/// </summary>
		/// <param name="broker">The multi-selection broker for the active view.</param>
		public void SaveSelections(IMultiSelectionBroker broker)
		{
			var snapshot = broker.TextView.TextSnapshot;
			// Convert each selection into a TrackedSelection object and store it.
			selectionsToRestore = broker.AllSelections.Select(s => new TrackedSelection(s, snapshot)).ToList();
		}

		/// <summary>
		/// Clears any saved selections without restoring them.
		/// </summary>
		public void ClearSelections()
		{
			selectionsToRestore = null;
		}

		/// <summary>
		/// Restores the previously saved selections onto the editor's current text snapshot.
		/// </summary>
		/// <param name="broker">The multi-selection broker for the active view.</param>
		public void RestoreSelections(IMultiSelectionBroker broker)
		{
			if (!HasSavedSelections)
			{
				return;
			}

			var currentSnapshot = broker.TextView.TextSnapshot;

			// Convert the tracked points back into Selection objects on the current snapshot.
			var newSelections = selectionsToRestore.Select(ts =>
			{
				// Get the updated positions from the tracking points.
				var trackedAnchor = new VirtualSnapshotPoint(ts.AnchorPoint.GetPoint(currentSnapshot));
				var trackedActive = new VirtualSnapshotPoint(ts.ActivePoint.GetPoint(currentSnapshot));

				// The constructor that takes a span and a reversed flag is the most reliable way
				// to reconstruct the selection while preserving its original direction.
				VirtualSnapshotSpan span;
				if (ts.IsReversed)
				{
					// For a reversed selection, the active point is the start of the span.
					span = new VirtualSnapshotSpan(trackedActive, trackedAnchor);
				}
				else
				{
					// For a forward selection, the anchor point is the start of the span.
					span = new VirtualSnapshotSpan(trackedAnchor, trackedActive);
				}

				// Use the correct constructor overload.
				return new Selection(span, ts.IsReversed);
			});

			var selectionsArray = newSelections.ToArray();
			if (selectionsArray.Any())
			{
				// Clear existing secondary selections.
				broker.ClearSecondarySelections();

				// The first selection becomes the new primary selection.
				var primarySelection = selectionsArray.First();
				broker.TextView.Selection.Select(new SnapshotSpan(primarySelection.Start.Position, primarySelection.End.Position), primarySelection.IsReversed);

				// Add the rest as new secondary selections.
				foreach (var selection in selectionsArray.Skip(1))
				{
					broker.AddSelection(selection);
				}
			}


			// Clear the saved state to prevent accidental re-use.
			selectionsToRestore = null;
		}
	}
}
