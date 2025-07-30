using System;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;

namespace VsHelix
{
	public sealed class ModeManager
	{
		// 1. Private constructor to prevent instantiation from outside
		private ModeManager()
		{
			// Initialization code can go here
		}

		// 2. A private, static, and readonly field to hold the lazy-initialized instance.
		private static readonly Lazy<ModeManager> lazyInstance =
			new Lazy<ModeManager>(() => new ModeManager());

		// 3. A public static property to provide the single global access point.
		public static ModeManager Instance => lazyInstance.Value;

		// --- Your existing class members ---
		public enum EditorMode { Normal, Insert, Visual, Search, Goto }
		public EditorMode Current { get; private set; } = EditorMode.Normal;

		private SearchMode? _searchMode;
		public SearchMode? Search => _searchMode;


		private ITextSearchService2 GetSearchService()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var componentModel = (IComponentModel)ServiceProvider.GlobalProvider.GetService(typeof(SComponentModel));
			return componentModel.GetService<ITextSearchService2>();
		}

		public void EnterInsert(ITextView view, IMultiSelectionBroker broker)
		{
			Current = EditorMode.Insert;
			StatusBarHelper.ShowMode(Current);
			view.Options.SetOptionValue(DefaultTextViewOptions.OverwriteModeId, false);
			view.Options.SetOptionValue(DefaultTextViewOptions.ShowSelectionMatchesId, true);
		}

		public void EnterVisual(ITextView view, IMultiSelectionBroker broker)
		{
			Current = EditorMode.Visual;
			StatusBarHelper.ShowMode(Current);
			view.Options.SetOptionValue(DefaultTextViewOptions.OverwriteModeId, true);
			view.Options.SetOptionValue(DefaultTextViewOptions.ShowSelectionMatchesId, false);
		}

		public void EnterSearch(ITextView view, IMultiSelectionBroker broker, bool selectAll, System.Collections.Generic.List<SnapshotSpan> domain)
		{
			Current = EditorMode.Search;
			_searchMode = new SearchMode(selectAll, view, broker, domain, GetSearchService());
			StatusBarHelper.ShowMode(Current);
			view.Options.SetOptionValue(DefaultTextViewOptions.OverwriteModeId, true);
		}

		public void EnterGoto(ITextView view, IMultiSelectionBroker broker)
		{
			Current = EditorMode.Goto;
			StatusBarHelper.ShowMode(Current);
			view.Options.SetOptionValue(DefaultTextViewOptions.OverwriteModeId, true);
		}

		public void EnterNormal(ITextView view, IMultiSelectionBroker broker)
		{
			Current = EditorMode.Normal;
			_searchMode = null;
			StatusBarHelper.ShowMode(Current);
			view.Options.SetOptionValue(DefaultTextViewOptions.OverwriteModeId, true);  // block caret
			view.Options.SetOptionValue(DefaultTextViewOptions.ShowSelectionMatchesId, false);

		}
	}
}
