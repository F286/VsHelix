using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace VsHelix
{
	[Export(typeof(IWpfTextViewCreationListener))]
	[ContentType("text")]
	[TextViewRole(PredefinedTextViewRoles.Editable)]
	[VisualStudioContribution]
	internal sealed class NormalModeSelectionBehavior : IWpfTextViewCreationListener
	{
	    public void TextViewCreated(IWpfTextView textView)
	    {
	        var broker = textView.GetMultiSelectionBroker();
	        textView.Caret.PositionChanged += (s, e) => EnsureSelection(textView, broker);
	        textView.Selection.SelectionChanged += (s, e) => EnsureSelection(textView, broker);
	    }

	    private void EnsureSelection(ITextView view, IMultiSelectionBroker broker)
	    {
	        if (ModeManager.Instance.Current != ModeManager.EditorMode.Normal)
	        {
	            return;
	        }

	        broker.PerformActionOnAllSelections(sel =>
	        {
	            if (sel.Selection.IsEmpty)
	            {
	                var point = sel.Selection.ActivePoint.Position;
	                var snapshot = point.Snapshot;
	                if (point.Position < snapshot.Length)
	                {
	                    var start = new SnapshotPoint(snapshot, point.Position);
	                    var end = new SnapshotPoint(snapshot, point.Position + 1);
	                    var span = new VirtualSnapshotSpan(new SnapshotSpan(start, end));
	                    sel.MoveTo(span.Start, false, PositionAffinity.Successor);
	                    sel.MoveTo(span.End, true, PositionAffinity.Successor);
	                }
	            }
	        });
	    }
	}
}
