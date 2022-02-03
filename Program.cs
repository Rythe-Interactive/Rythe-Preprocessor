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
        private static List<int> primitveIndecies = new List<int>();
        private static List<int> objectIndecies = new List<int>();
        private static CppCompilation engineAST = null;
        private static CppParserOptions options = new CppParserOptions();
        private static CodeWriterOptions cw_options = new CodeWriterOptions();
        private static CodeWriter cw = new CodeWriter(cw_options);
        private static string projectDirectory;

        static string generate_reflector_hpp(CppClass cppClass, string writePath)
        {
            cw.CurrentWriter.Dispose();
            cw = new CodeWriter(cw_options);
            cw.WriteLine("#pragma once");
            cw.WriteLine("#include <core/types/reflector.hpp>");
            string headerPath = cppClass.SourceFile.ToLower().Replace($@"{projectDirectory}\{writePath}".ToLower(), "");
            cw.WriteLine($"#include <{headerPath}>");
            cw.WriteLine($"struct {cppClass.Name};");
            cw.WriteLine("namespace legion::core");
            cw.OpenBraceBlock();
            cw.WriteLine($"L_NODISCARD auto make_reflector({cppClass.Name}& obj);");
            cw.WriteLine($"L_NODISCARD const auto make_reflector(const {cppClass.Name}& obj);");
            cw.CloseBraceBlock();
            var output = cw.ToString();
            return output;
        }
        static string generate_prototype_hpp(CppClass cppClassstring, string writePath)
        {
            cw.CurrentWriter.Dispose();
            cw = new CodeWriter(cw_options);
            cw.WriteLine("#pragma once");
            cw.WriteLine("#include <core/types/prototype.hpp>");
            string headerPath = cppClass.SourceFile.ToLower().Replace(parent.ToLower(), "");
            cw.WriteLine(string.Format("#include <{0}>", headerPath));
            cw.WriteLine(string.Format("struct {0};", cppClass.Name));
            cw.WriteLine("namespace legion::core");
            cw.OpenBraceBlock();
            cw.WriteLine(string.Format("L_NODISCARD prototype make_prototype({0}& obj);", cppClass.Name));
            cw.WriteLine(string.Format("L_NODISCARD prototype make_prototype(const {0}& obj);", cppClass.Name));
            cw.CloseBraceBlock();
            var output = cw.ToString();
            return output;
        }

        static string generate_reflector_cpp(CppClass cppClass)
        {
            cw.CurrentWriter.Dispose();
            cw = new CodeWriter(cw_options);
            cw.WriteLine("#pragma once");
            cw.WriteLine(string.Format("#include \"autogen_reflector_{0}.hpp\"", cppClass.Name));
            cw.WriteLine("namespace legion::core");
            cw.OpenBraceBlock();
            generate_reflector_code(cppClass);
            generate_reflector_code(cppClass, true);
            cw.CloseBraceBlock();
            var output = cw.ToString();
            return output;
        }
        static string generate_prototype_cpp(CppClass cppClass)
        {
            cw.CurrentWriter.Dispose();
            cw = new CodeWriter(cw_options);
            cw.WriteLine("#pragma once");
            cw.WriteLine(string.Format("#include \"autogen_prototype_{0}.hpp\"", cppClass.Name));
            cw.WriteLine("namespace legion::core");
            cw.OpenBraceBlock();
            generate_prototype_code(cppClass);
            cw.CloseBraceBlock();
            var output = cw.ToString();
            return output;
        }

        static void generate_reflector_code(CppClass cppClass, bool isConst = false)
        {
            primitveIndecies.Clear();
            objectIndecies.Clear();
            if (isConst)
                cw.WriteLine(string.Format("L_NODISCARD const auto make_reflector(const {0}& obj)", cppClass.Name));
            else
                cw.WriteLine(string.Format("L_NODISCARD auto make_reflector({0}& obj)", cppClass.Name));
            cw.OpenBraceBlock();
            if (isConst)
                cw.WriteLine("ptr_type address = reinterpret_cast<ptr_type>(std::addressof(obj));");
            cw.WriteLine("reflector refl;");
            cw.WriteLine(string.Format("refl.typeId = typeHash<{0}>();", cppClass.Name));
            cw.WriteLine(string.Format("refl.typeName = \"{0}\";", cppClass.Name));
            cw.WriteLine(string.Format("refl.members = std::vector<member_reference>"));
            cw.OpenBraceBlock();

            for (int i = 0; i < cppClass.Fields.Count; i++)
            {
                switch (cppClass.Fields[i].Type.TypeKind)
                {
                    case CppTypeKind.Primitive:
                        primitveIndecies.Add(i);
                        break;
                    case CppTypeKind.StructOrClass:
                        objectIndecies.Add(i);
                        break;
                }
            }
            for (int i = 0; i < primitveIndecies.Count; i++)
            {
                var primitive = cppClass.Fields[primitveIndecies[i]];
                cw.WriteLine("member_reference");
                cw.OpenBraceBlock();
                cw.WriteLine(string.Format("\"{0}\",", primitive.Name));
                cw.WriteLine(string.Format("primitive_reference {{typeHash<{0}>(), &obj.{1}}}", primitive.Type.ToString(), primitive.Name));
                cw.CloseBraceBlock();
                if (i != primitveIndecies.Count - 1)
                    cw.Write(",\n");
            }
            cw.CloseBraceBlock();
            cw.Write(";");
            foreach (int idx in objectIndecies)
            {
                cw.OpenBraceBlock();
                cw.WriteLine(string.Format("auto nested_refl = make_reflector(obj.{0});", cppClass.Fields[idx].Name));
                cw.WriteLine(string.Format("members.emplace_back(nested_refl);"));
                cw.CloseBraceBlock();
            }
            if (isConst)
                cw.WriteLine("refl.data = reinterpret_cast<void*>(address);");
            else
                cw.WriteLine("refl.data = std::addressof(obj);");
            cw.WriteLine("return refl;");
            cw.CloseBraceBlock();
        }
        static void generate_prototype_code(CppClass cppClass)
        {
            primitveIndecies.Clear();
            objectIndecies.Clear();
            cw.WriteLine(string.Format("L_NODISCARD prototype make_prototype(const {0}& obj)", cppClass.Name));
            cw.OpenBraceBlock();
            cw.WriteLine("prototype prot;");
            cw.WriteLine(string.Format("prot.typeId = typeHash<{0}>();", cppClass.Name));
            cw.WriteLine(string.Format("prot.typeName = \"{0}\";", cppClass.Name));
            cw.WriteLine(string.Format("prot.members = std::vector<member_value>"));
            cw.OpenBraceBlock();
            for (int i = 0; i < cppClass.Fields.Count; i++)
            {
                switch (cppClass.Fields[i].Type.TypeKind)
                {
                    case CppTypeKind.Primitive:
                        primitveIndecies.Add(i);
                        break;
                    case CppTypeKind.StructOrClass:
                        objectIndecies.Add(i);
                        break;
                }
            }
            for (int i = 0; i < primitveIndecies.Count; i++)
            {
                var primitive = cppClass.Fields[primitveIndecies[i]];
                cw.WriteLine("member_value");
                cw.OpenBraceBlock();
                cw.WriteLine(string.Format("\"{0}\",", primitive.Name));
                cw.WriteLine(string.Format("primitive_value {{typeHash<{0}>(),std::make_any<{0}>(obj.{1})}}", primitive.Type.ToString(), primitive.Name));
                cw.CloseBraceBlock();
                if (i != primitveIndecies.Count - 1)
                    cw.Write(",\n");
            }
            cw.CloseBraceBlock();
            cw.Write(";");
            foreach (int idx in objectIndecies)
            {
                cw.OpenBraceBlock();
                cw.WriteLine(string.Format("auto nested_prot = make_prototype(obj.{0});", cppClass.Fields[idx].Name));
                cw.WriteLine(string.Format("members.emplace_back(nested_prot);"));
                cw.CloseBraceBlock();
            }
            cw.WriteLine("return prot;");
            cw.CloseBraceBlock();
        }

        static void Main(string[] args)
        {
            options = options.ConfigureForWindowsMsvc(CppTargetCpu.X86_64, CppVisualStudioVersion.VS2019);
            options.AdditionalArguments.Add("-std=c++17");
            options.AdditionalArguments.Add("-Werror=return-type");
            options.IncludeFolders.Add(string.Format(@"{0}deps\include", projectDirectory));
            options.IncludeFolders.Add(string.Format(@"{0}legion\engine", projectDirectory));
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
            {
                projectDirectory = args[0].Replace("-root=", "");
            }

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
                    applicationPaths.Add(projectDirectory + @"applications\" + cmd + @"\");
                }
                else if (moduleRegex.Match(command).Success)
                {
                    var cmd = command.Replace("-module=", "");
                    modulePaths.Add(projectDirectory + @"legion\engine\" + cmd + @"\");
                }
            }

            //Directory.CreateDirectory(string.Format(@"{0}\autogen", applicationPath));

            ProcessDir(modulePaths.ToArray(), excludePatterns);

            foreach (string path in applicationPaths)
            {
                ProcessApplication(path);
            }

            Console.WriteLine($"Checked {filesChecked} files and added {filesCreated} files in total.");
        }

        static void ProcessDir(string[] pathsToProcess, List<Regex> excludePatterns)
        {
            string[] fileTypes = { "*.h", "*.hpp" };
            paths.Clear();

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
                            paths.Add(fileDir);
                        }
                    }
                Console.WriteLine("Done with {0}", path);
                Console.WriteLine("");
            }

            //Parse the whole engine and its modules
            engineAST = CppParser.ParseFiles(paths, options);
            foreach (var diagnostic in engineAST.Diagnostics.Messages)
            {
                if (diagnostic.Type != CppLogMessageType.Warning)
                    Console.WriteLine(diagnostic);
            }
        }

        static void ProcessApplication(string applicationPath)
        {
            foreach (CppClass cppClass in engineAST.Classes)
            {
                foreach (CppAttribute attr in cppClass.Attributes)
                {
                    if (!attr.Scope.Equals("legion") && !attr.Scope.Equals("rythe"))
                        continue;

                    if (attr.Name.Equals("reflectable"))
                    {
                        Console.WriteLine(cppClass.Name);
                        string hppData = generate_prototype_hpp(cppClass, modulePath);
                        string path = $@"{applicationPath}\autogen\autogen_prototype_{cppClass.Name}.hpp";
                        filesCreated++;
                        File.WriteAllText(path, hppData);

                        hppData = generate_reflector_hpp(cppClass, modulePath);
                        path = $@"{applicationPath}\autogen\autogen_reflector_{cppClass.Name}.hpp";
                        filesCreated++;
                        File.WriteAllText(path, hppData);

                        string cppData = generate_prototype_cpp(cppClass);
                        path = $@"{applicationPath}\autogen\autogen_prototype_{cppClass.Name}.cpp";
                        filesCreated++;
                        File.WriteAllText(path, cppData);

                        cppData = generate_reflector_cpp(cppClass);
                        path = $@"{applicationPath}\autogen\autogen_reflector_{cppClass.Name}.cpp";
                        filesCreated++;
                        File.WriteAllText(path, cppData);
                    }
                }
            }
        }
    }
}
