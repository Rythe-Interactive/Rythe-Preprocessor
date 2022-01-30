using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CppAst;
using CppAst.CodeGen;
using CppAst.CodeGen.Common;

namespace RytheTributary
{
    class Program
    {
        private static List<int> primitveIndecies = new List<int>();
        private static List<int> objectIndecies = new List<int>();
        private static CodeWriterOptions cw_options = new CodeWriterOptions();
        private static CodeWriter cw = new CodeWriter(cw_options);
        private static string searchDirectory = @"c:/users/blazi/documents/repos/legion-engine/applications";
        private static string relWritePath = @"sandbox/";
        private static string writePath = string.Format(@"{0}/{1}", searchDirectory, relWritePath);
        static string generate_reflector_hpp(CppClass cppClass)
        {
            cw.CurrentWriter.Dispose();
            cw = new CodeWriter(cw_options);
            cw.WriteLine("#include <core/types/reflector.hpp>");
            string headerPath = cppClass.SourceFile.ToLower().Replace(writePath.ToLower(), "");
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
        static string generate_prototype_hpp(CppClass cppClass)
        {
            cw.CurrentWriter.Dispose();
            cw = new CodeWriter(cw_options);
            cw.WriteLine("#include <core/types/prototype.hpp>");
            string headerPath = cppClass.SourceFile.ToLower().Replace(writePath.ToLower(), "");
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
            cw.WriteLine(string.Format("#include \"autogen_reflector_{0}.hpp\"", cppClass.Name, relWritePath));
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
            cw.WriteLine(string.Format("#include \"autogen_prototype_{0}.hpp\"", cppClass.Name, relWritePath));
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

        static void ProcessDir(string path, List<Regex> excludePatterns)
        {
            Console.WriteLine(path);

            string[] fileTypes = { "*.c", "*.h", "*.hpp", "*.cpp", "*.inl" };

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

                    }
                    //CleanFile(path, fileDir);
                }
        }
        static void Main(string[] args)
        {
            CppParserOptions options = new CppParserOptions();
            options = options.ConfigureForWindowsMsvc(CppTargetCpu.X86_64, CppVisualStudioVersion.VS2019);
            options.AdditionalArguments.Add("-std=c++17");
            options.AdditionalArguments.Add("-Werror=return-type");
            options.IncludeFolders.Add(@"C:\Users\blazi\Documents\Repos\Legion-Engine\include");
            options.IncludeFolders.Add(@"C:\Users\blazi\Documents\Repos\Legion-Engine\legion\engine");
            options.IncludeFolders.Add(@"C:\Users\blazi\Documents\Repos\Legion-Engine\legion\editor");
            options.IncludeFolders.Add(@"C:\Users\blazi\Documents\Repos\Legion-Engine\deps\include");
            options.ParseAttributes = true;
            var compilation = CppParser.ParseFile(@"C:/Users/blazi/Documents/Repos/Legion-Engine/applications/sandbox/systems/examplesystem.hpp", options);
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

                    Console.WriteLine(cppClass.Name);
                    if (attr.Name.Equals("reflectable"))
                    {
                        Console.WriteLine("PrototypeHPP");
                        string hppData = generate_prototype_hpp(cppClass);
                        Console.WriteLine(hppData);
                        string path = string.Format(@"{0}\autogen\autogen_prototype_{1}.hpp", writePath, cppClass.Name);
                        File.WriteAllText(path, hppData);

                        Console.WriteLine("PrototypeCPP");
                        string cppData = generate_prototype_cpp(cppClass);
                        Console.WriteLine(cppData);
                        path = string.Format(@"{0}\autogen\autogen_prototype_{1}.cpp", writePath, cppClass.Name);
                        File.WriteAllText(path, cppData);

                        Console.WriteLine("");
                        Console.WriteLine("");
                        Console.WriteLine("ReflectorHPP");
                        hppData = generate_reflector_hpp(cppClass);
                        Console.WriteLine(hppData);
                        path = string.Format(@"{0}\autogen\autogen_reflector_{1}.hpp", writePath, cppClass.Name);
                        File.WriteAllText(path, hppData);

                        Console.WriteLine("ReflectorCPP");
                        cppData = generate_reflector_cpp(cppClass);
                        Console.WriteLine(cppData);
                        path = string.Format(@"{0}\autogen\autogen_reflector_{1}.cpp", writePath, cppClass.Name);
                        File.WriteAllText(path, cppData);
                    }
                }
            }
        }
    }
}
