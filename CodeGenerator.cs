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

        public static string ReflectorHPP(string className, string nameSpace)
        {
            cw.CurrentWriter.Dispose();
            cw = new CodeWriter(cw_options);
            cw.WriteLine("#pragma once");
            cw.WriteLine("#include <core/types/reflector.hpp>");
            cw.WriteLine($"namespace {nameSpace}");
            cw.OpenBraceBlock();
            cw.WriteLine($"struct {className};");
            cw.CloseBraceBlock();
            cw.WriteLine("namespace legion::core");
            cw.OpenBraceBlock();
            cw.WriteLine("template<>");
            cw.WriteLine($"L_NODISCARD reflector make_reflector<{nameSpace}::{className}>({nameSpace}::{className}& obj);");
            cw.WriteLine("template<>");
            cw.WriteLine($"L_NODISCARD const reflector make_reflector<const {nameSpace}::{className}>(const {nameSpace}::{className}& obj);");
            cw.CloseBraceBlock();
            var output = cw.ToString();
            return output;
        }
        public static string PrototypeHPP(string className, string nameSpace)
        {
            cw.CurrentWriter.Dispose();
            cw = new CodeWriter(cw_options);
            cw.WriteLine("#pragma once");
            cw.WriteLine("#include <core/types/prototype.hpp>");
            cw.WriteLine($"namespace {nameSpace}");
            cw.OpenBraceBlock();
            cw.WriteLine($"struct {className};");
            cw.CloseBraceBlock();
            cw.WriteLine("namespace legion::core");
            cw.OpenBraceBlock();
            cw.WriteLine("template<>");
            cw.WriteLine($"L_NODISCARD prototype make_prototype<{nameSpace}::{className}>(const {nameSpace}::{className}& obj);");
            cw.CloseBraceBlock();
            var output = cw.ToString();
            return output;
        }

        public static string ReflectorInl(string className, string nameSpace, string depPath, CppContainerList<CppField> fields, CppContainerList<CppAttribute> attributes)
        {
            cw.CurrentWriter.Dispose();
            cw = new CodeWriter(cw_options);
            cw.WriteLine($"#include \"autogen_reflector_{className}.hpp\"");
            cw.WriteLine($"#include \"../../{depPath}\"");
            cw.WriteLine("namespace legion { using namespace core; }");
            cw.WriteLine("namespace legion::core");
            cw.OpenBraceBlock();
            reflectorFunction(className, nameSpace, fields, attributes);
            reflectorFunction(className, nameSpace, fields, attributes, true);
            cw.CloseBraceBlock();
            var output = cw.ToString();
            return output;
        }
        public static string PrototypeInl(string className, string nameSpace, string depPath, CppContainerList<CppField> fields, CppContainerList<CppAttribute> attributes)
        {
            cw.CurrentWriter.Dispose();
            cw = new CodeWriter(cw_options);
            cw.WriteLine($"#include \"autogen_prototype_{className}.hpp\"");
            cw.WriteLine($"#include \"../../{depPath}\"");
            cw.WriteLine("namespace legion { using namespace core; }");
            cw.WriteLine("namespace legion::core");
            cw.OpenBraceBlock();
            prototypeFunction(className, nameSpace, fields, attributes);
            cw.CloseBraceBlock();
            var output = cw.ToString();
            return output;
        }

        static void reflectorFunction(string className, string nameSpace, CppContainerList<CppField> fields, CppContainerList<CppAttribute> attributes, bool isConst = false)
        {
            primitveIndecies.Clear();
            objectIndecies.Clear();
            cw.WriteLine("template<>");
            if (isConst)
                cw.WriteLine($"L_NODISCARD const reflector make_reflector<const {nameSpace}::{className}>(const {nameSpace}::{className}& obj)");
            else
                cw.WriteLine($"L_NODISCARD reflector make_reflector<{nameSpace}::{className}>({nameSpace}::{className}& obj)");
            cw.OpenBraceBlock();
            if (isConst)
                cw.WriteLine("ptr_type address = reinterpret_cast<ptr_type>(std::addressof(obj));");
            cw.WriteLine("reflector refl;");
            cw.WriteLine($"refl.typeId = typeHash<{nameSpace}::{className}>();");
            cw.WriteLine($"refl.typeName = \"{nameSpace}::{className}\";");
            if (attributes != null)
            {
                foreach (CppAttribute attr in attributes)
                {
                    cw.OpenBraceBlock();
                    cw.WriteLine($"static const {attr.Name}_attribute {attr.Name}_attr{{{attr.Arguments}}};");
                    cw.WriteLine($"refl.attributes.push_back(std::cref({attr.Name}_attr));");
                    cw.CloseBraceBlock();
                }
            }
            cw.WriteLine("refl.members = std::vector<member_reference>");
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
                    cw.WriteLine("{");
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
                    cw.WriteLine("};");
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
                cw.WriteLine($"refl.members.emplace_back(\"{fields[idx].Name}\",nested_refl);");
                cw.CloseBraceBlock();
            }
            int count = 0;
            for (int i = 0; i < fields.Count; i++)
            {
                if (fields[i].Attributes == null)
                    continue;
                foreach (CppAttribute attr in fields[i].Attributes)
                {
                    cw.OpenBraceBlock();
                    cw.WriteLine($"static const {attr.Name}_attribute {attr.Name}_attr{{{attr.Arguments}}};");
                    cw.WriteLine($"refl.members[{count}].attributes.push_back(std::cref({attr.Name}_attr));");
                    cw.CloseBraceBlock();
                }
                count++;
            }
            if (isConst)
                cw.WriteLine("refl.data = reinterpret_cast<void*>(address);");
            else
                cw.WriteLine("refl.data = std::addressof(obj);");
            cw.WriteLine("return refl;");
            cw.CloseBraceBlock();
        }
        static void prototypeFunction(string className, string nameSpace, CppContainerList<CppField> fields, CppContainerList<CppAttribute> attributes)
        {
            primitveIndecies.Clear();
            objectIndecies.Clear();
            cw.WriteLine("template<>");
            cw.WriteLine($"L_NODISCARD prototype make_prototype<{nameSpace}::{className}>(const {nameSpace}::{className}& obj)");
            cw.OpenBraceBlock();
            cw.WriteLine("prototype prot;");
            cw.WriteLine($"prot.typeId = typeHash<{nameSpace}::{className}>();");
            cw.WriteLine($"prot.typeName = \"{nameSpace}::{className}\";");
            if (attributes != null)
            {
                foreach (CppAttribute attr in attributes)
                {
                    cw.OpenBraceBlock();
                    cw.WriteLine($"static const {attr.Scope}::{attr.Name}_attribute {attr.Name}_attr{{{attr.Arguments}}};");
                    cw.WriteLine($"prot.attributes.push_back(std::cref({attr.Name}_attr));");
                    cw.CloseBraceBlock();
                }
            }
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
                    cw.WriteLine("{");
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
                    cw.WriteLine("};");
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
                cw.WriteLine($"prot.members.emplace_back(\"{fields[idx].Name}\",nested_prot);");
                cw.CloseBraceBlock();
            }
            int count = 0;
            for (int i = 0; i < fields.Count; i++)
            {
                if (fields[i].Attributes == null)
                    continue;
                foreach (CppAttribute attr in fields[i].Attributes)
                {
                    cw.OpenBraceBlock();
                    cw.WriteLine($"static const {attr.Scope}::{attr.Name}_attribute {attr.Name}_attr{{{attr.Arguments}}};");
                    cw.WriteLine($"prot.members[{count}].attributes.push_back(std::cref({attr.Name}_attr));");
                    cw.CloseBraceBlock();
                }
                count++;
            }
            cw.WriteLine("return prot;");
            cw.CloseBraceBlock();
        }
    }
}
