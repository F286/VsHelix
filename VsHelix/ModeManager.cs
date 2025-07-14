using System;
using Microsoft.VisualStudio.Shell;

namespace VsHelix
{
	public sealed class ModeManager
	{
		// 1. Private constructor to prevent instantiation from outside
		internal ModeManager()
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

		internal Action<EditorMode> ShowModeAction { get; set; } = StatusBarHelper.ShowMode;

		public void EnterInsert()
		{
			Current = EditorMode.Insert;
			ShowModeAction(Current);
		}

		public void EnterNormal()
		{
			Current = EditorMode.Normal;
			ShowModeAction(Current);
		}
	}
}
