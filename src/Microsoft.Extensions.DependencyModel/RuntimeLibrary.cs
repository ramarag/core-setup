// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Extensions.DependencyModel
{
    public class RuntimeLibrary : Library
    {
        public RuntimeLibrary(string type,
            string name,
            string version,
            string hash,
            IReadOnlyList<RuntimeAssetGroup> runtimeAssemblyGroups,
            IReadOnlyList<RuntimeAssetGroup> nativeLibraryGroups,
            IEnumerable<ResourceAssembly> resourceAssemblies,
            IEnumerable<Dependency> dependencies,
            bool serviceable)
            : this(type,
                  name,
                  version,
                  hash,
                  runtimeAssemblyGroups,
                  nativeLibraryGroups,
                  resourceAssemblies,
                  dependencies,
                  serviceable,
                  path: null,
                  hashPath: null)
        {
        }

        public RuntimeLibrary(string type,
            string name,
            string version,
            string hash,
            IReadOnlyList<RuntimeAssetGroup> runtimeAssemblyGroups,
            IReadOnlyList<RuntimeAssetGroup> nativeLibraryGroups,
            IEnumerable<ResourceAssembly> resourceAssemblies,
            IEnumerable<Dependency> dependencies,
            bool serviceable,
            string path,
            string hashPath)
            : base(type,
                  name,
                  version,
                  hash,
                  dependencies,
                  serviceable,
                  path,
                  hashPath)
        {
            if (runtimeAssemblyGroups == null)
            {
                throw new ArgumentNullException(nameof(runtimeAssemblyGroups));
            }
            if (nativeLibraryGroups == null)
            {
                throw new ArgumentNullException(nameof(nativeLibraryGroups));
            }
            if (resourceAssemblies == null)
            {
                throw new ArgumentNullException(nameof(resourceAssemblies));
            }
            RuntimeAssemblyGroups = runtimeAssemblyGroups;
            ResourceAssemblies = resourceAssemblies.ToArray();
            NativeLibraryGroups = nativeLibraryGroups;

            Assemblies = new RuntimeAssembly[0];
            NativeLibraries = new string[0];
        }

        public static IList<RuntimeLibrary> RemoveReferences(IReadOnlyList<RuntimeLibrary> runtimeLibraries, IList<string> refname)
        {
            List<RuntimeLibrary> result = new List<RuntimeLibrary>();

            foreach (var runtimeLib in runtimeLibraries)
            {
                
                if (!string.IsNullOrEmpty(refname.First(elem => runtimeLib.Name.Contains(elem))))
                {
                    continue;
                }
                List<Dependency> toRemoveDependecy = new List<Dependency>();
                foreach (var dependency in runtimeLib.Dependencies)
                {
                    if (!string.IsNullOrEmpty(refname.First(elem => dependency.Name.Contains(elem))))
                    {
                        toRemoveDependecy.Add(dependency);
                    }
                }

                if (toRemoveDependecy.Count() > 0)
                {
                    List<Dependency> modifiedDependencies = new List<Dependency>();
                    foreach (var dependency in runtimeLib.Dependencies)
                    {
                        if (!toRemoveDependecy.Contains(dependency))
                        {
                            modifiedDependencies.Add(dependency);
                        }
                    }

                    result.Add(new RuntimeLibrary(runtimeLib.Type, 
                                                  runtimeLib.Name, 
                                                  runtimeLib.Version,
                                                  runtimeLib.Hash,
                                                  runtimeLib.RuntimeAssemblyGroups,
                                                  runtimeLib.NativeLibraryGroups,
                                                  runtimeLib.ResourceAssemblies,
                                                  modifiedDependencies,
                                                  runtimeLib.Serviceable));
                }
                else
                {
                    result.Add(runtimeLib);
                }
            }
                return result;
        }

        // Temporary (legacy) properties: https://github.com/dotnet/cli/issues/1998
        public IReadOnlyList<RuntimeAssembly> Assemblies { get; }
        public IReadOnlyList<string> NativeLibraries { get; }

        public IReadOnlyList<RuntimeAssetGroup> RuntimeAssemblyGroups { get; }

        public IReadOnlyList<RuntimeAssetGroup> NativeLibraryGroups { get; }

        public IReadOnlyList<ResourceAssembly> ResourceAssemblies { get; }
    }
}
