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
        private static CppParserOptions options = new CppParserOptions();
        private static CodeWriterOptions cw_options = new CodeWriterOptions();
        private static CodeWriter cw = new CodeWriter(cw_options);
        private static string projectDirectory;

        static string generate_reflector_hpp(string parent, CppClass cppClass)
        {
            cw.CurrentWriter.Dispose();
            cw = new CodeWriter(cw_options);
            cw.WriteLine("#include <core/types/reflector.hpp>");
            string headerPath = cppClass.SourceFile.ToLower().Replace(parent.ToLower(), "");
            cw.WriteLine(string.Format("#include \"../{0}\"", headerPath));
            cw.WriteLine(string.Format("struct {0};", cppClass.Name));
            cw.WriteLine("namespace legion::core");
            cw.OpenBraceBlock();
            cw.WriteLine(string.Format("L_NODISCARD reflector make_reflector({0}& obj);", cppClass.Name));
            cw.WriteLine(string.Format("L_NODISCARD reflector make_reflector(const {0}& obj);", cppClass.Name));
            cw.CloseBraceBlock();
            var output = cw.ToString();
            return output;
        }
        static string generate_prototype_hpp(string parent, CppClass cppClass)
        {
            cw.CurrentWriter.Dispose();
            cw = new CodeWriter(cw_options);
            cw.WriteLine("#include <core/types/prototype.hpp>");
            string headerPath = cppClass.SourceFile.ToLower().Replace(parent.ToLower(), "");
            cw.WriteLine(string.Format("#include \"../{0}\"", headerPath));
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
            cw.WriteLine(string.Format("#include \"autogen_prototype_{0}.hpp\"", cppClass.Name));
            cw.WriteLine("namespace legion::core");
            cw.OpenBraceBlock();
            generate_prototype_code(cppClass);
            generate_prototype_code(cppClass, true);
            cw.CloseBraceBlock();
            var output = cw.ToString();
            return output;
        }

        static void generate_reflector_code(CppClass cppClass, bool isConst = false)
        {
            primitveIndecies.Clear();
            objectIndecies.Clear();
            if (isConst)
                cw.WriteLine(string.Format("L_NODISCARD reflector make_reflector(const {0}& obj)", cppClass.Name));
            else
                cw.WriteLine(string.Format("L_NODISCARD reflector make_reflector({0}& obj)", cppClass.Name));
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
        static void generate_prototype_code(CppClass cppClass, bool isConst = false)
        {
            primitveIndecies.Clear();
            objectIndecies.Clear();
            if (isConst)
                cw.WriteLine(string.Format("L_NODISCARD prototype make_prototype(const {0}& obj)", cppClass.Name));
            else
                cw.WriteLine(string.Format("L_NODISCARD prototype make_prototype({0}& obj)", cppClass.Name));
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
            projectDirectory = @"D:\Repos\Args-Engine\";
            options = options.ConfigureForWindowsMsvc(CppTargetCpu.X86_64, CppVisualStudioVersion.VS2019);
            options.AdditionalArguments.Add("-std=c++17");
            options.AdditionalArguments.Add("-Werror=return-type");
            options.IncludeFolders.Add(string.Format(@"{0}legion\engine", projectDirectory));
            options.IncludeFolders.Add(string.Format(@"{0}legion\engine\core", projectDirectory));
            //options.IncludeFolders.Add(string.Format(@"{0}deps/include", projectDirectory));
            options.ParseAttributes = true;
            if (args.Length == 0 || args[0] == "-help" || args[0] == "-h")
            {
                Console.WriteLine(
                    "\t---rythe_preprocessor---\n" +
                    "\tA tool for automatically generating code for additional functionality.\n\n" +
                    "\tpropper usage: any argument is assumed to be a path to check except those starting with \"-ex=\",\n" +
                    "\t    arguments starting with \"-ex=\" are taken as exclusion patterns.\n" +
                    "\teg:\n" +
                    "\t    lgn_cleanup \"../legion\" -ex=\"**/glm/\" -ex=\"**/folder/*/file.hpp\""
                    );
                return;
            }

            Regex excludeRegex = new Regex("-ex=(.*)");
            Regex starReplace = new Regex("([^\\.*]*)\\*([^\\*]*)");

            List<string> searchPaths = new List<string>();
            List<Regex> excludePatterns = new List<Regex>();

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
                else
                {
                    searchPaths.Add(command);
                }
            }

            foreach (string searchPath in searchPaths)
            {
                ProcessDir(searchPath, excludePatterns);
            }

            Console.WriteLine($"Checked {filesChecked} files and added {filesCreated} files in total.");
        }

        static void ProcessDir(string path, List<Regex> excludePatterns)
        {
            string[] fileTypes = { "*.c", "*.h", "*.hpp", "*.cpp" };
            paths.Clear();

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

            var compilation = CppParser.ParseFiles(paths, options);
            ProcessFile(compilation, path);
            Console.WriteLine("Done with {0}", path);
            Console.WriteLine("");
        }

        static void ProcessFile(CppCompilation compilation, string parent)
        {
            Directory.CreateDirectory(string.Format(@"{0}\autogen", parent));

            foreach (var diagnostic in compilation.Diagnostics.Messages)
            {
                if (diagnostic.Type != CppLogMessageType.Warning)
                    Console.WriteLine(diagnostic);
            }

            foreach (CppClass cppClass in compilation.Classes)
            {
                foreach (CppAttribute attr in cppClass.Attributes)
                {
                    if (!attr.Scope.Equals("legion") && !attr.Scope.Equals("rythe"))
                        continue;

                    if (attr.Name.Equals("reflectable"))
                    {
                        Console.WriteLine(cppClass.Name);
                        string hppData = generate_prototype_hpp(parent, cppClass);
                        string path = string.Format(@"{0}\autogen\autogen_prototype_{1}.hpp", parent, cppClass.Name);
                        filesCreated++;
                        File.WriteAllText(path, hppData);

                        string cppData = generate_prototype_cpp(cppClass);
                        path = string.Format(@"{0}\autogen\autogen_prototype_{1}.cpp", parent, cppClass.Name);
                        filesCreated++;
                        File.WriteAllText(path, cppData);

                        hppData = generate_reflector_hpp(parent, cppClass);
                        path = string.Format(@"{0}\autogen\autogen_reflector_{1}.hpp", parent, cppClass.Name);
                        filesCreated++;
                        File.WriteAllText(path, hppData);

                        cppData = generate_reflector_cpp(cppClass);
                        path = string.Format(@"{0}\autogen\autogen_reflector_{1}.cpp", parent, cppClass.Name);
                        filesCreated++;
                        File.WriteAllText(path, cppData);
                    }
                }
            }
        }
    }
}
