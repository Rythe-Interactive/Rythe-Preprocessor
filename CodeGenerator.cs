using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CppAst;
using CppAst.CodeGen.Common;

namespace RytheTributary
{
    class CodeGenerator
    {
        private static List<int> primitveIndecies = new List<int>();
        private static List<int> objectIndecies = new List<int>();

        private static CodeWriterOptions cw_options = new CodeWriterOptions();
        private static CodeWriter cw = new CodeWriter(cw_options);
        public static string headerPath;

        public static string ReflectorHPP(string className)
        {
            cw.CurrentWriter.Dispose();
            cw = new CodeWriter(cw_options);
            cw.WriteLine("#pragma once");
            cw.WriteLine("#include <core/types/reflector.hpp>");
            cw.WriteLine("namespace legion::core");
            cw.OpenBraceBlock();
            cw.WriteLine($"struct {className};");
            cw.WriteLine("template<>");
            cw.WriteLine($"L_NODISCARD reflector make_reflector<{className}>({className}& obj);");
            cw.WriteLine("template<>");
            cw.WriteLine($"L_NODISCARD const reflector make_reflector<const {className}>(const {className}& obj);");
            cw.CloseBraceBlock();
            var output = cw.ToString();
            return output;
        }
        public static string PrototypeHPP(string className)
        {
            cw.CurrentWriter.Dispose();
            cw = new CodeWriter(cw_options);
            cw.WriteLine("#pragma once");
            cw.WriteLine("#include <core/types/prototype.hpp>");
            cw.WriteLine("namespace legion::core");
            cw.OpenBraceBlock();
            cw.WriteLine($"struct {className};");
            cw.WriteLine("template<>");
            cw.WriteLine($"L_NODISCARD prototype make_prototype<{className}>(const {className}& obj);");
            cw.CloseBraceBlock();
            var output = cw.ToString();
            return output;
        }

        public static string ReflectorCPP(string className, string depPath, CppContainerList<CppField> fields)
        {
            cw.CurrentWriter.Dispose();
            cw = new CodeWriter(cw_options);
            cw.WriteLine($"#include \"autogen_reflector_{className}.hpp\"");
            cw.WriteLine($"#include \"../../{depPath}\"");
            cw.WriteLine("namespace legion::core");
            cw.OpenBraceBlock();
            reflectorFunction(className, fields);
            reflectorFunction(className, fields, true);
            cw.CloseBraceBlock();
            var output = cw.ToString();
            return output;
        }
        public static string PrototypeCPP(string className, string depPath, CppContainerList<CppField> fields)
        {
            cw.CurrentWriter.Dispose();
            cw = new CodeWriter(cw_options);
            cw.WriteLine($"#include \"autogen_prototype_{className}.hpp\"");
            cw.WriteLine($"#include \"../../{depPath}\"");
            cw.WriteLine("namespace legion::core");
            cw.OpenBraceBlock();
            prototypeFunction(className, fields);
            cw.CloseBraceBlock();
            var output = cw.ToString();
            return output;
        }

        static void reflectorFunction(string className, CppContainerList<CppField> fields, bool isConst = false)
        {
            primitveIndecies.Clear();
            objectIndecies.Clear();
            cw.WriteLine("template<>");
            if (isConst)
                cw.WriteLine($"L_NODISCARD const reflector make_reflector<const {className}>(const {className}& obj)");
            else
                cw.WriteLine($"L_NODISCARD reflector make_reflector<{className}>({className}& obj)");
            cw.OpenBraceBlock();
            if (isConst)
                cw.WriteLine("ptr_type address = reinterpret_cast<ptr_type>(std::addressof(obj));");
            cw.WriteLine("reflector refl;");
            cw.WriteLine($"refl.typeId = typeHash<{className}>();");
            cw.WriteLine($"refl.typeName = \"{className}\";");
            cw.Write("refl.members = std::vector<member_reference>");
            if (fields.Count > 0)
            {
                for (int i = 0; i < fields.Count; i++)
                {
                    if (fields[i].Visibility == CppVisibility.Private || fields[i].Visibility == CppVisibility.Protected)
                        continue;

                    switch (fields[i].Type.TypeKind)
                    {
                        case CppTypeKind.Primitive:
                            primitveIndecies.Add(i);
                            break;
                        case CppTypeKind.StructOrClass:
                            objectIndecies.Add(i);
                            break;
                    }
                }

                if (primitveIndecies.Count > 0)
                {
                    cw.OpenBraceBlock();
                    for (int i = 0; i < primitveIndecies.Count; i++)
                    {
                        var primitive = fields[primitveIndecies[i]];
                        cw.WriteLine("member_reference");
                        cw.OpenBraceBlock();
                        cw.WriteLine($"\"{primitive.Name}\",");
                        cw.WriteLine($"primitive_reference {{typeHash<{ primitive.Type.ToString()}>(), &obj.{primitive.Name}}}");
                        cw.CloseBraceBlock();
                        if (i != primitveIndecies.Count - 1)
                            cw.Write(",\n");
                    }
                    cw.CloseBraceBlock();
                    cw.Write(";");
                }
                else
                {
                    cw.Write("();\n");
                }
            }
            else
            {
                cw.Write("();\n");
            }

            foreach (int idx in objectIndecies)
            {
                cw.OpenBraceBlock();
                cw.WriteLine($"auto nested_refl = make_reflector(obj.{fields[idx].Name});");
                cw.WriteLine("members.emplace_back(nested_refl);");
                cw.CloseBraceBlock();
            }
            if (isConst)
                cw.WriteLine("refl.data = reinterpret_cast<void*>(address);");
            else
                cw.WriteLine("refl.data = std::addressof(obj);");
            cw.WriteLine("return refl;");
            cw.CloseBraceBlock();
        }
        static void prototypeFunction(string className, CppContainerList<CppField> fields)
        {
            primitveIndecies.Clear();
            objectIndecies.Clear();
            cw.WriteLine("template<>");
            cw.WriteLine($"L_NODISCARD prototype make_prototype<{className}>(const {className}& obj)");
            cw.OpenBraceBlock();
            cw.WriteLine("prototype prot;");
            cw.WriteLine($"prot.typeId = typeHash<{className}>();");
            cw.WriteLine($"prot.typeName = \"{className}\";");
            cw.Write("prot.members = std::vector<member_value>");
            if (fields.Count > 0)
            {
                for (int i = 0; i < fields.Count; i++)
                {
                    if (fields[i].Visibility == CppVisibility.Private || fields[i].Visibility == CppVisibility.Protected)
                        continue;

                    switch (fields[i].Type.TypeKind)
                    {
                        case CppTypeKind.Primitive:
                            primitveIndecies.Add(i);
                            break;
                        case CppTypeKind.StructOrClass:
                            objectIndecies.Add(i);
                            break;
                    }
                }
                if (primitveIndecies.Count > 0)
                {
                    cw.OpenBraceBlock();
                    for (int i = 0; i < primitveIndecies.Count; i++)
                    {
                        var primitive = fields[primitveIndecies[i]];
                        cw.WriteLine("member_value");
                        cw.OpenBraceBlock();
                        cw.WriteLine($"\"{primitive.Name}\",");
                        cw.WriteLine($"primitive_value {{typeHash<{primitive.Type.ToString()}>(),std::make_any<{primitive.Type.ToString()}>(obj.{primitive.Name})}}");
                        cw.CloseBraceBlock();
                        if (i != primitveIndecies.Count - 1)
                            cw.Write(",\n");
                    }
                    cw.CloseBraceBlock();
                    cw.Write(";");
                }
                else
                {
                    cw.Write("();\n");
                }
            }
            else
            {
                cw.Write("();\n");
            }
            foreach (int idx in objectIndecies)
            {
                cw.OpenBraceBlock();
                cw.WriteLine($"auto nested_prot = make_prototype(obj.{fields[idx].Name});");
                cw.WriteLine("members.emplace_back(nested_prot);");
                cw.CloseBraceBlock();
            }
            cw.WriteLine("return prot;");
            cw.CloseBraceBlock();
        }
    }
}
