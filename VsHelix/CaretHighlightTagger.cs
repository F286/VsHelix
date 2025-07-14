using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace VsHelix
{
	internal static class CaretHighlightFormat
	{
		public const string Name = "VsHelixCaretHighlight";
	}

	[Export(typeof(EditorFormatDefinition))]
	[Name(CaretHighlightFormat.Name)]
	[UserVisible(true)]
	internal sealed class CaretHighlightDefinition : MarkerFormatDefinition
	{
		public CaretHighlightDefinition()
		{
			BackgroundColor = System.Windows.Media.Colors.LightGray;
			DisplayName = "VsHelix caret highlight";
			ZOrder = 5;
		}
	}

	[Export(typeof(IViewTaggerProvider))]
	[ContentType("text")]
	[TagType(typeof(TextMarkerTag))]
	internal sealed class CaretHighlightProvider : IViewTaggerProvider
	{
		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
		{
			if (textView.TextBuffer != buffer)
			return null;
			return textView.Properties.GetOrCreateSingletonProperty(() => new CaretHighlightTagger(textView)) as ITagger<T>;
		}
	}

	internal sealed class CaretHighlightTagger : ITagger<TextMarkerTag>
	{
		private readonly ITextView _view;
		private readonly IMultiSelectionBroker _broker;

		public CaretHighlightTagger(ITextView view)
		{
			view ??= throw new ArgumentNullException(nameof(view));
			 _view = view;
			 _broker = view.GetMultiSelectionBroker();
			 _view.Caret.PositionChanged += OnChanged;
			 _view.Selection.SelectionChanged += OnChanged;
			 _view.LayoutChanged += (s, e) => RaiseTagsChanged();
		}

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		private void OnChanged(object sender, EventArgs e) => RaiseTagsChanged();

		private void RaiseTagsChanged()
		{
			var snapshot = _view.TextSnapshot;
			TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
		}

		public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans)
		{
			if (spans.Count == 0 || ModeManager.Instance.Current != ModeManager.EditorMode.Normal)
			yield break;

			var snapshot = _view.TextSnapshot;
			foreach (var sel in _broker.AllSelections)
			{
				var span = SelectionUtilities.GetEffectiveSpan(sel, snapshot);
				if (span.Length == 0)
				continue;

				foreach (var visible in spans)
				{
			var inter = visible.Intersection(span);
			if (inter != null)
			{
					yield return new TagSpan<TextMarkerTag>(inter.Value, new TextMarkerTag(CaretHighlightFormat.Name));
					break;
			}
				}
			}
		}
	}
}
