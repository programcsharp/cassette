﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Cassette.IO;
using Cassette.Utilities;

#if NET35
using Iesi.Collections.Generic;
#endif

namespace Cassette
{
    abstract class BundleFactoryBase<T> : IBundleFactory<T> 
        where T : Bundle
    {
        public virtual T CreateBundle(string path, IEnumerable<IFile> allFiles, BundleDescriptor bundleDescriptor)
        {
            var bundle = CreateBundleCore(path, bundleDescriptor);
            AddAssets(bundle, allFiles, bundleDescriptor.AssetFilenames);
            AddReferences(bundle, bundleDescriptor.References);
            SetIsSortedIfExplicitFilenames(bundle, bundleDescriptor.AssetFilenames);

            if (bundleDescriptor.IsFromFile)
            {
                bundle.DescriptorFilePath = bundleDescriptor.File.FullPath;
            }
            if (!string.IsNullOrWhiteSpace(bundleDescriptor.PageLocation))
            {
                bundle.PageLocation = bundleDescriptor.PageLocation;
            }

            return bundle;
        }

        protected abstract T CreateBundleCore(string path, BundleDescriptor bundleDescriptor);

        void AddAssets(Bundle bundle, IEnumerable<IFile> allFiles, IEnumerable<string> descriptorFilenames)
        {
            var remainingFiles = new HashedSet<IFile>(allFiles);
            var filesByPath = allFiles.ToDictionary(f => f.FullPath, StringComparer.OrdinalIgnoreCase);

            foreach (var filename in descriptorFilenames)
            {
                if (filename == "*")
                {
                    AddAllAssetsToBundle(bundle, remainingFiles);
                    break;
                }
                else if (filename.EndsWith("/*")) // Thanks to maniserowicz for this idea
                {
                    AddAllSubDirectoryAssetsToBundle(bundle, filename.TrimEnd('*'), remainingFiles);
                }
                else
                {
                    var file = FindFileOrThrow(bundle, filename, filesByPath);

                    bundle.Assets.Add(new FileAsset(file, bundle));
                    remainingFiles.Remove(file);
                }
            }
        }

        IFile FindFileOrThrow(Bundle bundle, string filename, Dictionary<string, IFile> filesByPath)
        {
            IFile file;
            if (filesByPath.TryGetValue(filename, out file))
            {
                return file;
            }

            ThrowIfShouldReferenceNonMinFile(bundle, filename, filesByPath);
            ThrowIfShouldReferenceDebugFile(bundle, filename, filesByPath);

            throw new FileNotFoundException(
                string.Format(
                    "The asset file \"{0}\" was not found for bundle \"{1}\".",
                    filename,
                    bundle.Path
                )
            );
        }

        static void ThrowIfShouldReferenceNonMinFile(Bundle bundle, string filename, Dictionary<string, IFile> filesByPath)
        {
            var minMatch = Regex.Match(filename, @"^(.*)[.-]min(\.js|\.css)$");
            if (minMatch.Success)
            {
                var nonMinFilename = minMatch.Groups[1].Value + minMatch.Groups[2].Value;
                if (filesByPath.ContainsKey(nonMinFilename))
                {
                    throw new ArgumentException(
                        string.Format(
                            "Bundle \"{0}\" references \"{1}\" when it should reference \"{2}\".",
                            bundle.Path,
                            filename,
                            nonMinFilename
                            )
                        );
                }
            }
        }

        static void ThrowIfShouldReferenceDebugFile(Bundle bundle, string filename, Dictionary<string, IFile> filesByPath)
        {
            var insertionIndex = filename.LastIndexOf('.');
            var debugOptions = new[] { "-debug", ".debug" };
            foreach (var debugOption in debugOptions)
            {
                var debugFilename = filename.Insert(insertionIndex, debugOption);
                if (filesByPath.ContainsKey(debugFilename))
                {
                    throw new ArgumentException(
                        string.Format(
                            "Bundle \"{0}\" references \"{1}\" when it should reference \"{2}\".",
                            bundle.Path,
                            filename,
                            debugFilename
                            )
                        );
                }
            }
        }

        void AddAllAssetsToBundle(Bundle bundle, IEnumerable<IFile> remainingFiles)
        {
            foreach (var file in remainingFiles)
            {
                bundle.Assets.Add(new FileAsset(file, bundle));
            }
        }

        void AddAllSubDirectoryAssetsToBundle(Bundle bundle, string path, HashedSet<IFile> remainingFiles)
        {
            path = PathUtilities.AppRelative(PathUtilities.NormalizePath(path));
            var filesInSubDirectory = remainingFiles
                .Where(file => file.FullPath.StartsWith(path, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (var file in filesInSubDirectory)
            {
                remainingFiles.Remove(file);
                bundle.Assets.Add(new FileAsset(file, bundle));
            }
        }

        void AddReferences(Bundle bundle, IEnumerable<string> references)
        {
            foreach (var reference in references)
            {
                bundle.AddReference(reference);
            }
        }

        void SetIsSortedIfExplicitFilenames(Bundle bundle, IList<string> filenames)
        {
            if (filenames.Count == 0 || filenames[0] != "*")
            {
                bundle.IsSorted = true;
            }
        }
    }
}
