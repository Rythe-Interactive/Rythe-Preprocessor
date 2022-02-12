using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CppAst;
using CppAst.CodeGen.Common;

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
            Regex applicationRegex = new Regex("-app=(.*)");
            Regex moduleRegex = new Regex("-module=(.*)");
            Regex starReplace = new Regex("([^\\.*]*)\\*([^\\*]*)");

            Dictionary<string, string> applicationPaths = new Dictionary<string, string>();
            Dictionary<string, string> modulePaths = new Dictionary<string, string>();
            List<Regex> excludePatterns = new List<Regex>();

            if (args[0].StartsWith("-root="))
                projectDirectory = args[0].Replace("-root=", "");

            options = options.ConfigureForWindowsMsvc(CppTargetCpu.X86_64, CppVisualStudioVersion.VS2019);
            options.AdditionalArguments.Add("-std=c++17");
            options.Defines.Add("L_AUTOGENACTIVE");
            options.ParseMacros = true;
            options.ParseAttributes = true;
            options.IncludeFolders.Add($@"{projectDirectory}deps\include\");
            options.IncludeFolders.Add($@"{projectDirectory}legion\engine\");

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
                else if (applicationRegex.Match(command).Success)
                {
                    var cmd = command.Replace("-app=", "");
                    applicationPaths.Add(cmd, $@"{projectDirectory}applications\{cmd}\");
                }
                else if (moduleRegex.Match(command).Success)
                {
                    var cmd = command.Replace("-module=", "");
                    modulePaths.Add(cmd, $@"{projectDirectory}legion\engine\{cmd}\");
                }
            }

            paths.AddRange(modulePaths.Values);
            ProcessDir(paths.ToArray(), excludePatterns);

            foreach (string name in modulePaths.Keys)
            {
                Directory.CreateDirectory($@"{modulePaths[name]}autogen");
                ProcessProject(name, modulePaths[name]);
                File.WriteAllText($@"{modulePaths[name]}autogen\autogen.hpp", $"#pragma once \n #include_next \"autogen.hpp\" \n{autogenIncludes[name]}");
            }
            autogenIncludes.Clear();
            paths.Clear();
            paths.AddRange(applicationPaths.Values);
            ProcessDir(paths.ToArray(), excludePatterns);

            foreach (string name in applicationPaths.Keys)
            {
                Directory.CreateDirectory($@"{applicationPaths[name]}\autogen");
                ProcessProject(name, applicationPaths[name]);
                File.WriteAllText($@"{applicationPaths[name]}\autogen\autogen.hpp", $"#pragma once \n #include_next \"autogen.hpp\" \n{autogenIncludes[name]}");
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
                Console.WriteLine("Done with {0}", path);
                Console.WriteLine("");
            }

            //Parse the whole engine and its modules
            engineAST = CppParser.ParseFiles(fileDirs, options);
            foreach (var diagnostic in engineAST.Diagnostics.Messages)
            {
                if (diagnostic.Type == CppLogMessageType.Error)
                    Console.WriteLine(diagnostic);
            }
        }
        static void ProcessProject(string projectName, string path)
        {
            SearchNamespaces(projectName, path, engineAST.Namespaces);
            GenerateCode(projectName, path, engineAST.Classes);
        }

        static void GenerateCode(string projectName, string path, CppContainerList<CppClass> classes)
        {
            foreach (var cppStruct in classes)
            {
                string depPath = cppStruct.SourceFile.ToLower().Contains($@"{projectDirectory.ToLower()}legion\engine\") ? cppStruct.SourceFile.ToLower().Replace($@"{projectDirectory.ToLower()}legion\engine\", "") : cppStruct.SourceFile.ToLower().Replace($@"{projectDirectory.ToLower()}applications\", "");
                if (!depPath.Contains(projectName.ToLower()))
                    continue;
                
                foreach (CppAttribute attr in cppStruct.Attributes)
                {
                    if (!attr.Scope.Contains("legion") && !attr.Scope.Contains("rythe"))
                        continue;

                    if (attr.Name.Equals("reflectable"))
                    {
                        if (!autogenIncludes.ContainsKey(projectName))
                            autogenIncludes.Add(projectName, "");

                        Console.WriteLine(cppStruct.Name);
                        string hppData = CodeGenerator.PrototypeHPP(cppStruct.Name);
                        Console.WriteLine(hppData);
                        string writePath = $@"{path}autogen\autogen_prototype_{cppStruct.Name}.hpp";
                        autogenIncludes[projectName] += $"#include \"{writePath}\"\n".Replace(path + @"autogen\", "");
                        filesCreated++;
                        File.WriteAllText(writePath, hppData);

                        hppData = CodeGenerator.ReflectorHPP(cppStruct.Name);
                        Console.WriteLine(hppData);
                        writePath = $@"{path}autogen\autogen_reflector_{cppStruct.Name}.hpp";
                        autogenIncludes[projectName] += $"#include \"{writePath}\"\n".Replace(path + @"autogen\", "");
                        filesCreated++;
                        File.WriteAllText(writePath, hppData);

                        depPath = cppStruct.SourceFile.ToLower().Contains($@"{projectDirectory.ToLower()}legion\engine\") ? cppStruct.SourceFile.ToLower().Replace($@"{projectDirectory.ToLower()}legion\engine\", "") : cppStruct.SourceFile.ToLower().Replace($@"{projectDirectory.ToLower()}applications\", "");
                        string cppData = CodeGenerator.PrototypeCPP(cppStruct.Name, depPath, cppStruct.Fields);
                        Console.WriteLine(cppData);
                        writePath = $@"{path}\autogen\autogen_prototype_{cppStruct.Name}.cpp";
                        filesCreated++;
                        File.WriteAllText(writePath, cppData);

                        cppData = CodeGenerator.ReflectorCPP(cppStruct.Name, depPath, cppStruct.Fields);
                        Console.WriteLine(cppData);
                        writePath = $@"{path}\autogen\autogen_reflector_{cppStruct.Name}.cpp";
                        filesCreated++;
                        File.WriteAllText(writePath, cppData);
                    }
                }
            }
        }

        static void SearchNamespaces(string name, string path, CppContainerList<CppNamespace> Namespaces)
        {
            foreach (var nameSpace in Namespaces)
            {
                if (nameSpace.Namespaces.Count < 1)
                {
                    GenerateCode(name, path, nameSpace.Classes);
                }
                else
                {
                    SearchNamespaces(name, path, nameSpace.Namespaces);
                    GenerateCode(name, path, nameSpace.Classes);
                }
            }
        }
    }
}
