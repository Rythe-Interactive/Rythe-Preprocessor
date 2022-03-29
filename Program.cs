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
        private static Dictionary<string, string> autogenHeaders = new Dictionary<string, string>();
        private static Dictionary<string, string> autogenInline = new Dictionary<string, string>();
        public static uint filesCreated = 0;

        static void Main(string[] args)
        {
            Logger.Init();
            if(!CLArgs.Parse(args))
                return;

            Logger.Log($"Module Name: {CLArgs.moduleName}", LogLevel.info);

            if (ModuleAST.GenerateAST())
            {
                Directory.CreateDirectory($@"{CLArgs.moduleRoot}\{CLArgs.moduleName}\autogen");
                SearchNamespaces(ModuleAST.Namespaces);
                GenerateCode(ModuleAST.Classes);

                if (autogenHeaders.ContainsKey(CLArgs.moduleName))
                    File.WriteAllText($@"{CLArgs.moduleRoot}\{CLArgs.moduleName}\autogen\autogen.hpp", $"{autogenHeaders[CLArgs.moduleName]}\n");

                if (autogenInline.ContainsKey(CLArgs.moduleName))
                    File.WriteAllText($@"{CLArgs.moduleRoot}\{CLArgs.moduleName}\autogen\autogen.cpp", $"{autogenInline[CLArgs.moduleName]}");
            }
            Logger.Log($"Scanned {ModuleAST.filesChecked} files and generated {filesCreated} files in total.", LogLevel.info);

            Logger.WriteToFile("PreProcessor");
        }

        static void GenerateCode(CppContainerList<CppClass> classes, string nameSpace = "")
        {
            foreach (var cppStruct in classes)
            {
                if (cppStruct.SourceFile == null)
                    continue;

                if (!cppStruct.SourceFile.Contains(CLArgs.moduleName))
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

                foreach (CppAttribute attr in cppStruct.Attributes)
                {
                    string scopeStr = attr.Scope != null ? attr.Scope + "::" : "";
                    Regex findStruct = new Regex($@"\b(?:struct|class)\b\s*\[\[{scopeStr}{attr.Name}\]\]\s*{cppStruct.Name}[\s|<|>|\w|:| |\n]*\{{");
                    if (!findStruct.IsMatch(source))
                        continue;

                    if (attr.Name.Equals("reflectable"))
                    {
                        if (!autogenHeaders.ContainsKey(CLArgs.moduleName))
                            autogenHeaders.Add(CLArgs.moduleName, "#pragma once\n#include <core/platform/platform.hpp>\n#if __has_include_next(\"autogen.hpp\")\n#include_next \"autogen.hpp\"\n#endif\n");
                        if (!autogenInline.ContainsKey(CLArgs.moduleName))
                            autogenInline.Add(CLArgs.moduleName, "#include \"autogen.hpp\"\n#pragma once\n");

                        WriteToHpp(cppStruct.Name, CodeGenerator.PrototypeHPP(cppStruct.Name, nameSpace), "prototype");
                        WriteToHpp(cppStruct.Name, CodeGenerator.ReflectorHPP(cppStruct.Name, nameSpace), "reflector");

                        WriteToInl(cppStruct.Name, CodeGenerator.PrototypeInl(cppStruct.Name, nameSpace, depPath, cppStruct.Fields, cppStruct.Attributes), "prototype");
                        WriteToInl(cppStruct.Name, CodeGenerator.ReflectorInl(cppStruct.Name, nameSpace, depPath, cppStruct.Fields, cppStruct.Attributes), "reflector");
                        Logger.Log($"Generated files for {cppStruct.Name}", LogLevel.debug);
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
            string writePath = $@"{CLArgs.moduleRoot}\{CLArgs.moduleName}\autogen\autogen_{type}_{structName}.hpp";
            autogenHeaders[CLArgs.moduleName] += $"#include \"{writePath}\"\n".Replace($@"{CLArgs.moduleRoot}\{CLArgs.moduleName}\autogen\", "");
            filesCreated++;
            File.WriteAllText(writePath, contents);
        }
        static void WriteToInl(string structName, string contents, string type)
        {
            string writePath = $@"{CLArgs.moduleRoot}\{CLArgs.moduleName}\autogen\autogen_{type}_{structName}.inl";
            autogenInline[CLArgs.moduleName] += $"#include \"autogen_{type}_{structName}.inl\"\n";
            filesCreated++;
            File.WriteAllText(writePath, contents);
        }
    }
}
