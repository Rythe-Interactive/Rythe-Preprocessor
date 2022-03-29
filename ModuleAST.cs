using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CppAst;
using System.Diagnostics;
using System.Globalization;

namespace RytheTributary
{
    internal class ModuleAST
    {
        public static uint filesChecked = 0;
        private static CppCompilation result = null;

        public static List<CppMacro> Macros => result.Macros;
        public static CppContainerList<CppField> Fields => result.Fields;
        public static CppContainerList<CppFunction> Functions => result.Functions;
        public static CppContainerList<CppEnum> Enums => result.Enums;
        public static CppContainerList<CppClass> Classes => result.Classes;
        public static CppContainerList<CppTypedef> Typedefs => result.Typedefs;
        public static CppContainerList<CppNamespace> Namespaces => result.Namespaces;
        public static CppContainerList<CppAttribute> Attributes => result.Attributes;

        private static int CountClasses(CppNamespace targetNamespace)
        {
            int count = targetNamespace.Classes.Count;

            foreach (var ns in targetNamespace.Namespaces)
                count += CountClasses(ns);

            return count;
        }

        private static int CountClasses()
        {
            int count = Classes.Count;

            foreach (var ns in Namespaces)
                count += CountClasses(ns);

            return count;
        }

        public static bool GenerateAST()
        {
            string[] fileTypes = { "*.h", "*.hpp" };
            var fileDirs = new List<string>();

            foreach (string type in fileTypes)
                foreach (String fileDir in Directory.GetFiles(CLArgs.moduleRoot + "\\" + CLArgs.moduleName, type, SearchOption.AllDirectories))
                {
                    bool matched = false;
                    foreach (Regex regex in CLArgs.excludePatterns)
                        if (regex.IsMatch(fileDir))
                        {
                            matched = true;
                            break;
                        }

                    if (!matched)
                    {
                        if (!File.Exists(fileDir))
                        {
                            Logger.Log($"[ProcessDir] File Path {fileDir} does not exist", LogLevel.error);
                            continue;
                        }

                        filesChecked++;
                        fileDirs.Add(fileDir);
                        Logger.Log($"Adding File Directory to parse list: {fileDir}", LogLevel.debug);
                    }
                }
            Logger.Log($"Finished searching {CLArgs.moduleRoot}\\{CLArgs.moduleName}", LogLevel.info);

            //Parse the whole engine and its modules
            Stopwatch watch = new Stopwatch();
            watch.Start();
            result = CppParser.ParseFiles(fileDirs, CLArgs.options);
            watch.Stop();

            foreach (var diagnostic in result.Diagnostics.Messages)
            {
                if (diagnostic.Type == CppLogMessageType.Warning)
                    Logger.LogHidden("W:" + diagnostic.ToString(), LogLevel.warn);
                if (diagnostic.Type == CppLogMessageType.Info)
                    Logger.LogHidden("I:" + diagnostic.ToString(), LogLevel.info);
                if (diagnostic.Type == CppLogMessageType.Error)
                {
                    Logger.PrintLn(diagnostic);
                    Logger.LogHidden("E:" + diagnostic.ToString(), LogLevel.error);
                }
            }

            if (!result.HasErrors)
            {
                Logger.Log($"AST Generation completed in {watch.Elapsed.TotalSeconds.ToString("F6", CultureInfo.InvariantCulture)} seconds", LogLevel.info);
                Logger.Log($"Classes Found: {CountClasses()}", LogLevel.info);
                return true;
            }
            else
            {
                Logger.Log("AST Generation had errors and did not complete", LogLevel.fatal);
                return false;
            }
        }
    }
}
