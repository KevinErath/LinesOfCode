using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace RosLoc
{
    internal static class Program
    {
        private static void Main()
        {
            var filename = GetFilenameFromCmd();

            var methodsPerClass = GetAllMethodsPerClass(filename);

            var locPerMethod = CalculateLocPerMethod(methodsPerClass);
            var locPerClass = CalculateLocPerClass(locPerMethod);

            PrintReport(locPerClass, locPerMethod);
        }

        private static Dictionary<string, IEnumerable<MethodDeclarationSyntax>> GetAllMethodsPerClass(string filename)
        {
            var ws = CreateWorkspace();
            var projects = LoadProjectsFromFile(filename, ws);
            var documents = OpenAllDocuments(projects);
            var classes = FindAllClasses(documents);
            var methodsPerClass = FindAllMethodsForClasses(classes);
            return methodsPerClass;
        }

        private static Dictionary<string, int> CalculateLocPerClass(Dictionary<string, Dictionary<string, int>> locPerMethod)
        {
            var locPerClass = new Dictionary<string, int>();
            foreach (var cls in locPerMethod)
            {
                locPerClass[cls.Key] = cls.Value.Sum(x => x.Value);
            }
            return locPerClass;
        }

        private static Dictionary<string, Dictionary<string, int>> CalculateLocPerMethod(Dictionary<string, IEnumerable<MethodDeclarationSyntax>> methods)
        {
            var loc = new Dictionary<string, Dictionary<string, int>>();
            foreach (var cls in methods)
            {
                var clsLoc = new Dictionary<string, int>();
                loc[cls.Key] = clsLoc;
                foreach (var method in cls.Value)
                {
                    var block = method.DescendantNodes().OfType<BlockSyntax>().First();
                    var blockTokens = block.ChildNodes().SelectMany(x => x.DescendantTokens());
                    var withTrailing = blockTokens.Where(y => y.HasTrailingTrivia);
                    var count = withTrailing.SelectMany(x => x.TrailingTrivia).Count(m => m.IsKind(SyntaxKind.EndOfLineTrivia));
                    clsLoc[method.Identifier.Text] = count;
                }
            }
            return loc;
        }

        private static Dictionary<string, IEnumerable<MethodDeclarationSyntax>> FindAllMethodsForClasses(IEnumerable<ClassDeclarationSyntax> classes)
        {
            var methods = new Dictionary<string, IEnumerable<MethodDeclarationSyntax>>();
            foreach (var cls in classes)
            {
                methods[cls.Identifier.Text] = cls.DescendantNodes().OfType<MethodDeclarationSyntax>();
            }
            return methods;
        }

        private static void PrintReport(Dictionary<string, int> locPerClass, Dictionary<string, Dictionary<string, int>> loc)
        {
            foreach (var clsLoc in loc)
            {
                Console.WriteLine("{0}:{1}", clsLoc.Key, locPerClass[clsLoc.Key]);
                foreach (var methodLoc in clsLoc.Value)
                {
                    Console.WriteLine("- {0}:{1}", methodLoc.Key, methodLoc.Value);
                }
            }
        }

        private static IEnumerable<ClassDeclarationSyntax> FindAllClasses(IEnumerable<SyntaxNode> documents)
        {
            return documents.SelectMany(x => x.DescendantNodes().OfType<ClassDeclarationSyntax>());
        }

        private static IEnumerable<SyntaxNode> OpenAllDocuments(IEnumerable<Project> projects)
        {
            var documents = projects.SelectMany(x => x.Documents);
            var documentRoots = documents.Select(x => x.GetSyntaxRootAsync().Result);
            return documentRoots;
        }

        private static IEnumerable<Project> LoadProjectsFromFile(string filename, MSBuildWorkspace ws)
        {
            IEnumerable<Project> projects;
            if (Path.GetExtension(filename) == ".sln")
            {
                var solution = ws.OpenSolutionAsync(filename).Result;
                projects = solution.Projects;
            }
            else
            {
                var project = ws.OpenProjectAsync(filename).Result;
                projects = new List<Project> { project };
            }
            return projects;
        }

        private static MSBuildWorkspace CreateWorkspace()
        {
            return MSBuildWorkspace.Create();
        }

        private static string GetFilenameFromCmd()
        {
            return Environment.GetCommandLineArgs()[1];
        }
    }
}