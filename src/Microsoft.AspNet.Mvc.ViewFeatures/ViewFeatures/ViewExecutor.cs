// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc.Infrastructure;
using Microsoft.AspNet.Mvc.Internal;
using Microsoft.AspNet.Mvc.Rendering;
using Microsoft.AspNet.Mvc.ViewEngines;
using Microsoft.Extensions.OptionsModel;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNet.Mvc.ViewFeatures
{
    /// <summary>
    /// Executes an <see cref="IView"/>.
    /// </summary>
    public class ViewExecutor
    {
        /// <summary>
        /// The default content-type header value for views, <c>text/html; charset=utf8</c>.
        /// </summary>
        public static readonly MediaTypeHeaderValue DefaultContentType = new MediaTypeHeaderValue("text/html")
        {
            Encoding = Encoding.UTF8
        }.CopyAsReadOnly();

        /// <summary>
        /// Creates a new <see cref="ViewExecutor"/>.
        /// </summary>
        /// <param name="viewOptions">The <see cref="IOptions{MvcViewOptions}"/>.</param>
        /// <param name="writerFactory">The <see cref="IHttpResponseStreamWriterFactory"/>.</param>
        /// <param name="viewEngine">The <see cref="ICompositeViewEngine"/>.</param>
        /// <param name="diagnosticSource">The <see cref="DiagnosticSource"/>.</param>
        public ViewExecutor(
            IOptions<MvcViewOptions> viewOptions,
            IHttpResponseStreamWriterFactory writerFactory,
            ICompositeViewEngine viewEngine,
            DiagnosticSource diagnosticSource)
        {
            if (viewOptions == null)
            {
                throw new ArgumentNullException(nameof(viewOptions));
            }

            if (writerFactory == null)
            {
                throw new ArgumentNullException(nameof(writerFactory));
            }

            if (viewEngine == null)
            {
                throw new ArgumentNullException(nameof(viewEngine));
            }

            if (diagnosticSource == null)
            {
                throw new ArgumentNullException(nameof(diagnosticSource));
            }

            ViewOptions = viewOptions.Value;
            WriterFactory = writerFactory;
            ViewEngine = viewEngine;
            DiagnosticSource = diagnosticSource;
        }

        /// <summary>
        /// Gets the <see cref="DiagnosticSource"/>.
        /// </summary>
        protected DiagnosticSource DiagnosticSource { get; }

        /// <summary>
        /// Gets the default <see cref="IViewEngine"/>.
        /// </summary>
        protected IViewEngine ViewEngine { get; }

        /// <summary>
        /// Gets the <see cref="MvcViewOptions"/>.
        /// </summary>
        protected MvcViewOptions ViewOptions { get; }

        /// <summary>
        /// Gets the <see cref="IHttpResponseStreamWriterFactory"/>.
        /// </summary>
        protected IHttpResponseStreamWriterFactory WriterFactory { get; }

        /// <summary>
        /// Executes a view asynchronously.
        /// </summary>
        /// <param name="actionContext">The <see cref="ActionContext"/> associated with the current request.</param>
        /// <param name="view">The <see cref="IView"/>.</param>
        /// <param name="viewData">The <see cref="ViewDataDictionary"/>.</param>
        /// <param name="tempData">The <see cref="ITempDataDictionary"/>.</param>
        /// <param name="actionResultContentType">
        /// The content-type header value to set in the response. If <c>null</c>,
        /// <see cref="DefaultContentType"/> will be used.
        /// </param>
        /// <param name="statusCode">
        /// The HTTP status code to set in the response. May be <c>null</c>.
        /// </param>
        /// <returns>A <see cref="Task"/> which will complete when view execution is completed.</returns>
        public virtual async Task ExecuteAsync(
            ActionContext actionContext,
            IView view,
            ViewDataDictionary viewData,
            ITempDataDictionary tempData,
            MediaTypeHeaderValue actionResultContentType,
            int? statusCode)
        {
            if (actionContext == null)
            {
                throw new ArgumentNullException(nameof(actionContext));
            }

            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (viewData == null)
            {
                throw new ArgumentNullException(nameof(viewData));
            }

            if (tempData == null)
            {
                throw new ArgumentNullException(nameof(tempData));
            }

            var response = actionContext.HttpContext.Response;

            var resolvedContentType = ResponseContentTypeHelper.GetContentType(
                actionResultContentType,
                response.ContentType,
                DefaultContentType);

            response.ContentType = resolvedContentType.ToString();

            if (statusCode != null)
            {
                response.StatusCode = statusCode.Value;
            }

            using (var writer = WriterFactory.CreateWriter(
                response.Body,
                resolvedContentType.Encoding ?? DefaultContentType.Encoding))
            {
                var viewContext = new ViewContext(
                    actionContext,
                    view,
                    viewData,
                    tempData,
                    writer,
                    ViewOptions.HtmlHelperOptions);

                if (DiagnosticSource.IsEnabled("Microsoft.AspNet.Mvc.BeforeView"))
                {
                    DiagnosticSource.Write(
                        "Microsoft.AspNet.Mvc.BeforeView",
                        new { view = view, viewContext = viewContext, });
                }

                await view.RenderAsync(viewContext);

                if (DiagnosticSource.IsEnabled("Microsoft.AspNet.Mvc.AfterView"))
                {
                    DiagnosticSource.Write(
                        "Microsoft.AspNet.Mvc.AfterView",
                        new { view = view, viewContext = viewContext, });
                }

                // Perf: Invoke FlushAsync to ensure any buffered content is asynchronously written to the underlying
                // response asynchronously. In the absence of this line, the buffer gets synchronously written to the
                // response as part of the Dispose which has a perf impact.
                await writer.FlushAsync();
            }
        }
    }
}