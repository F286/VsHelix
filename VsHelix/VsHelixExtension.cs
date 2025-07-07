using Microsoft.VisualStudio.Extensibility;
using System;

namespace VsHelix
{
/*	[VisualStudioContribution]
	internal class MockEntrypoint : Extension
	{
		public override ExtensionConfiguration ExtensionConfiguration => null;
	}*/

	// Main entry point for Visual Studio Extensibility SDK
	[VisualStudioContribution]
	public sealed class VsHelixExtension : Microsoft.VisualStudio.Extensibility.Extension
    {
        public override ExtensionConfiguration ExtensionConfiguration => new()
        {
            Metadata = new ExtensionMetadata(
                Vsix.Id,
                new Version(Vsix.Version),
                Vsix.Name,
                Vsix.Description,
                Vsix.Author
            )
        };
    }
}