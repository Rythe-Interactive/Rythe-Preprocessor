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
        private static string moduleRoot = Directory.GetCurrentDirectory() + "\\";
        private static string moduleName;
        private static Dictionary<string, string> autogenHeaders = new Dictionary<string, string>();
        private static Dictionary<string, string> autogenInline = new Dictionary<string, string>();
        private static CppCompilation engineAST = null;
        private static CppParserOptions options = new CppParserOptions();
        private static List<Regex> excludePatterns = new List<Regex>();
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
                     "\t -moduleroot= : paths starting with \"-moduleroot=\" are included in the include directories and are considered the root of the module (the last instance of this command will be the one set)\n" +
                    "\t -include= : paths starting with\"-include=\" will be considered system includes and ignored when generating the AST\n" +
                    "\t -module= : arguments starting with \"-module=\" are considered the name of the module, if one is not given it will be extracted from the root\n" +
                    "\t -ex= : arguments starting with \"-ex=\" are taken as exclusion patterns.\n"
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
            Regex moduleInclude = new Regex("-moduleroot=(.*)");
            Regex includePath = new Regex("-include=(.*)");
            Regex moduleRegex = new Regex("-module=(.*)");
            Regex starReplace = new Regex("([^\\.*]*)\\*([^\\*]*)");


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
                    var cmd = command.Replace("-moduleroot=", "");
                    if (!cmd.EndsWith('\\'))
                        cmd += "\\";
                    moduleRoot = Directory.GetParent(cmd).ToString();
                    options.IncludeFolders.Add(moduleRoot);
                    Console.WriteLine($"Moduleroot:{moduleRoot}");
                    log += $"Moduleroot:{ moduleRoot}\n";
                }
                else if (includePath.Match(command).Success)
                {
                    var cmd = command.Replace("-include=", "");
                    if (!cmd.EndsWith('\\'))
                        cmd += "\\";
                    options.SystemIncludeFolders.Add(Directory.GetParent(cmd).ToString());
                    Console.WriteLine($"Include Directory: {Directory.GetParent(cmd).ToString()}");
                    log += $"Include Directory: {Directory.GetParent(cmd).ToString()}\n";
                }
                else if (moduleRegex.Match(command).Success)
                {
                    var cmd = command.Replace("-module=", "");
                    moduleName = cmd;
                }
            }

            if (moduleName == null)
            {
                var split = moduleRoot.Split('\\');
                moduleName = split[split.Length - 1];
            }

            Console.WriteLine($"Module Name: {moduleName}");
            log += $"Module Name: {moduleName}\n";

            if (ProcessDir())
            {
                Directory.CreateDirectory($@"{moduleRoot}\{moduleName}\autogen");
                SearchNamespaces(engineAST.Namespaces);
                GenerateCode(engineAST.Classes);

                if (autogenHeaders.ContainsKey(moduleName))
                    File.WriteAllText($@"{moduleRoot}\{moduleName}\autogen\autogen.hpp", $"{autogenHeaders[moduleName]}\n");

                if (autogenInline.ContainsKey(moduleName))
                    File.WriteAllText($@"{moduleRoot}\{moduleName}\autogen\autogen.cpp", $"{autogenInline[moduleName]}");
            }
            Console.WriteLine($"Scanned {filesChecked} files and generated {filesCreated} files in total.");
            log += $"Scanned {filesChecked} files and generated {filesCreated} files in total.\n";

            File.WriteAllText("Output.txt", log);
            Console.WriteLine("Log Written to file");
        }

        static bool ProcessDir()
        {
            string[] fileTypes = { "*.h", "*.hpp" };
            var fileDirs = new List<string>();

            foreach (string type in fileTypes)
                foreach (String fileDir in Directory.GetFiles(moduleRoot + "\\" + moduleName, type, SearchOption.AllDirectories))
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
                        if (!File.Exists(fileDir))
                        {
                            Console.WriteLine($"[ProcessDir] File Path {fileDir} does not exist");
                            log += $"[ProcessDir] File Path {fileDir} does not exist\n";
                            continue;
                        }

                        filesChecked++;
                        fileDirs.Add(fileDir);
                        Console.WriteLine("Adding File Directory to parse list: {0}", fileDir);
                        log += $"Adding File Directory to parse list: {fileDir}\n";
                    }
                }
            Console.WriteLine($"Finished searching {moduleRoot}\\{moduleName}");
            log += $"Finished searching {moduleRoot}\\{moduleName}\n";

            //Parse the whole engine and its modules
            Stopwatch watch = new Stopwatch();
            watch.Start();
            engineAST = CppParser.ParseFiles(fileDirs, options);
            watch.Stop();
            Console.WriteLine($"Classes Found: {engineAST.Classes.Count}");
            log += $"Classes Found: {engineAST.Classes.Count}\n";
            foreach (var diagnostic in engineAST.Diagnostics.Messages)
            {
                if (diagnostic.Type == CppLogMessageType.Warning)
                    log += "W:" + diagnostic.Text + "\n";
                if (diagnostic.Type == CppLogMessageType.Info)
                    log += "I:" + diagnostic.Text + "\n";
                if (diagnostic.Type == CppLogMessageType.Error)
                {
                    Console.WriteLine(diagnostic);
                    log += "E:" + diagnostic.Text + "\n";
                }
            }
            if (!engineAST.HasErrors)
            {
                Console.WriteLine($"AST Generation completed in {watch.Elapsed.TotalSeconds} seconds");
                log += $"AST Generation completed in {watch.Elapsed.TotalSeconds} seconds\n";
                return true;
            }
            else
            {
                Console.WriteLine("AST Generation had errors and did not complete");
                log += "AST Generation had errors and did not complete\n";
                return false;
            }
        }

        static void GenerateCode(CppContainerList<CppClass> classes, string nameSpace = "")
        {
            foreach (var cppStruct in classes)
            {
                if (cppStruct.Name.Equals("example_comp"))
                    Console.WriteLine("Example_comp");
                if (cppStruct.SourceFile == null)
                    continue;

                if (!cppStruct.SourceFile.Contains(moduleName))
                    continue;

                string depPath = " ";
                if (cppStruct.SourceFile.Contains(moduleRoot + "\\"))
                    depPath = cppStruct.SourceFile.Replace(moduleRoot + "\\", "");
                depPath = depPath.Replace("\\", "/");

                string source = File.ReadAllText(cppStruct.SourceFile);
                Regex removeGroupComments = new Regex(@"\/\*[\s|\S]*?\*\/");
                Regex removeComments = new Regex(@"\/\/[\t| |\S]*");
                source = removeComments.Replace(source, "");
                source = removeGroupComments.Replace(source, "");

                foreach (CppAttribute attr in cppStruct.Attributes)
                {
                    Regex findStruct = new Regex($@"\b(?:struct|class)\b\s*\[\[{attr.Scope}::{attr.Name}\]\]\s*{cppStruct.Name}[\s|<|>|\w|:| |\n]*\{{");
                    if (!findStruct.IsMatch(source))
                        continue;

                    if (attr.Name.Equals("reflectable"))
                    {
                        if (!autogenHeaders.ContainsKey(moduleName))
                            autogenHeaders.Add(moduleName, "#pragma once\n#include <core/platform/platform.hpp>\n#if __has_include_next(\"autogen.hpp\")\n#include_next \"autogen.hpp\"\n#endif\n");
                        if (!autogenInline.ContainsKey(moduleName))
                            autogenInline.Add(moduleName, "#include \"autogen.hpp\"\n#pragma once\n");

                        WriteToHpp(cppStruct.Name, CodeGenerator.PrototypeHPP(cppStruct.Name, nameSpace), "prototype");
                        WriteToHpp(cppStruct.Name, CodeGenerator.ReflectorHPP(cppStruct.Name, nameSpace), "reflector");

                        WriteToInl(cppStruct.Name, CodeGenerator.PrototypeInl(cppStruct.Name, nameSpace, depPath, cppStruct.Fields, cppStruct.Attributes), "prototype");
                        WriteToInl(cppStruct.Name, CodeGenerator.ReflectorInl(cppStruct.Name, nameSpace, depPath, cppStruct.Fields, cppStruct.Attributes), "reflector");
                        Console.WriteLine($"Generated files for {cppStruct.Name}");
                    }
                }
            }
        }
        static void SearchNamespaces(CppContainerList<CppNamespace> Namespaces, string parentNameSpace = "")
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
                    GenerateCode(nameSpace.Classes, combinedNameSpace);
                }
                else
                {
                    SearchNamespaces(nameSpace.Namespaces, combinedNameSpace);
                    GenerateCode(nameSpace.Classes, combinedNameSpace);
                }
            }
        }
        static void WriteToHpp(string structName, string contents, string type)
        {
            string writePath = $@"{moduleRoot}\{moduleName}\autogen\autogen_{type}_{structName}.hpp";
            autogenHeaders[moduleName] += $"#include \"{writePath}\"\n".Replace($@"{moduleRoot}\{moduleName}\autogen\", "");
            filesCreated++;
            File.WriteAllText(writePath, contents);
        }
        static void WriteToInl(string structName, string contents, string type)
        {
            string writePath = $@"{moduleRoot}\{moduleName}\autogen\autogen_{type}_{structName}.inl";
            autogenInline[moduleName] += $"#include \"autogen_{type}_{structName}.inl\"\n";
            filesCreated++;
            File.WriteAllText(writePath, contents);
        }
    }
}
