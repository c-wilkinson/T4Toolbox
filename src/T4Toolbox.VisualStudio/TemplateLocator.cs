// <copyright file="TemplateLocator.cs" company="Oleg Sych">
//  Copyright © Oleg Sych. All Rights Reserved.
// </copyright>

namespace T4Toolbox.VisualStudio
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.TextTemplating;
    using Microsoft.VisualStudio.TextTemplating.VSHost;

    /// <summary>
    /// Internal service for locating a template based on its file name.
    /// </summary>
    internal class TemplateLocator
    {
        protected TemplateLocator(IAsyncServiceProvider2 serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        protected IAsyncServiceProvider2 ServiceProvider { get; private set; }

        /// <summary>
        /// Returns full path to the template file resolved using T4 include rules.
        /// </summary>
        public virtual bool LocateTemplate(string fullInputPath, ref string templatePath)
        {
            ITextTemplating textTemplating = (ITextTemplating)ServiceProvider.GetServiceAsync(typeof(STextTemplating)).Result;

            // Use the built-in "include" resolution logic to find the template.
#pragma warning disable IDE0059 // Unnecessary assignment of a value
            textTemplating.PreprocessTemplate(Path.ChangeExtension(fullInputPath, ".tt"), string.Empty, null, "DummyClass", string.Empty, out string[] references);
#pragma warning restore IDE0059 // Unnecessary assignment of a value

            ITextTemplatingEngineHost engineHost = (ITextTemplatingEngineHost)textTemplating;

#pragma warning disable IDE0059 // Unnecessary assignment of a value
            if (engineHost.LoadIncludeText(templatePath, out string templateFileContent, out string templateFullPath))
            {
#pragma warning restore IDE0059 // Unnecessary assignment of a value
                templatePath = Path.GetFullPath(templateFullPath);
                return true;
            }

            return false;
        }

        internal static void Register(IAsyncServiceContainer serviceContainer)
        {
            serviceContainer.AddService(typeof(TemplateLocator), CreateService, true);
        }

        private static Task<object> CreateService(IAsyncServiceContainer container, CancellationToken cancellationToken, Type serviceType)
        {
            if (serviceType == typeof(TemplateLocator))
            {
                return System.Threading.Tasks.Task.FromResult<object>(new TemplateLocator(container as IAsyncServiceProvider2));
            }

            return System.Threading.Tasks.Task.FromResult<object>(null);
        }
    }
}