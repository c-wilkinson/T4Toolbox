// <copyright file="TemplatedFileGenerator.cs" company="Oleg Sych">
//  Copyright © Oleg Sych. All Rights Reserved.
// </copyright>

namespace T4Toolbox.VisualStudio
{
    using System;
    using System.CodeDom.Compiler;
    using System.Globalization;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using Microsoft.VisualStudio;
    using Microsoft.VisualStudio.Designer.Interfaces;
    using Microsoft.VisualStudio.Shell.Interop;
    using Microsoft.VisualStudio.TextTemplating;
    using Microsoft.VisualStudio.TextTemplating.VSHost;

    /// <summary>
    /// File generator that uses Text Templating engine to transform template specified in the 
    /// Template metadata of the input file.
    /// </summary>
    [Guid("AB67B8E2-1F42-4C27-A0DD-16E8D54EE487")]
    public sealed class TemplatedFileGenerator : BaseTemplatedCodeGenerator
    {
        internal const string Name = "T4Toolbox.TemplatedFileGenerator";
        internal const string Description = "Generator that uses Text Templating engine to transform template specified in the Template metadata of the input file.";

        /// <summary>
        /// Extracts template name from the input file metadata, resolves its full name using the built-in T4 logic
        /// for loading include files, loads the template content and passes it to the base class to transform.
        /// </summary>
        protected override byte[] GenerateCode(string inputFileName, string inputFileContent)
        {
            // Begin a new error session to make sure errors we log here are not removed when the base method is called.
            TextTemplating.BeginErrorSession();
            try
            {
                // Pass the name and content of the input file to the transformation
                ITextTemplatingSessionHost sessionHost = (ITextTemplatingSessionHost)TextTemplating;
                sessionHost.Session[TransformationContext.InputFileNameKey] = inputFileName;
                sessionHost.Session[TransformationContext.InputFileContentKey] = inputFileContent;

                // Resolve template file name based on input metadata
                if (ResolveTemplate(ref inputFileName, ref inputFileContent))
                {
                    // Perform default transformation of the template
                    return base.GenerateCode(inputFileName, inputFileContent);
                }

                return Encoding.Default.GetBytes("ErrorGeneratingOutput");
            }
            finally
            {
                TextTemplating.EndErrorSession();
            }
        }

        private static string ResolveFromTemplateMetadata(IVsHierarchy hierarchy, uint inputFileId)
        {
            IVsBuildPropertyStorage propertyStorage = (IVsBuildPropertyStorage)hierarchy;
            if (ErrorHandler.Succeeded(propertyStorage.GetItemAttribute(inputFileId, ItemMetadata.Template, out string templateFileName)))
            {
                return templateFileName;
            }

            return null;
        }

        private void LogError(string fileName, string format, params object[] args)
        {
            ITextTemplatingEngineHost engineHost = (ITextTemplatingEngineHost)TextTemplating;
            string errorText = string.Format(CultureInfo.CurrentCulture, format, args);
            CompilerError error = new CompilerError { FileName = fileName, ErrorText = errorText };
            engineHost.LogErrors(new CompilerErrorCollection(new[] { error }));
        }

        private bool ResolveTemplate(ref string inputFileName, ref string inputFileContent)
        {
            // Try getting template file name from the MSBuild metadata of the input file
            IVsHierarchy hierarchy = (IVsHierarchy)GetService(typeof(IVsHierarchy));
            ErrorHandler.ThrowOnFailure(hierarchy.ParseCanonicalName(inputFileName, out uint inputFileId));

            string templateFileName = ResolveFromTemplateMetadata(hierarchy, inputFileId) 
                ?? ResolveFromLastGenOutputMetadata(hierarchy, inputFileId, inputFileName);
            if (string.IsNullOrWhiteSpace(templateFileName))
            {
                LogError(inputFileName, "Input file does not specify Template metadata element required by {0}.", TemplatedFileGenerator.Name);
                return true;
            }

            TemplateLocator templateLocator = T4ToolboxPackage.Instance.GetServiceAsync(typeof(TemplateLocator)).Result as TemplateLocator;
            if (!templateLocator.LocateTemplate(inputFileName, ref templateFileName))
            {
                LogError(inputFileName, "Template '{0}' could not be found.", templateFileName);
                return false;
            }

            inputFileName = templateFileName;
            inputFileContent = File.ReadAllText(inputFileName, EncodingHelper.GetEncoding(inputFileName));
            return true;
        }

        private string ResolveFromLastGenOutputMetadata(IVsHierarchy hierarchy, uint inputFileId, string inputFileName)
        {
            IVsBuildPropertyStorage propertyStorage = (IVsBuildPropertyStorage)hierarchy;

            if (ErrorHandler.Succeeded(propertyStorage.GetItemAttribute(inputFileId, ItemMetadata.LastGenOutput, out string lastGenOutputFileName)) &&
                string.Equals(".tt", Path.GetExtension(lastGenOutputFileName), StringComparison.OrdinalIgnoreCase))
            {
                // Remove the script file from the project to prevent Visual Studio from deleting it
                string inputDirectory = Path.GetDirectoryName(inputFileName);
                string lastGenOutputFilePath = Path.Combine(inputDirectory, lastGenOutputFileName);
                string tempFilePath = Path.Combine(inputDirectory, Path.GetRandomFileName());
                File.Move(lastGenOutputFilePath, tempFilePath);
                ErrorHandler.ThrowOnFailure(hierarchy.ParseCanonicalName(lastGenOutputFilePath, out uint lastGenOutputFileId));
                IVsProject2 project = (IVsProject2)hierarchy;
#pragma warning disable IDE0059 // Unnecessary assignment of a value
                ErrorHandler.ThrowOnFailure(project.RemoveItem(default, lastGenOutputFileId, out int result));
#pragma warning restore IDE0059 // Unnecessary assignment of a value
                File.Move(tempFilePath, lastGenOutputFilePath);

                // Save name of the script file in the <Template> metadata item of the project item and refresh the Properties window
                ErrorHandler.ThrowOnFailure(propertyStorage.SetItemAttribute(inputFileId, ItemMetadata.Template, lastGenOutputFileName));
                IVSMDPropertyBrowser propertyBrowser = (IVSMDPropertyBrowser)GlobalServiceProvider.GetService(typeof(SVSMDPropertyBrowser));
                propertyBrowser.Refresh();

                return lastGenOutputFileName;
            }

            return null;
        }
    }
}
