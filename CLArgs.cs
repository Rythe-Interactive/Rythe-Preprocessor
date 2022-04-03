using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CppAst;
using System.Diagnostics;

namespace RytheTributary
{
    internal enum LogLevel
    {
        trace,
        debug,
        info,
        warn,
        error,
        fatal,
        silent,
        max = fatal,
        min = trace
    }

    internal class CLArgs
    {
        public static CppParserOptions options = new CppParserOptions();
        public static string moduleRoot = Directory.GetCurrentDirectory() + "\\";
        public static string moduleName;
        public static List<Regex> excludePatterns = new List<Regex>();
        public static LogLevel logLevel;

        public static bool Parse(string[] args)
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
                return false;
            }

            logLevel = LogLevel.error;

            Regex excludeRegex = new Regex("-ex=(.*)");
            Regex verbosityRegex = new Regex("-v=(.*)");
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
                Match match = excludeRegex.Match(command);
                if (match.Success)
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
                }
                else if (includePath.Match(command).Success)
                {
                    var cmd = command.Replace("-include=", "");
                    if (!cmd.EndsWith('\\'))
                        cmd += "\\";
                    options.SystemIncludeFolders.Add(Directory.GetParent(cmd).ToString());
                }
                else if (moduleRegex.Match(command).Success)
                {
                    var cmd = command.Replace("-module=", "");
                    moduleName = cmd;
                }
                else if (command.Contains("-s"))
                {
                    logLevel = LogLevel.silent;
                }
                else if (verbosityRegex.Match(command).Success)
                {
                    var cmd = command.Replace("-v=", "");
                    try
                    {
                        logLevel = (LogLevel)Math.Clamp(int.Parse(cmd), (int)LogLevel.min, (int)LogLevel.max);
                    }
                    catch
                    {
                        Console.Error.WriteLine("Invalid verbosity level");
                    }
                }
            }

            foreach (var dir in options.SystemIncludeFolders)
                Logger.Log($"Include Directory: {dir}", LogLevel.info);

            Logger.Log($"Moduleroot: {moduleRoot}", LogLevel.info);

            if (moduleName == null)
            {
                var split = moduleRoot.Split('\\');
                moduleName = split[split.Length - 1];
            }

            return true;
        }
    }
}
