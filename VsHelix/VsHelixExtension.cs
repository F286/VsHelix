using Microsoft.VisualStudio.Extensibility;
using System;

namespace VsHelix
{
    // Main entry point for Visual Studio Extensibility SDK
    public sealed class VsHelixExtension : Extension
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