// <copyright file="CustomToolTemplateEditor.cs" company="Oleg Sych">
//  Copyright © Oleg Sych. All Rights Reserved.
// </copyright>

namespace T4Toolbox.VisualStudio
{
    using System;
    using System.ComponentModel;
    using System.Drawing.Design;
    using System.IO;
    using Microsoft.VisualStudio;
    using Microsoft.VisualStudio.Shell.Interop;
    using Microsoft.Win32;

    /// <summary>
    /// A specialized file name editor for the template property.
    /// </summary>
    public class CustomToolTemplateEditor : UITypeEditor
    {
        /// <summary>
        /// Defines the editor as a modal dialog.
        /// </summary>
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.Modal;
        }

        /// <summary>
        /// Uses the Windows Open File dialog to allow user to choose the template file.
        /// </summary>
        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            string templateFullPath = GetFullTemplatePath(context, (string)value);

            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Select Custom Tool Template",
                FileName = Path.GetFileName(templateFullPath),
                InitialDirectory = Path.GetDirectoryName(templateFullPath),
                Filter = "Text Templates (*.tt)|*.tt|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                return GetRelativeTemplatePath(context, dialog.FileName);
            }

            return value;
        }

        private static string GetFullTemplatePath(ITypeDescriptorContext context, string fileName)
        {
            string inputFullPath = GetFullInputPath(context);

            if (string.IsNullOrEmpty(fileName))
            {
                return Path.GetDirectoryName(inputFullPath) + Path.DirectorySeparatorChar;
            }

            string templateFullPath = fileName;
            TemplateLocator templateLocator = (TemplateLocator)context.GetService(typeof(TemplateLocator));
            if (!templateLocator.LocateTemplate(inputFullPath, ref templateFullPath))
            {
                return Path.Combine(Path.GetDirectoryName(inputFullPath), fileName);
            }

            return templateFullPath;
        }

        private static string GetRelativeTemplatePath(ITypeDescriptorContext context, string fullPath)
        {
            string inputPath = GetFullInputPath(context);
            string relativePath = FileMethods.GetRelativePath(inputPath, fullPath);
            if (relativePath.StartsWith("." + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                // Remove leading .\ from the path
                relativePath = relativePath.Substring(relativePath.IndexOf(Path.DirectorySeparatorChar) + 1);
            }

            return relativePath;
        }

        private static string GetFullInputPath(ITypeDescriptorContext context)
        {
            IVsBrowseObject browseObject = (IVsBrowseObject)context.Instance;
            ErrorHandler.ThrowOnFailure(browseObject.GetProjectItem(out IVsHierarchy hierarchy, out uint itemId));
            ErrorHandler.ThrowOnFailure(hierarchy.GetCanonicalName(itemId, out string inputFileName));
            return inputFileName;
        }
    }
}