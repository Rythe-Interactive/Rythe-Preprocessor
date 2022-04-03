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
        private static string autogenHeaders;
        private static string autogenImpl;
        public static uint filesCreated = 0;

        private static string autogenFolderPath;

        static void Main(string[] args)
        {
            Logger.Init();

            if (!CLArgs.Parse(args))
                return;

            Logger.Log("Enter Main", LogLevel.trace);

            Logger.Log($"Module Name: {CLArgs.moduleName}", LogLevel.info);

            autogenFolderPath = $@"{CLArgs.moduleRoot}\{CLArgs.moduleName}\autogen";

            string autogenHeaderPath = $@"{autogenFolderPath}\autogen.hpp";
            string autogenImplPath = $@"{autogenFolderPath}\autogen.cpp";

            if (Directory.Exists(autogenFolderPath))
                IterateFiles(autogenFolderPath, new string[] { "*" }, (string filePath) =>
                {
                    File.Delete(filePath);
                }, SearchOption.TopDirectoryOnly);
            else
                Directory.CreateDirectory(autogenFolderPath);

            File.WriteAllText(autogenHeaderPath, "#pragma once\n#include <core/platform/platform.hpp>\n#if !defined(L_AUTOGENACTIVE)\nL_ERROR(\"Preprocessor still busy, or crashed.\");\n#endif\n");
            File.WriteAllText(autogenImplPath, "#include <core/platform/platform.hpp>\n#if !defined(L_AUTOGENACTIVE)\nL_ERROR(\"Preprocessor still busy, or crashed.\");\n#endif\n");

            if (ModuleAST.GenerateAST())
            {
                autogenHeaders = "#pragma once\n#include <core/platform/platform.hpp>\n#if __has_include_next(\"autogen.hpp\")\n#include_next \"autogen.hpp\"\n#endif\n";
                autogenImpl = "#include \"autogen.hpp\"\n";

                AddOverrideFiles();

                SearchNamespaces(ModuleAST.Namespaces);
                GenerateCode(ModuleAST.Classes);

                File.WriteAllText(autogenHeaderPath, $"{autogenHeaders}\n");

                File.WriteAllText(autogenImplPath, $"{autogenImpl}");
            }
            Logger.Log($"Scanned {ModuleAST.filesChecked} files and generated {filesCreated} files in total.", LogLevel.info);

            Logger.Log("Exit Main", LogLevel.trace);
            Logger.WriteToFile("PreProcessor");
        }

        static string GetRelativePath(string basePath, string targetPath)
        {
            return (new Uri(basePath)).MakeRelativeUri(new Uri(targetPath)).OriginalString;
        }

        static void IterateFiles(string path, string[] searchPatterns, Action<string> fileIteration, SearchOption searchOption = SearchOption.AllDirectories)
        {
            foreach (string patern in searchPatterns)
                foreach (var filePath in Directory.GetFileSystemEntries(path, patern, searchOption))
                {
                    FileAttributes attr = File.GetAttributes(filePath);

                    if (!attr.HasFlag(FileAttributes.Directory))
                    {
                        fileIteration(filePath);
                    }
                }
        }

        static void AddOverrideFiles()
        {
            string overridePath = $@"{autogenFolderPath}\override";
            if (Directory.Exists(overridePath))
            {
                string[] headerTypes = { "*.h", "*.hpp" };

                IterateFiles(overridePath, headerTypes, (string path) =>
                {
                    autogenHeaders += $"#include \"{GetRelativePath(overridePath, path)}\"\n";
                    Logger.Log($"Including override header: {path}", LogLevel.debug);
                });

                string[] implTypes = { "*.inl" };

                IterateFiles(overridePath, implTypes, (string path) =>
                {
                    autogenImpl += $"#include \"{GetRelativePath(overridePath, path)}\"\n";
                    Logger.Log($"Including override implementation: {path}", LogLevel.debug);
                });
            }
        }

        static void GenerateCode(CppContainerList<CppClass> classes, string nameSpace = "")
        {
            Logger.Log("Enter GenerateCode", LogLevel.trace);
            foreach (var cppStruct in classes)
            {
                if (cppStruct.SourceFile == null)
                    continue;

                if (!cppStruct.SourceFile.Contains(CLArgs.moduleName))
                    continue;

                bool matched = false;
                foreach (Regex regex in CLArgs.excludePatterns)
                    if (regex.IsMatch(cppStruct.SourceFile))
                    {
                        matched = true;
                        break;
                    }

                if (matched)
                    continue;

                string depPath = " ";
                if (cppStruct.SourceFile.Contains(CLArgs.moduleRoot + "\\"))
                    depPath = cppStruct.SourceFile.Replace(CLArgs.moduleRoot + "\\", "");
                depPath = depPath.Replace("\\", "/");

                string source = File.ReadAllText(cppStruct.SourceFile);
                Regex removeGroupComments = new Regex(@"\/\*[\s|\S]*?\*\/");
                Regex removeComments = new Regex(@"\/\/[\t| |\S]*");
                source = removeComments.Replace(source, "");
                source = removeGroupComments.Replace(source, "");

                Regex findStruct = new Regex($@"\b(?:struct|class)\b\s*.*\s*{cppStruct.Name}\s*(?:\s*\:[\s|<|>|\w|\:]*)?\s*\{{");
                if (!findStruct.IsMatch(source))
                    continue;

                Regex isClassRegex = new Regex($@"\b(?:class)\b\s*.*\s*{cppStruct.Name}\s*(?:\s*\:[\s|<|>|\w|\:]*)?\s*\{{");

                bool reflectable = false;
                bool no_reflect = isClassRegex.IsMatch(source);
                foreach (CppAttribute attr in cppStruct.Attributes)
                {
                    if (attr.Name.Equals("reflectable"))
                    {
                        reflectable = true;
                        no_reflect = false;
                        break;
                    }
                    else if (attr.Name.Equals("no_reflect"))
                    {
                        no_reflect = true;
                        break;
                    }
                }

                if (no_reflect)
                    continue;

                WriteToHpp(cppStruct.Name, CodeGenerator.PrototypeHeader(cppStruct.Name, nameSpace), "prototype");
                WriteToHpp(cppStruct.Name, CodeGenerator.ReflectorHeader(cppStruct.Name, nameSpace), "reflector");

                if (reflectable)
                {
                    WriteToInl(cppStruct.Name, CodeGenerator.PrototypeImpl(cppStruct, nameSpace, depPath), "prototype");
                    WriteToInl(cppStruct.Name, CodeGenerator.ReflectorImpl(cppStruct, nameSpace, depPath), "reflector");
                }
                else
                {
                    WriteToInl(cppStruct.Name, CodeGenerator.DummyPrototypeImpl(cppStruct, nameSpace, depPath), "prototype");
                    WriteToInl(cppStruct.Name, CodeGenerator.DummyReflectorImpl(cppStruct, nameSpace, depPath), "reflector");
                }

                Logger.Log($"Generated files for {cppStruct.Name}", LogLevel.debug);
            }
            Logger.Log("Exit GenerateCode", LogLevel.trace);
        }

        static void SearchNamespaces(CppContainerList<CppNamespace> Namespaces, string parentNameSpace = "")
        {
            Logger.Log("Enter SearchNamespaces", LogLevel.trace);

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
            Logger.Log("Exit SearchNamespaces", LogLevel.trace);
        }
        static void WriteToHpp(string structName, string contents, string type)
        {
            Logger.Log("Enter WriteToHpp", LogLevel.trace);
            string writePath = $@"{autogenFolderPath}\autogen_{type}_{structName}.hpp";
            autogenHeaders += $"#include \"{writePath}\"\n".Replace($@"{CLArgs.moduleRoot}\{CLArgs.moduleName}\autogen\", "");
            filesCreated++;
            File.WriteAllText(writePath, contents);
            Logger.Log("Exit WriteToHpp", LogLevel.trace);
        }
        static void WriteToInl(string structName, string contents, string type)
        {
            Logger.Log("Enter WriteToInl", LogLevel.trace);
            string writePath = $@"{autogenFolderPath}\autogen_{type}_{structName}.inl";
            autogenImpl += $"#include \"autogen_{type}_{structName}.inl\"\n";
            filesCreated++;
            File.WriteAllText(writePath, contents);
            Logger.Log("Exit WriteToInl", LogLevel.trace);
        }
    }
}
