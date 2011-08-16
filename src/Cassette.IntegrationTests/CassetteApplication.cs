﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using Cassette.HtmlTemplates;
using Cassette.Scripts;
using Cassette.Stylesheets;
using Cassette.Web;
using Moq;
using Should;
using Xunit;
using Cassette.UI;
using System.Web.Routing;

namespace Cassette.IntegrationTests
{
    public class CassetteApplication_FullTest
    {
        [Fact]
        public void CassetteApplicationCachesMinifiedModulesInIsolatedStorage()
        {
            // Ensure there is no existing cache.
            using (var storage = IsolatedStorageFile.GetMachineStoreForAssembly())
            {
                storage.Remove();
            }

            using (var storage = IsolatedStorageFile.GetMachineStoreForAssembly())
            {
                var sourceFileSystem = new FileSystem(Path.GetFullPath(@"..\..\assets"));
                var cacheFileSystem = new IsolatedStorageFileSystem(storage);
                var application = new Cassette.Web.CassetteApplication(sourceFileSystem, cacheFileSystem, new UrlGenerator("/", Enumerable.Empty<string>()), true, "1");
                var httpContext = new Mock<HttpContextBase>();
                var items = new Dictionary<string, object>();
                httpContext.SetupGet(c => c.Items).Returns(items);
                httpContext.SetupGet(c => c.Response).Returns(Mock.Of<HttpResponseBase>());
                var placeholderTracker = new Mock<IPlaceholderTracker>();
                placeholderTracker.Setup(t => t.InsertPlaceholder(It.IsAny<Func<IHtmlString>>()))
                    .Returns<Func<IHtmlString>>(f => f());
                items[typeof(IPlaceholderTracker).FullName] = placeholderTracker.Object;
                application.GetHttpContext = () => httpContext.Object;

                // Define the modules
                application.Add(new PerSubDirectorySource<ScriptModule>("scripts")
                {
                    Exclude = new Regex(@"\.vsdoc\.js$")
                });
                application.Add(new DirectorySource<StylesheetModule>("styles"));
                application.Add(new DirectorySource<HtmlTemplateModule>("templates"));

                application.InitializeModuleContainers();

                var scripts = application.GetPageAssetManager<ScriptModule>();
                scripts.Reference("scripts/module-a");
                scripts.Reference("scripts/module-b");
                //scripts.Render().ToHtmlString().ShouldEqual(
                //    "function asset2(){}function asset1(){}" + 
                //    Environment.NewLine + 
                //    "function asset3(){}"
                //);

                // Get the stylesheet container and check it has the correct content.
                var styles = application.GetPageAssetManager<StylesheetModule>();
                styles.Reference("styles");
                //styles.Render().ToHtmlString().ShouldEqual(
                //    "body{color:#abc}p{border:1px solid red}"
                //);

                // Get the html template container and check it has the correct content.
                var htmlTemplates = application.GetPageAssetManager<HtmlTemplateModule>();
                htmlTemplates.Reference("templates");
                //htmlTemplates.Render().ToHtmlString().ShouldEqual(
                //    "<script id=\"asset-1\" type=\"text/html\">" + Environment.NewLine +
                //    "<p>asset 1</p>" + Environment.NewLine +
                //    "</script>" + Environment.NewLine +
                //    "<script id=\"asset-2\" type=\"text/html\">" + Environment.NewLine +
                //    "<p>asset 2</p>" + Environment.NewLine +
                //    "</script>"
                //);

                var routes = new RouteCollection();
                application.InstallRoutes(routes);
                var routeData = new RouteData();
                routeData.Values.Add("path", "scripts/module-a");
                
                var context = MakeMockHttpContext("~/_assets/scripts/module-a");
                var requestContext = new RequestContext(context.Object, routeData);
                var routeHandler = routes.GetRouteData(context.Object).RouteHandler;
                var handler = routeHandler.GetHttpHandler(requestContext);
                handler.ProcessRequest(null);
                context.Object.Response.OutputStream.Position = 0;
                var scriptOutput = context.Object.Response.OutputStream.ReadToEnd();
                scriptOutput.ShouldEqual("function asset2(){}function asset1(){}");
            }

        }
        


        Mock<HttpContextBase> MakeMockHttpContext(string url, string httpMethod = "GET")
        {
            var mockHttpContext = new Mock<HttpContextBase>();
            MockRequest(url, mockHttpContext, httpMethod);
            MockResponse(mockHttpContext);
            return mockHttpContext;
        }

        void MockRequest(string url, Mock<HttpContextBase> mockHttpContext, string method)
        {
            var mockRequest = new Mock<HttpRequestBase>();
            mockHttpContext.SetupGet(c => c.Request).Returns(mockRequest.Object);
            mockRequest.SetupGet(r => r.AppRelativeCurrentExecutionFilePath).Returns(url);
            mockRequest.SetupGet(r => r.PathInfo).Returns("");
            mockRequest.SetupGet(r => r.RequestType).Returns(method);
            mockRequest.SetupGet(r => r.HttpMethod).Returns(method);
        }

        void MockResponse(Mock<HttpContextBase> mockHttpContext)
        {
            var mockResponse = new Mock<HttpResponseBase>();
            mockHttpContext.SetupGet(c => c.Response).Returns(mockResponse.Object);
            mockResponse.Setup(r => r.ApplyAppPathModifier(It.IsAny<string>())).Returns<string>(r => r);
            mockResponse.SetupGet(r => r.OutputStream).Returns(new MemoryStream());
            mockResponse.SetupGet(r => r.Cache).Returns(Mock.Of<HttpCachePolicyBase>());
        }
    }
}