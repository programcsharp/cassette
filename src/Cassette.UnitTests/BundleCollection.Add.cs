using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cassette.IO;
using Cassette.Scripts;
using Moq;
using Should;
using Xunit;
using Cassette.Utilities;
using Cassette.BundleProcessing;

namespace Cassette
{
    public class BundleCollection_Add_Tests : BundleCollectionTestsBase
    {
        TestableBundle createdBundle;
        
        public BundleCollection_Add_Tests()
        {
            CreateDirectory("test");
        
            factory
                .Setup(f => f.CreateBundle(It.IsAny<string>(), It.IsAny<IEnumerable<IFile>>(), It.IsAny<BundleDescriptor>()))
                .Returns<string, IEnumerable<IFile>, BundleDescriptor>(
                    (path, files, d) => (createdBundle = new TestableBundle(path))
                );
        }

        [Fact]
        public void GivenDefaultFileSourceReturnsAFile_WhenAddDirectoryPath_ThenFactoryUsedToCreateBundle()
        {
            var file = StubFile();
            fileSearch
                .Setup(s => s.FindFiles(It.IsAny<IDirectory>()))
                .Returns(new[] { file });

            bundles.Add<TestableBundle>("~/test");

            factory.Verify(f => f.CreateBundle(
                "~/test",
                It.Is<IEnumerable<IFile>>(files => files.SequenceEqual(new[] { file })),
                It.Is<BundleDescriptor>(d => d.AssetFilenames.Single() == "*"))
                );

            bundles["~/test"].ShouldBeSameAs(createdBundle);
        }

        [Fact]
        public void WhenAddWithDirectoryPathAndFileSearch_ThenFileSearchIsUsedToGetAssets()
        {
            var fileSearch = new Mock<IFileSearch>();
            fileSearch.Setup(s => s.FindFiles(It.IsAny<IDirectory>()))
                .Returns(new[] { StubFile() })
                .Verifiable();

            bundles.Add<TestableBundle>("~/test", fileSearch.Object);

            fileSearch.Verify();
        }

        [Fact]
        public void WhenAddWithCustomizeAction_ThenCustomizeActionCalledWithTheBundle()
        {
            fileSearch
                .Setup(s => s.FindFiles(It.IsAny<IDirectory>()))
                .Returns(new[] { StubFile() });

            Bundle bundle = null;
            Action<TestableBundle> action = b => bundle = b;

            bundles.Add("~/test", action);

            bundle.ShouldBeSameAs(createdBundle);
        }

        [Fact]
        public void GivenFilePath_WhenAdd_ThenBundleAdded()
        {
            File.WriteAllText(Path.Combine(tempDirectory, "file.js"), "");
            bundles.Add<TestableBundle>("~/file.js");

            bundles["~/file.js"].ShouldBeType<TestableBundle>();
        }

        [Fact]
        public void GivenFilePath_WhenAddFileWithoutPathTildePrefix_ThenBundleFactoryIsCalledWithBundleDescriptorHavingFullFilePathForAsset()
        {
            File.WriteAllText(Path.Combine(tempDirectory, "file.js"), "");
            bundles.Add<TestableBundle>("file.js");

            factory.Verify(f => f.CreateBundle(
                "~/file.js",
                It.IsAny<IEnumerable<IFile>>(),
                It.Is<BundleDescriptor>(
                    descriptor => descriptor.AssetFilenames.Single().Equals("~/file.js")
                )
            ));
        }

        [Fact]
        public void GivenPathThatDoesNotExist_WhenAddWith_ThenThrowException()
        {
            Assert.Throws<DirectoryNotFoundException>(
                () => bundles.Add<TestableBundle>("~/does-not-exist")
                );
        }

        [Fact]
        public void GivenBundleDescriptorFile_WhenAdd_ThenDescriptorPassedToFactory()
        {
            File.WriteAllText(Path.Combine(tempDirectory, "bundle.txt"), "b.js\na.js");

            var fileA = StubFile("~/a.js");
            var fileB = StubFile("~/b.js");
            fileSearch
                .Setup(s => s.FindFiles(It.IsAny<IDirectory>()))
                .Returns(new[] { fileA, fileB });

            bundles.Add<TestableBundle>("~");

            factory.Verify(f => f.CreateBundle(
                "~",
                It.IsAny<IEnumerable<IFile>>(),
                It.Is<BundleDescriptor>(d => d.AssetFilenames.SequenceEqual(new[] { "~/b.js", "~/a.js" }))
            ));
        }

        [Fact]
        public void GivenBundleDescriptorFileWithLocation_WhenAdd_ThenDescriptorPassedToFactoryAndLocationSet()
        {
            bundleFactoryProvider
                 .Setup(f => f.GetBundleFactory<ScriptBundle>())
                 .Returns(new ScriptBundleFactory(() => Mock.Of<IBundlePipeline<ScriptBundle>>()));

            File.WriteAllText(Path.Combine(tempDirectory, "bundle.txt"), "b.js\na.js\n[bundle]\npageLocation = head");

            var fileA = StubFile("~/a.js");
            var fileB = StubFile("~/b.js");
            fileSearch
                .Setup(s => s.FindFiles(It.IsAny<IDirectory>()))
                .Returns(new[] { fileA, fileB });

            bundles.Add<ScriptBundle>("~");
            
            bundles.First().PageLocation.ShouldEqual("head");
        }
        
        [Fact]
        public void GivenScriptBundleDescriptor_WhenAdd_ThenScriptDescriptorPassedToFactory()
        {
            var scriptBundleFactory = new Mock<IBundleFactory<ScriptBundle>>();
            scriptBundleFactory
                .Setup(f => f.CreateBundle(It.IsAny<string>(), It.IsAny<IEnumerable<IFile>>(), It.IsAny<BundleDescriptor>()))
                .Returns<string, IEnumerable<IFile>, BundleDescriptor>(
                    (path, files, d) => new ScriptBundle(path)
                );
            bundleFactoryProvider
                .Setup(p => p.GetBundleFactory<ScriptBundle>())
                .Returns(scriptBundleFactory.Object);

            File.WriteAllText(Path.Combine(tempDirectory, "scriptbundle.txt"), "b.js\na.js");

            var fileA = StubFile("~/a.js");
            var fileB = StubFile("~/b.js");
            fileSearch
                .Setup(s => s.FindFiles(It.IsAny<IDirectory>()))
                .Returns(new[] { fileA, fileB });

            bundles.Add<ScriptBundle>("~");

            scriptBundleFactory.Verify(f => f.CreateBundle(
                "~",
                It.IsAny<IEnumerable<IFile>>(),
                It.Is<BundleDescriptor>(d => d.AssetFilenames.SequenceEqual(new[] { "~/b.js", "~/a.js" }))
            ));
        }

        [Fact]
        public void GivenScriptBundleDescriptorAndBundleDescriptor_WhenAdd_ThenScriptBundleDescriptorIsUsed()
        {
            var scriptBundleFactory = new Mock<IBundleFactory<ScriptBundle>>();
            scriptBundleFactory
                .Setup(f => f.CreateBundle(It.IsAny<string>(), It.IsAny<IEnumerable<IFile>>(), It.IsAny<BundleDescriptor>()))
                .Returns<string, IEnumerable<IFile>, BundleDescriptor>(
                    (path, files, d) => new ScriptBundle(path)
                );
            bundleFactoryProvider
                .Setup(p => p.GetBundleFactory<ScriptBundle>())
                .Returns(scriptBundleFactory.Object);

            File.WriteAllText(Path.Combine(tempDirectory, "scriptbundle.txt"), "b.js\na.js");
            File.WriteAllText(Path.Combine(tempDirectory, "bundle.txt"), "");

            var fileA = StubFile("~/a.js");
            var fileB = StubFile("~/b.js");
            fileSearch
                .Setup(s => s.FindFiles(It.IsAny<IDirectory>()))
                .Returns(new[] { fileA, fileB });

            bundles.Add<ScriptBundle>("~");

            scriptBundleFactory.Verify(f => f.CreateBundle(
                "~",
                It.IsAny<IEnumerable<IFile>>(),
                It.Is<BundleDescriptor>(d => d.AssetFilenames.SequenceEqual(new[] { "~/b.js", "~/a.js" }))
            ));
        }

        [Fact]
        public void GivenDirectoryWithExternalBundleDescriptorReferencingOutsideDirectory_WhenAdd_ThenCreateWorks()
        {
            bundleFactoryProvider
                .Setup(f => f.GetBundleFactory<ScriptBundle>())
                .Returns(new ScriptBundleFactory(() => Mock.Of<IBundlePipeline<ScriptBundle>>()));

            CreateDirectory("test");
            File.WriteAllText(
                PathUtilities.Combine(tempDirectory, "test", "bundle.txt"),
                "[external]" + Environment.NewLine + "url=http://example.org/test.js" + Environment.NewLine + "[assets]" + Environment.NewLine + "~/test.js"
                );
            File.WriteAllText(Path.Combine(tempDirectory, "test.js"), "");

            bundles.Add<ScriptBundle>("test");
            bundles.Count().ShouldEqual(1);
            ScriptBundle bundle = bundles.Get<ScriptBundle>("test");
            bundle.Assets.Count().ShouldEqual(1);
            bundle.Assets[0].Path.ShouldEqual("~/test.js");
        }
    }
}