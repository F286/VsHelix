using System;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

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
        public enum EditorMode { Normal, Insert }
        public EditorMode Current { get; private set; } = EditorMode.Normal;
		
		public void EnterInsert(ITextView view, IMultiSelectionBroker broker)
		{
		Current = EditorMode.Insert;
		StatusBarHelper.ShowMode(Current);
		CaretHelper.DisplayInsertCarets(view, broker);
		}
		
		public void EnterNormal(ITextView view, IMultiSelectionBroker broker)
		{
		Current = EditorMode.Normal;
		StatusBarHelper.ShowMode(Current);
		CaretHelper.DisplayInsertCarets(view, broker);
		}
    }
}
