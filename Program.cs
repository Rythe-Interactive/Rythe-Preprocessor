using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CppAst;
using System.Diagnostics;

namespace RytheTributary
{
    class Program
    {
        static int filesCreated = 0;
        static int filesChecked = 0;
        private static List<string> paths = new List<string>();
        private static List<string> moduleRoots = new List<string>();
        private static Dictionary<string, string> autogenHeaders = new Dictionary<string, string>();
        private static Dictionary<string, string> autogenInline = new Dictionary<string, string>();
        private static CppCompilation engineAST = null;
        private static CppParserOptions options = new CppParserOptions();
        private static string log;

        static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "-help" || args[0] == "-h")
            {
                Console.WriteLine(
                    "|###############################|\n" +
                    "|###---rythe_preprocessor---####|\n" +
                    "|###############################|\n" +
                    "A tool for automatically generating code for additional functionality in the rythe game engine.\n\n" +
                    "arguments:\n" +
                    "\t -ex= : arguments starting with \"-ex=\" are taken as exclusion patterns.\n" +
                    "\t -include= : paths starting with\"-include=\" will be considered system includes and ignored when generating the AST\n" +
                    "\t -moduleinlucde= : paths starting with \"-moduleinclude=\" are the include directories for a module\n" +
                    "\t -module= : arguments starting with \"-module=\" are the directory(s) of your module(s)\n"
                    );
                Console.ReadKey();
                return;
            }
            foreach (string arg in args)
            {
                log += arg + "\n";
                Console.WriteLine(arg);
            }
            Regex excludeRegex = new Regex("-ex=(.*)");
            Regex moduleInclude = new Regex("-moduleinclude=(.*)");
            Regex includePath = new Regex("-include=(.*)");
            Regex moduleRegex = new Regex("-module=(.*)");
            Regex starReplace = new Regex("([^\\.*]*)\\*([^\\*]*)");

            Dictionary<string, string> modulePaths = new Dictionary<string, string>();
            List<Regex> excludePatterns = new List<Regex>();

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
                else if (moduleInclude.Match(command).Success)
                {
                    var cmd = command.Replace("-moduleinclude=", "");
                    if (!cmd.EndsWith('\\'))
                        cmd += "\\";
                    moduleRoots.Add(cmd);
                    options.IncludeFolders.Add(cmd);
                }
                else if (includePath.Match(command).Success)
                {
                    var cmd = command.Replace("-include=", "");
                    if (!cmd.EndsWith('\\'))
                        cmd += "\\";
                    options.SystemIncludeFolders.Add(cmd);
                }
                else if (moduleRegex.Match(command).Success)
                {
                    var cmd = command.Replace("-module=", "");
                    if (!cmd.EndsWith('\\'))
                        cmd += "\\";
                    foreach (string root in moduleRoots)
                    {
                        if (Directory.Exists(root + cmd))
                        {
                            modulePaths.Add(cmd, root + cmd);
                            break;
                        }
                    }
                }
            }

            paths.AddRange(modulePaths.Values);
            foreach (string path in paths)
            {
                Console.WriteLine(path);
                log += path + "\n";
            }
            ProcessDir(paths.ToArray(), excludePatterns);

            foreach (string name in modulePaths.Keys)
            {
                Directory.CreateDirectory($@"{modulePaths[name]}autogen");
                ProcessProject(name, modulePaths[name]);

                if (autogenHeaders.ContainsKey(name))
                    File.WriteAllText($@"{modulePaths[name]}autogen\autogen.hpp", $"{autogenHeaders[name]}\n");

                if (autogenInline.ContainsKey(name))
                    File.WriteAllText($@"{modulePaths[name]}autogen\autogen.cpp", $"{autogenInline[name]}");
            }

            Console.WriteLine($"Scanned {filesChecked} files and generated {filesCreated} files in total.");
            File.WriteAllText("D:\\Repos\\Rythe-legacy\\Output.txt", log);
            Console.WriteLine("Log Written to file");
            Console.WriteLine("Press any button to close");
            Console.ReadKey();
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
                            log += $"Adding File Directory to parse list: {fileDir}\n";
                        }
                    }
                Console.WriteLine("Finished searching {0}", path);
            }

            //Parse the whole engine and its modules
            Stopwatch watch = new Stopwatch();
            watch.Start();
            engineAST = CppParser.ParseFiles(fileDirs, options);
            watch.Stop();
            Console.WriteLine(engineAST.Classes.Count);
            if (!engineAST.HasErrors)
                Console.WriteLine($"AST Generation completed in {watch.Elapsed.TotalSeconds} seconds");
            foreach (var diagnostic in engineAST.Diagnostics.Messages)
            {
                if(diagnostic.Type == CppLogMessageType.Warning)
                    log += "W:" + diagnostic.Text + "\n";
                if (diagnostic.Type == CppLogMessageType.Info)
                    log += "I:" + diagnostic.Text + "\n";
                if (diagnostic.Type == CppLogMessageType.Error)
                    log += "E:" + diagnostic.Text + "\n";
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

                if (!cppStruct.SourceFile.Contains(projectName))
                    continue;

                string depPath = " ";
                foreach (string incPath in options.IncludeFolders)
                {
                    if (cppStruct.SourceFile.Contains(incPath))
                        depPath = cppStruct.SourceFile.Replace(incPath, "");
                }
                depPath = depPath.Replace("\\", "/");

                string source = File.ReadAllText(cppStruct.SourceFile);
                Regex removeGroupComments = new Regex(@"\/\*[\s|\S]*?\*\/");
                Regex removeComments = new Regex(@"\/\/[\t| |\S]*");
                Regex findStruct = new Regex($@"\b(?:struct|class)\b\s*\[\[legion::reflectable\]\]\s*{cppStruct.Name}[\s|<|>|\w|:| |\n]*\{{");
                source = removeComments.Replace(source, "");
                source = removeGroupComments.Replace(source, "");
                if (!findStruct.IsMatch(source))
                    continue;
                foreach (CppAttribute attr in cppStruct.Attributes)
                {
                    if (!attr.Scope.Contains("legion") && !attr.Scope.Contains("rythe"))
                        continue;

                    if (attr.Name.Equals("reflectable"))
                    {
                        if (!autogenHeaders.ContainsKey(projectName))
                            autogenHeaders.Add(projectName, "#pragma once\n#include <core/platform/platform.hpp>\n#if __has_include_next(\"autogen.hpp\")\n#include_next \"autogen.hpp\"\n#endif\n");
                        if (!autogenInline.ContainsKey(projectName))
                            autogenInline.Add(projectName, "#include \"autogen.hpp\"\n#pragma once\n");

                        WriteToHpp(projectName, cppStruct.Name, CodeGenerator.PrototypeHPP(cppStruct.Name, nameSpace), path, "prototype");
                        WriteToHpp(projectName, cppStruct.Name, CodeGenerator.ReflectorHPP(cppStruct.Name, nameSpace), path, "reflector");

                        WriteToInl(projectName, cppStruct.Name, CodeGenerator.PrototypeInl(cppStruct.Name, nameSpace, depPath, cppStruct.Fields, cppStruct.Attributes), path, "prototype");
                        WriteToInl(projectName, cppStruct.Name, CodeGenerator.ReflectorInl(cppStruct.Name, nameSpace, depPath, cppStruct.Fields, cppStruct.Attributes), path, "reflector");
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
            autogenHeaders[projectName] += $"#include \"{writePath}\"\n".Replace(path + @"autogen\", "");
            filesCreated++;
            File.WriteAllText(writePath, contents);
        }
        static void WriteToInl(string projectName, string structName, string contents, string path, string type)
        {
            string writePath = $@"{path}autogen\autogen_{type}_{structName}.inl";
            autogenInline[projectName] += $"#include \"autogen_{type}_{structName}.inl\"\n";
            filesCreated++;
            File.WriteAllText(writePath, contents);
        }
    }
}
