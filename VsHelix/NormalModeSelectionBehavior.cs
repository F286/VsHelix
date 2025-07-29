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
			textView.Options.SetOptionValue(DefaultTextViewOptions.OverwriteModeId, true);  // block caret
			textView.Options.SetOptionValue(DefaultTextViewOptions.ShowSelectionMatchesId, false);	// disable similar word highlighting
		}
	}
}
