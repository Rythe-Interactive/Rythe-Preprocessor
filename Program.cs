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
        private static CppCompilation engineAST = null;
        private static CppParserOptions options = new CppParserOptions();

        private static string projectDirectory;

        static void Main(string[] args)
        {
            options = options.ConfigureForWindowsMsvc(CppTargetCpu.X86_64, CppVisualStudioVersion.VS2019);
            options.AdditionalArguments.Add("-std=c++17");
            options.AdditionalArguments.Add("-Wall");
            options.ParseMacros = true;
            options.ParseAttributes = true;
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

            List<string> applicationPaths = new List<string>();
            List<string> modulePaths = new List<string>();
            List<Regex> excludePatterns = new List<Regex>();
            if (args[0].StartsWith("-root="))
                projectDirectory = args[0].Replace("-root=", "");

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
                    applicationPaths.Add($@"{projectDirectory}applications\{cmd}\");
                }
                else if (moduleRegex.Match(command).Success)
                {
                    var cmd = command.Replace("-module=", "");
                    modulePaths.Add($@"{projectDirectory}legion\engine\{cmd}\");
                }
            }

            paths.Clear();
            paths.AddRange(modulePaths);
            paths.AddRange(applicationPaths);

            //Process the whole engine into an ast.
            ProcessDir(paths.ToArray(), excludePatterns);

            foreach (string path in applicationPaths)
            {
                Directory.CreateDirectory($@"{path}\autogen");
                ProcessApplication(path);
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

            //for (int i = 0; i < fileDirs.Count; i++)
            //{
            //    var idx = fileDirs[i].LastIndexOf(@"\");
            //    options.IncludeFolders.Add(fileDirs[i].Substring(0, idx));
            //}

            //int idx = fileDirs.IndexOf(fileDirs.Find(dir => dir.Contains("core.hpp")));

            //string temp = fileDirs[idx];
            //fileDirs.RemoveAt(idx);
            //fileDirs.Add(temp);

            //Parse the whole engine and its modules
            engineAST = CppParser.ParseFiles(fileDirs, options);
            foreach (var diagnostic in engineAST.Diagnostics.Messages)
            {
                if (diagnostic.Type == CppLogMessageType.Error)
                    Console.WriteLine(diagnostic);
            }
        }
        static void ProcessApplication(string applicationPath)
        {
            foreach (CppClass cppClass in engineAST.Classes)
            {
                string depPath = "";
                foreach (CppAttribute attr in cppClass.Attributes)
                {
                    if (!attr.Scope.Equals("legion") && !attr.Scope.Equals("rythe"))
                        continue;

                    if (attr.Name.Equals("reflectable"))
                    {
                        Console.WriteLine(cppClass.Name);
                        depPath = cppClass.SourceFile.ToLower().Contains($@"{projectDirectory.ToLower()}legion\engine\") ? cppClass.SourceFile.ToLower().Replace($@"{projectDirectory.ToLower()}legion\engine\", "") : cppClass.SourceFile.ToLower().Replace($@"{projectDirectory.ToLower()}applications\", "");
                        string hppData = CodeGenerator.PrototypeHPP(cppClass.Name, depPath);
                        Console.WriteLine(hppData);
                        string path = $@"{applicationPath}\autogen\autogen_prototype_{cppClass.Name}.hpp";
                        filesCreated++;
                        File.WriteAllText(path, hppData);

                        //depPath = cppClass.SourceFile.ToLower().Contains($@"{projectDirectory.ToLower()}legion\engine\") ? cppClass.SourceFile.ToLower().Replace($@"{projectDirectory}legion\engine\", "") : cppClass.SourceFile.ToLower().Replace($@"{projectDirectory}applications\", "");
                        hppData = CodeGenerator.ReflectorHPP(cppClass.Name, depPath);
                        path = $@"{applicationPath}\autogen\autogen_reflector_{cppClass.Name}.hpp";
                        filesCreated++;
                        File.WriteAllText(path, hppData);

                        string cppData = CodeGenerator.PrototypeCPP(cppClass.Name, cppClass.Fields);
                        path = $@"{applicationPath}\autogen\autogen_prototype_{cppClass.Name}.cpp";
                        filesCreated++;
                        File.WriteAllText(path, cppData);

                        cppData = CodeGenerator.ReflectorCPP(cppClass.Name, cppClass.Fields);
                        path = $@"{applicationPath}\autogen\autogen_reflector_{cppClass.Name}.cpp";
                        filesCreated++;
                        File.WriteAllText(path, cppData);
                    }
                }
            }
        }
    }
}
