﻿// Copyright (c) DNN Software. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq.Expressions;
using System.Web;
using System.Web.Mvc;
using System.Web.UI.WebControls;
using Dnn.DynamicContent;
using DotNetNuke.Common;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Web.Mvc.Common;
using DotNetNuke.Web.Mvc.Helpers;

namespace Dnn.Modules.DynamicContentViewer.Helpers
{
    internal class TemplateHelper
    {
        private static readonly Dictionary<string, Func<HtmlHelper, string>> DefaultDisplayActions =
            new Dictionary<string, Func<HtmlHelper, string>>()
            {
                {"Boolean", DefaultDisplayTemplates.BooleanTemplate},
                {"Bytes", DefaultDisplayTemplates.BytesTemplate},
                {"DateTime", DefaultDisplayTemplates.StringTemplate},
                {"Integer", DefaultDisplayTemplates.StringTemplate},
                {"Float", DefaultDisplayTemplates.StringTemplate},
                {"Guid", DefaultDisplayTemplates.StringTemplate},
                {"String", DefaultDisplayTemplates.StringTemplate},
                {"TimeSpan", DefaultDisplayTemplates.StringTemplate},
                {"Uri", DefaultDisplayTemplates.UrlTemplate}
            };

        private static readonly Dictionary<string, Func<HtmlHelper, string>> DefaultEditorActions =
            new Dictionary<string, Func<HtmlHelper, string>>()
            {
                {"String", DefaultEditorTemplates.StringInputTemplate},
                {"Uri", DefaultEditorTemplates.UrlInputTemplate}
            };

        private static readonly string CacheItemId = Guid.NewGuid().ToString();

        private static string ExecuteDefaultAction(DnnHelper dnnHelper, DataType dataType, ViewDataDictionary viewData, DataBoundControlMode mode)
        {
            Dictionary<string, Func<HtmlHelper, string>> defaultActions = GetDefaultActions(mode);
            var actionCache = GetActionCache(dnnHelper);

            Func<HtmlHelper, string> defaultAction;
            if (defaultActions.TryGetValue(dataType.UnderlyingDataType.ToString(), out defaultAction))
            {
                actionCache[dataType.Name] = new ActionCacheCodeItem { Action = defaultAction };
                return defaultAction(MakeHtmlHelper(dnnHelper, viewData));
            }

            return String.Empty;
        }

        private static string ExecuteTemplate(DnnHelper dnnHelper, string dataType, string template, ViewDataDictionary viewData)
        {
            var actionCache = GetActionCache(dnnHelper);
            var viewContext = dnnHelper.ViewContext;

            ViewEngineResult viewEngineResult = ViewEngines.Engines.FindPartialView(dnnHelper.ViewContext, template);
            if (viewEngineResult.View != null)
            {
                actionCache[dataType] = new ActionCacheViewItem { View = viewEngineResult.View };

                using (StringWriter writer = new StringWriter(CultureInfo.InvariantCulture))
                {
                    viewEngineResult.View.Render(new ViewContext(viewContext, viewEngineResult.View, viewData, viewContext.TempData, writer), writer);
                    return writer.ToString();
                }
            }

            return String.Empty;
        }

        internal static Dictionary<string, ActionCacheItem> GetActionCache(DnnHelper dnnHelper)
        {
            HttpContextBase context = dnnHelper.ViewContext.HttpContext;
            Dictionary<string, ActionCacheItem> result;

            if (!context.Items.Contains(CacheItemId))
            {
                result = new Dictionary<string, ActionCacheItem>();
                context.Items[CacheItemId] = result;
            }
            else
            {
                result = (Dictionary<string, ActionCacheItem>)context.Items[CacheItemId];
            }

            return result;
        }

        internal static Dictionary<string, Func<HtmlHelper, string>> GetDefaultActions(DataBoundControlMode mode)
        {
            return mode == DataBoundControlMode.ReadOnly ? DefaultDisplayActions : DefaultEditorActions;
        }

        private static string GetTemplate(string templateName, string rootPath, string rootMapPath, DataBoundControlMode mode)
        {
            var displayPath = "{0}Content Templates\\DisplayTemplates\\{1}.cshtml";
            var editorPath = "{0}Content Templates\\EditorTemplates\\{1}.cshtml";
            var templatePath = (mode == DataBoundControlMode.ReadOnly) ? displayPath : editorPath;

            var path = String.Format(templatePath.Replace("\\", "/"), rootPath, templateName);
            var mapPath = String.Format(templatePath, rootMapPath, templateName);
            if (File.Exists(mapPath))
            {
                return path;
            }

            return String.Empty;
        }

        private static string GetTemplate(string templateName, DataType dataType, DataBoundControlMode mode, PortalSettings settings)
        {
            var portalRoot = settings.HomeDirectory;
            var hostRoot = Globals.HostPath;
            var portalRootMap = settings.HomeDirectoryMapPath;
            var hostRootMap = Globals.HostMapPath;

            //Check Portal for Template Name
            var path = GetTemplate(templateName, portalRoot, portalRootMap, mode);

            //Check Host for Template Name
            if (String.IsNullOrEmpty(path))
            {
                path = GetTemplate(templateName, hostRoot, hostRootMap, mode);
            }

            //Check Portal for DataType
            if (String.IsNullOrEmpty(path))
            {
                path = GetTemplate(dataType.Name, portalRoot, portalRootMap, mode);
            }

            //Check Host for DataType
            if (String.IsNullOrEmpty(path))
            {
                path = GetTemplate(dataType.Name, hostRoot, hostRootMap, mode);
            }

            //Check Portal for Underlying DataType
            if (String.IsNullOrEmpty(path))
            {
                path = GetTemplate(dataType.UnderlyingDataType.ToString(), portalRoot, portalRootMap, mode);
            }

            //Check Host for Underlying DataType
            if (String.IsNullOrEmpty(path))
            {
                path = GetTemplate(dataType.UnderlyingDataType.ToString(), hostRoot, hostRootMap, mode);
            }

            return path;
        }

        private static HtmlHelper MakeHtmlHelper(DnnHelper dnnHelper, ViewDataDictionary viewData)
        {
            var viewContext = dnnHelper.ViewContext;
            var newHelper = new HtmlHelper(
                new ViewContext(viewContext, viewContext.View, viewData, viewContext.TempData, viewContext.Writer),
                new ViewDataContainer(viewData));
            return newHelper;
        }

        internal static MvcHtmlString TemplateFor<TModel>(DnnHelper<TModel> dnnHelper, string fieldName, string templateName, string htmlFieldName, DataBoundControlMode mode, object additionalViewData)
        {
            var contentItem = dnnHelper.ViewData.Model as DynamicContentItem;

            if (contentItem == null)
            {
                throw new InvalidOperationException("This helper is only supported for models of type DynamicContentItem");
            }

            var contentField = contentItem.Fields[fieldName];

            if (contentField == null)
            {
                throw new InvalidOperationException("The fieldName does not represent a valid DynamicContentField");
            }

            var dataType = contentField.Definition.DataType;

            var viewData = new ViewDataDictionary(dnnHelper.ViewData)
                                        {
                                            Model = contentField.Value
                                        };

            if (additionalViewData != null)
            {
                foreach (KeyValuePair<string, object> kvp in TypeHelper.ObjectToDictionary(additionalViewData))
                {
                    viewData[kvp.Key] = kvp.Value;
                }
            }

            Dictionary<string, ActionCacheItem> actionCache = GetActionCache(dnnHelper);

            ActionCacheItem cacheItem;

            string htmlString = String.Empty;
            if (actionCache.TryGetValue(dataType.Name, out cacheItem))
            {
                if (cacheItem != null)
                {
                    htmlString = cacheItem.Execute(dnnHelper, viewData);
                }
            }
            else
            {
                var template = GetTemplate(templateName, dataType, mode, dnnHelper.PortalSettings);

                if (String.IsNullOrEmpty(template))
                {
                    htmlString = ExecuteDefaultAction(dnnHelper, dataType, viewData, mode);
                }
                else
                {
                    htmlString = contentField.Value == null 
                                    ? String.Empty 
                                    : ExecuteTemplate(dnnHelper, dataType.Name, template, viewData);
                }
            }

            return MvcHtmlString.Create(htmlString);
        }

        internal abstract class ActionCacheItem
        {
            public abstract string Execute(DnnHelper dnnHelper, ViewDataDictionary viewData);
        }

        internal class ActionCacheCodeItem : ActionCacheItem
        {
            public Func<HtmlHelper, string> Action { get; set; }

            public override string Execute(DnnHelper dnnHelper, ViewDataDictionary viewData)
            {
                return Action(MakeHtmlHelper(dnnHelper, viewData));
            }
        }

        internal class ActionCacheViewItem : ActionCacheItem
        {
            public IView View { get; set; }

            public override string Execute(DnnHelper dnnHelper, ViewDataDictionary viewData)
            {
                var viewContext = dnnHelper.ViewContext;

                using (StringWriter writer = new StringWriter(CultureInfo.InvariantCulture))
                {
                    View.Render(new ViewContext(viewContext, View, viewData, viewContext.TempData, writer), writer);
                    return writer.ToString();
                }
            }
        }

        private class ViewDataContainer : IViewDataContainer
        {
            public ViewDataContainer(ViewDataDictionary viewData)
            {
                ViewData = viewData;
            }

            public ViewDataDictionary ViewData { get; set; }
        }
    }
}
