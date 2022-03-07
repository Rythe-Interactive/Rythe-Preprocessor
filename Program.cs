using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CppAst;
using CppAst.CodeGen.Common;
using System.Diagnostics;

namespace RytheTributary
{
    class Program
    {
        static int filesCreated = 0;
        static int filesChecked = 0;
        private static List<string> paths = new List<string>();
        private static Dictionary<string, string> autogenIncludes = new Dictionary<string, string>();
        private static CppCompilation engineAST = null;
        private static CppParserOptions options = new CppParserOptions();

        private static string projectDirectory;

        static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "-help" || args[0] == "-h")
            {
                Console.WriteLine(
                    "\t---rythe_preprocessor---\n" +
                    "\tA tool for automatically generating code for additional functionality.\n\n" +
                    "\targuments:" +
                    "\t     -ex= : arguments starting with \"-ex=\" are taken as exclusion patterns.\n" +
                    "\t     -app= : arguments starting with \"-app=\" are the directory(s)of your application(s)" +
                    "\t     -module= : arguments starting with \"-module=\" are the directory(s) of your module(s)"
                    );
                return;
            }

            Regex excludeRegex = new Regex("-ex=(.*)");
            Regex includePath = new Regex("-include=(.*)");
            Regex systemIncludePath = new Regex("-sysinclude=(.*)");
            Regex moduleRegex = new Regex("-module=(.*)");
            Regex starReplace = new Regex("([^\\.*]*)\\*([^\\*]*)");

            Dictionary<string, string> modulePaths = new Dictionary<string, string>();
            List<Regex> excludePatterns = new List<Regex>();

            if (args[0].StartsWith("-root="))
                projectDirectory = args[0].Replace("-root=", "");

            options = options.ConfigureForWindowsMsvc(CppTargetCpu.X86_64, CppVisualStudioVersion.VS2019);
            options.AdditionalArguments.Add("-std=c++17");
            options.Defines.Add("L_AUTOGENACTIVE");
            options.ParseMacros = false;
            options.ParseAttributes = true;
            options.ParseSystemIncludes = false;

            foreach (string command in args)
            {
                Match match;
                if ((match = excludeRegex.Match(command)).Success)
                {
                    string excludePattern = match.Groups[1].Value;
                    excludePattern = excludePattern.Replace("\"", "");
                    excludePattern = excludePattern.Replace("\\", "\\\\");
                    excludePattern = excludePattern.Replace("/", "\\\\");
                    excludePattern = excludePattern.Replace("**", ".*");
                    excludePattern = starReplace.Replace(excludePattern, "$1[^\\\\]*$2");

                    excludePatterns.Add(new Regex(excludePattern));
                }
                else if (includePath.Match(command).Success)
                {
                    var cmd = command.Replace("-include=", "");
                    if (!cmd.EndsWith('\\'))
                        cmd += "\\";
                    options.IncludeFolders.Add($@"{projectDirectory}{cmd}");
                }
                else if (systemIncludePath.Match(command).Success)
                {
                    var cmd = command.Replace("-sysinclude=", "");
                    if (!cmd.EndsWith('\\'))
                        cmd += "\\";
                    options.SystemIncludeFolders.Add($@"{projectDirectory}{cmd}");
                }
                else if (moduleRegex.Match(command).Success)
                {
                    var cmd = command.Replace("-module=", "");
                    if (!cmd.EndsWith('\\'))
                        cmd += "\\";
                    modulePaths.Add(cmd, $@"{projectDirectory}{cmd}");
                }
            }

            paths.AddRange(modulePaths.Values);
            ProcessDir(paths.ToArray(), excludePatterns);

            foreach (string name in modulePaths.Keys)
            {
                Directory.CreateDirectory($@"{modulePaths[name]}autogen");
                ProcessProject(name, modulePaths[name]);
                if (autogenIncludes.ContainsKey(name))
                    File.WriteAllText($@"{modulePaths[name]}autogen\autogen.hpp", $"{autogenIncludes[name]}");
            }

            Console.WriteLine($"Checked {filesChecked} files and added {filesCreated} files in total.");
        }

        static void ProcessDir(string[] pathsToProcess, List<Regex> excludePatterns)
        {
            string[] fileTypes = { "*.h", "*.hpp" };
            var fileDirs = new List<string>();

            foreach (string path in pathsToProcess)
            {
                foreach (string type in fileTypes)
                    foreach (String fileDir in Directory.GetFiles(path, type, SearchOption.AllDirectories))
                    {
                        bool matched = false;
                        foreach (Regex regex in excludePatterns)
                            if (regex.IsMatch(fileDir))
                            {
                                matched = true;
                                break;
                            }

                        if (!matched)
                        {
                            Console.WriteLine("Adding File Directory to parse list: {0}", fileDir);
                            filesChecked++;
                            fileDirs.Add(fileDir);
                        }
                    }
                Console.WriteLine("Done adding {0}", path);
            }

            //Parse the whole engine and its modules
            Stopwatch watch = new Stopwatch();
            watch.Start();
            engineAST = CppParser.ParseFiles(fileDirs, options);
            watch.Stop();
            Console.WriteLine(watch.Elapsed.TotalSeconds);
            foreach (var diagnostic in engineAST.Diagnostics.Messages)
            {
                if (diagnostic.Type == CppLogMessageType.Error)
                    Console.WriteLine(diagnostic);
            }
        }
        static void ProcessProject(string projectName, string path)
        {
            SearchNamespaces(projectName, path, "", engineAST.Namespaces);
            GenerateCode(projectName, path, "", engineAST.Classes);
        }

        static void GenerateCode(string projectName, string path, string nameSpace, CppContainerList<CppClass> classes)
        {
            foreach (var cppStruct in classes)
            {
                if (cppStruct.SourceFile == null)
                    continue;

                string depPath = " ";
                if (cppStruct.SourceFile.ToLower().Contains($@"{projectDirectory.ToLower()}legion\engine\"))
                    depPath = cppStruct.SourceFile.ToLower().Replace($@"{projectDirectory.ToLower()}legion\engine\", "");
                else
                    depPath = cppStruct.SourceFile.ToLower().Replace($@"{projectDirectory.ToLower()}applications\", "");

                if (!cppStruct.SourceFile.ToLower().Contains(projectName.ToLower()))
                    continue;

                string source = File.ReadAllText(cppStruct.SourceFile);
                Regex removeGroupComments = new Regex(@"\/\*[\s|\S]*\*\/");
                Regex removeComments = new Regex(@"\/\/[\t| |\S]*");
                Regex findStruct = new Regex($@"\b(?:struct|class)\b\s*\[\[legion::reflectable\]\]\s*{cppStruct.Name}[\s|<|>|\w|:| |\n]*\{{");
                source = removeComments.Replace(source, "");
                source = removeGroupComments.Replace(source, "");
                if (!findStruct.IsMatch(source))
                    continue;

                depPath = depPath.Replace("\\", "/");

                foreach (CppAttribute attr in cppStruct.Attributes)
                {
                    if (!attr.Scope.Contains("legion") && !attr.Scope.Contains("rythe"))
                        continue;

                    if (attr.Name.Equals("reflectable"))
                    {
                        if (!autogenIncludes.ContainsKey(projectName))
                            autogenIncludes.Add(projectName, "#pragma once\n#include <core/platform/platform.hpp>\n#if __has_include_next(\"autogen.hpp\")\n#include_next \"autogen.hpp\"\n#endif\n");

                        WriteToHpp(projectName, cppStruct.Name, CodeGenerator.PrototypeHPP(cppStruct.Name, nameSpace), path, "prototype");
                        WriteToHpp(projectName, cppStruct.Name, CodeGenerator.ReflectorHPP(cppStruct.Name, nameSpace), path, "reflector");

                        WriteToCPP(cppStruct.Name, CodeGenerator.PrototypeCPP(cppStruct.Name, nameSpace, depPath, cppStruct.Fields, cppStruct.Attributes), path, "prototype");
                        WriteToCPP(cppStruct.Name, CodeGenerator.ReflectorCPP(cppStruct.Name, nameSpace, depPath, cppStruct.Fields, cppStruct.Attributes), path, "reflector");
                        Console.WriteLine($"Generated files for {cppStruct.Name}");
                    }
                }
            }
        }
        static void SearchNamespaces(string name, string path, string parentNameSpace, CppContainerList<CppNamespace> Namespaces)
        {
            string combinedNameSpace = "";
            foreach (var nameSpace in Namespaces)
            {
                if (parentNameSpace.Length > 0 && nameSpace.Name.Length > 0)
                    combinedNameSpace = parentNameSpace + "::" + nameSpace.Name;
                else if (parentNameSpace.Length > 0)
                    combinedNameSpace = parentNameSpace;
                else
                    combinedNameSpace = nameSpace.Name;

                if (nameSpace.Namespaces.Count < 1)
                {
                    GenerateCode(name, path, combinedNameSpace, nameSpace.Classes);
                }
                else
                {
                    SearchNamespaces(name, path, combinedNameSpace, nameSpace.Namespaces);
                    GenerateCode(name, path, combinedNameSpace, nameSpace.Classes);
                }
            }
        }
        static void WriteToHpp(string projectName, string structName, string contents, string path, string type)
        {
            string writePath = $@"{path}autogen\autogen_{type}_{structName}.hpp";
            autogenIncludes[projectName] += $"#include \"{writePath}\"\n".Replace(path + @"autogen\", "");
            filesCreated++;
            File.WriteAllText(writePath, contents);
        }
        static void WriteToCPP(string structName, string contents, string path, string type)
        {
            string writePath = $@"{path}\autogen\autogen_{type}_{structName}.cpp";
            filesCreated++;
            File.WriteAllText(writePath, contents);
        }
    }
}
