// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.Extensions.DependencyModel;
using NuGet.Frameworks;

namespace RuntimeGraphGenerator
{
    public class Program
    {
        public static List<RuntimeLibrary> RemoveReferences(IReadOnlyList<RuntimeLibrary> runtimeLibraries, IReadOnlyList<string> refname)
        {
            List<RuntimeLibrary> result = new List<RuntimeLibrary>();

            foreach (var runtimeLib in runtimeLibraries)
            {
                if (string.IsNullOrEmpty(refname.FirstOrDefault(elem => runtimeLib.Name.ToLower().Contains(elem))))
                {
                    List<Dependency> toRemoveDependecy = new List<Dependency>();
                    foreach (var dependency in runtimeLib.Dependencies)
                    {
                        if (!string.IsNullOrEmpty(refname.FirstOrDefault(elem => dependency.Name.ToLower().Contains(elem))))
                        {
                            toRemoveDependecy.Add(dependency);
                        }
                    }

                    if (toRemoveDependecy.Count > 0)
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
                    else if (string.IsNullOrEmpty(refname.FirstOrDefault(elem => runtimeLib.Name.ToLower().Contains(elem))))
                    {
                        result.Add(runtimeLib);
                    }
                }
            }
            return result;
        }

        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            string projectDirectory = null;
            string depsFile = null;
            IReadOnlyList<string> runtimes = null;
            IReadOnlyList<string> runtimepackagesToBeRemoved = null;
            try
            {
                ArgumentSyntax.Parse(args, syntax =>
                {
                    syntax.ApplicationName = "Runtime GraphGenerator";

                    syntax.HandleHelp = false;
                    syntax.HandleErrors = false;

                    syntax.DefineOption("p|project", ref projectDirectory, "Project location");
                    syntax.DefineOption("d|deps", ref depsFile, "Deps file path");
                    syntax.DefineOptionList("r|remove", ref runtimepackagesToBeRemoved, "Runtime packages to be removed");

                    syntax.DefineParameterList("runtimes", ref runtimes, "Runtimes");
                });
            }
            catch (ArgumentSyntaxException exception)
            {
                Console.Error.WriteLine(exception.Message);
                return 1;
            }

            if (runtimes == null || runtimes.Count == 0)
            {
                Reporter.Error.WriteLine("No runtimes specified");
                return 1;
            }
            if (!File.Exists(depsFile))
            {
                Reporter.Error.WriteLine($"Deps file not found: {depsFile}");
                return 1;
            }
            if (!Directory.Exists(projectDirectory))
            {
                Reporter.Error.WriteLine($"Project directory not found: {projectDirectory}");
                return 1;
            }

            try
            {
                DependencyContext context;
                using (var depsStream = File.OpenRead(depsFile))
                {
                    context = new DependencyContextJsonReader().Read(depsStream);
                }
                var framework = NuGetFramework.Parse(context.Target.Framework);
                var projectContext = ProjectContext.Create(projectDirectory, framework);

                // Configuration is used only for P2P dependencies so were don't care
                var exporter = projectContext.CreateExporter("Debug");
                var manager = new RuntimeGraphManager();
                var graph = manager.Collect(exporter.GetDependencies(LibraryType.Package));
                var expandedGraph = manager.Expand(graph, runtimes);

                var trimmedRuntimeLibraries = context.RuntimeLibraries;

                if (runtimepackagesToBeRemoved != null && runtimepackagesToBeRemoved.Count > 0)
                {
                    runtimepackagesToBeRemoved = runtimepackagesToBeRemoved.Select(x => x.ToLower()).ToList(); ;
                    trimmedRuntimeLibraries = RemoveReferences(context.RuntimeLibraries, runtimepackagesToBeRemoved);
                }

                context = new DependencyContext(
                    context.Target,
                    context.CompilationOptions,
                    context.CompileLibraries,
                    trimmedRuntimeLibraries,
                    expandedGraph
                    );

                using (var depsStream = File.Create(depsFile))
                {
                    new DependencyContextWriter().Write(context, depsStream);
                }

                return 0;
            }
            catch (Exception ex)
            {
#if DEBUG
                Reporter.Error.WriteLine(ex.ToString());
#else
                Reporter.Error.WriteLine(ex.Message);
#endif
                return 1;
            }
        }

    }
}
