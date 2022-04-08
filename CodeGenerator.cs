using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using CppAst;
using CppAst.CodeGen.Common;

namespace RytheTributary
{
    class AttributeNameComparer : IEqualityComparer<CppAttribute>
    {
        public bool Equals(CppAttribute x, CppAttribute y)
        {
            return x.Name == y.Name;
        }

        public int GetHashCode([DisallowNull] CppAttribute obj)
        {
            return obj.Name.GetHashCode();
        }
    }

    class CodeGenerator
    {
        private static List<int> primitveIndecies = new List<int>();
        private static List<int> objectIndecies = new List<int>();

        private static CodeWriterOptions cw_options = new CodeWriterOptions();
        private static CodeWriter cw = new CodeWriter(cw_options);

        public static string ReflectorHeader(string className, string nameSpace)
        {
            Logger.Log("Enter ReflectorHPP", LogLevel.trace);
            cw.CurrentWriter.Dispose();
            cw = new CodeWriter(cw_options);
            cw.WriteLine("#pragma once");
            cw.WriteLine("#include <core/types/reflector.hpp>");
            if (nameSpace.Length > 0)
            {
                cw.WriteLine($"namespace {nameSpace}");
                cw.OpenBraceBlock();
            }
            cw.WriteLine($"struct {className};");
            if (nameSpace.Length > 0)
            {
                cw.CloseBraceBlock();
            }
            cw.WriteLine("namespace legion::core");
            cw.OpenBraceBlock();
            cw.WriteLine("template<>");
            cw.WriteLine($"L_NODISCARD extern reflector make_reflector<{nameSpace}::{className}>({nameSpace}::{className}& obj);");
            cw.WriteLine("template<>");
            cw.WriteLine($"L_NODISCARD extern const reflector make_reflector<const {nameSpace}::{className}>(const {nameSpace}::{className}& obj);");
            cw.CloseBraceBlock();
            var output = cw.ToString();
            Logger.Log("Exit ReflectorHPP", LogLevel.trace);
            return output;
        }

        public static string PrototypeHeader(string className, string nameSpace)
        {
            Logger.Log("Enter PrototypeHPP", LogLevel.trace);
            cw.CurrentWriter.Dispose();
            cw = new CodeWriter(cw_options);
            cw.WriteLine("#pragma once");
            cw.WriteLine("#include <core/types/prototype.hpp>");
            if (nameSpace.Length > 0)
            {
                cw.WriteLine($"namespace {nameSpace}");
                cw.OpenBraceBlock();
            }
            cw.WriteLine($"struct {className};");
            if (nameSpace.Length > 0)
            {
                cw.CloseBraceBlock();
            }
            cw.WriteLine("namespace legion::core");
            cw.OpenBraceBlock();
            cw.WriteLine("template<>");
            cw.WriteLine($"L_NODISCARD extern prototype make_prototype<{nameSpace}::{className}>(const {nameSpace}::{className}& obj);");
            cw.CloseBraceBlock();
            var output = cw.ToString();
            Logger.Log("Exit PrototypeHPP", LogLevel.trace);
            return output;
        }

        public static string ReflectorImpl(CppClass type, string nameSpace, string depPath)
        {
            Logger.Log("Enter ReflectorInl", LogLevel.trace);
            cw.CurrentWriter.Dispose();
            cw = new CodeWriter(cw_options);
            cw.WriteLine($"#include \"autogen_reflector_{type.Name}.hpp\"");
            cw.WriteLine($"#include \"../../{depPath}\"");
            cw.WriteLine("namespace legion { using namespace core; }");
            cw.WriteLine("namespace legion::core");
            cw.OpenBraceBlock();
            reflectorFunction(type, nameSpace, false);
            reflectorFunction(type, nameSpace, false, true);
            cw.CloseBraceBlock();
            var output = cw.ToString();
            Logger.Log("Exit ReflectorInl", LogLevel.trace);
            return output;
        }

        public static string PrototypeImpl(CppClass type, string nameSpace, string depPath)
        {
            Logger.Log("Enter PrototypeInl", LogLevel.trace);
            cw.CurrentWriter.Dispose();
            cw = new CodeWriter(cw_options);
            cw.WriteLine($"#include \"autogen_prototype_{type.Name}.hpp\"");
            cw.WriteLine($"#include \"../../{depPath}\"");
            cw.WriteLine("namespace legion { using namespace core; }");
            cw.WriteLine("namespace legion::core");
            cw.OpenBraceBlock();
            prototypeFunction(type, nameSpace, false);
            cw.CloseBraceBlock();
            var output = cw.ToString();
            Logger.Log("Exit PrototypeInl", LogLevel.trace);
            return output;
        }

        public static string DummyReflectorImpl(CppClass type, string nameSpace, string depPath)
        {
            Logger.Log("Enter DummyReflectorImpl", LogLevel.trace);
            cw.CurrentWriter.Dispose();
            cw = new CodeWriter(cw_options);
            cw.WriteLine($"#include \"autogen_reflector_{type.Name}.hpp\"");
            cw.WriteLine($"#include \"../../{depPath}\"");
            cw.WriteLine("namespace legion { using namespace core; }");
            cw.WriteLine("namespace legion::core");
            cw.OpenBraceBlock();
            reflectorFunction(type, nameSpace, true);
            reflectorFunction(type, nameSpace, true, true);
            cw.CloseBraceBlock();
            var output = cw.ToString();
            Logger.Log("Exit ReflectorInl", LogLevel.trace);
            return output;
        }

        public static string DummyPrototypeImpl(CppClass type, string nameSpace, string depPath)
        {
            Logger.Log("Enter DummyPrototypeImpl", LogLevel.trace);
            cw.CurrentWriter.Dispose();
            cw = new CodeWriter(cw_options);
            cw.WriteLine($"#include \"autogen_prototype_{type.Name}.hpp\"");
            cw.WriteLine($"#include \"../../{depPath}\"");
            cw.WriteLine("namespace legion { using namespace core; }");
            cw.WriteLine("namespace legion::core");
            cw.OpenBraceBlock();
            prototypeFunction(type, nameSpace, true);
            cw.CloseBraceBlock();
            var output = cw.ToString();
            Logger.Log("Exit PrototypeInl", LogLevel.trace);
            return output;
        }

        static void reflectorMembers(CppContainerList<CppField> fields)
        {
            if (fields == null || fields.Count == 0)
                return;

            foreach (var field in fields)
            {
                if (field.Visibility == CppVisibility.Private || field.Visibility == CppVisibility.Protected)
                    continue;

                var actualType = field.Type.TypeKind;
                if (actualType == CppTypeKind.Typedef)
                    actualType = field.Type.GetCanonicalType().TypeKind;

                switch (actualType)
                {
                    case CppTypeKind.StructOrClass:
                        if ((field.Type is CppClass) && ((field.Type as CppClass).TemplateParameters.Count > 0))
                            continue;

                        if (walkNamespaceTree(field.Type.Parent as CppNamespace).Contains("std::"))
                            goto case CppTypeKind.Primitive;

                        cw.WriteLine($"refl.members.emplace(\"{field.Name}\", member_reference(\"{field.Name}\", make_reflector(obj.{field.Name})));");
                        break;
                    case CppTypeKind.Primitive:
                    case CppTypeKind.Enum:
                        cw.WriteLine($"refl.members.emplace(\"{field.Name}\", member_reference(\"{field.Name}\", primitive_reference{{typeHash(obj.{field.Name}), &obj.{field.Name}}}));");
                        break;
                }
            }

            foreach (var field in fields)
            {
                if (field.Attributes == null || field.Attributes.Count == 0)
                    continue;

                cw.OpenBraceBlock();
                cw.WriteLine($"auto& member = refl.members.at({field.Name});");
                foreach (CppAttribute attr in field.Attributes)
                {
                    var scopeStr = attr.Scope == null ? "" : attr.Scope + "::";
                    cw.WriteLine($"static const {scopeStr}{attr.Name}_attribute {attr.Name}_attr{{{attr.Arguments}}};");
                    cw.WriteLine($"member.attributes.push_back(std::cref({attr.Name}_attr));");
                }

                cw.CloseBraceBlock();
            }
        }

        static string walkNamespaceTree(CppNamespace container)
        {
            if (container == null)
                return "";

            return walkNamespaceTree(container.Parent as CppNamespace) + container.Name + "::";
        }

        static void reflectorBaseClasses(CppClass type, bool isConst)
        {
            foreach (var baseClass in type.BaseTypes)
            {
                var baseType = baseClass.Type.GetCanonicalType() as CppClass;

                string baseTypeFullName = walkNamespaceTree(baseType.Parent as CppNamespace) + baseType.Name;

                if (!baseType.Attributes.Contains(new CppAttribute("reflectable"), new AttributeNameComparer()))
                {
                    cw.WriteLine($"refl.baseclasses.push_back(reflector(typeHash<{baseTypeFullName}>(), nameOfType<{baseTypeFullName})>(), reflector::member_container(), std::addressof(obj))));");
                    continue;
                }
                cw.WriteLine($"refl.baseclasses.push_back(make_reflector(*static_cast<{(isConst ? "const " : "")}{baseTypeFullName}*>(&obj)))");
            }
        }

        static void reflectorFunction(CppClass type, string nameSpace, bool dummy, bool isConst = false)
        {
            Logger.Log("Enter reflectorFunction", LogLevel.trace);

            cw.WriteLine("template<>");

            if (isConst)
                cw.WriteLine($"L_NODISCARD const reflector make_reflector<const {nameSpace}::{type.Name}>(const {nameSpace}::{type.Name}& obj)");
            else
                cw.WriteLine($"L_NODISCARD reflector make_reflector<{nameSpace}::{type.Name}>({nameSpace}::{type.Name}& obj)");

            cw.OpenBraceBlock();

            if (isConst)
                cw.WriteLine("ptr_type address = reinterpret_cast<ptr_type>(std::addressof(obj));");

            cw.WriteLine("reflector refl;");
            cw.WriteLine($"refl.typeId = typeHash<{nameSpace}::{type.Name}>();");
            string nameSpaceName = nameSpace.Length > 0 ? nameSpace + "::" : "";
            cw.WriteLine($"refl.typeName = \"{nameSpaceName}{type.Name}\";");

            if (type.Attributes != null && !dummy)
            {
                foreach (CppAttribute attr in type.Attributes)
                {
                    cw.OpenBraceBlock();
                    var scopeStr = attr.Scope == null ? "" : attr.Scope + "::";
                    cw.WriteLine($"static const {scopeStr}{attr.Name}_attribute {attr.Name}_attr{{{attr.Arguments}}};");
                    cw.WriteLine($"refl.attributes.push_back(std::cref({attr.Name}_attr));");
                    cw.CloseBraceBlock();
                }
            }

            if (!dummy)
                reflectorMembers(type.Fields);

            if (isConst)
                cw.WriteLine("refl.data = reinterpret_cast<void*>(address);");
            else
                cw.WriteLine("refl.data = std::addressof(obj);");

            cw.WriteLine("return refl;");
            cw.CloseBraceBlock();
            Logger.Log("Exit reflectorFunction", LogLevel.trace);
        }

        static void prototypeMembers(CppContainerList<CppField> fields)
        {
            if (fields == null || fields.Count == 0)
                return;

            foreach (var field in fields)
            {
                if (field.Visibility == CppVisibility.Private || field.Visibility == CppVisibility.Protected)
                    continue;

                var actualType = field.Type.TypeKind;
                if (actualType == CppTypeKind.Typedef)
                    actualType = field.Type.GetCanonicalType().TypeKind;

                switch (actualType)
                {
                    case CppTypeKind.StructOrClass:
                        if ((field.Type is CppClass) && ((field.Type as CppClass).TemplateParameters.Count > 0))
                            continue;

                        if (walkNamespaceTree(field.Type.Parent as CppNamespace).Contains("std::"))
                            goto case CppTypeKind.Primitive;

                        cw.WriteLine($"prot.members.emplace(\"{field.Name}\", member_value(\"{field.Name}\", make_prototype(obj.{field.Name})));");
                        break;
                    case CppTypeKind.Primitive:
                    case CppTypeKind.Enum:
                        cw.WriteLine($"prot.members.emplace(\"{field.Name}\", member_value(\"{field.Name}\", primitive_value{{typeHash(obj.{field.Name}), std::any(obj.{field.Name})}}));");
                        break;
                }
            }

            foreach (var field in fields)
            {
                if (field.Attributes == null || field.Attributes.Count == 0)
                    continue;

                cw.OpenBraceBlock();
                cw.WriteLine($"auto& member = prot.members.at({field.Name});");
                foreach (CppAttribute attr in field.Attributes)
                {
                    var scopeStr = attr.Scope == null ? "" : attr.Scope + "::";
                    cw.WriteLine($"static const {scopeStr}{attr.Name}_attribute {attr.Name}_attr{{{attr.Arguments}}};");
                    cw.WriteLine($"member.attributes.push_back(std::cref({attr.Name}_attr));");
                }

                cw.CloseBraceBlock();
            }
        }

        static void prototypeBaseClasses(CppClass type)
        {
            foreach (var baseClass in type.BaseTypes)
            {
                var baseType = baseClass.Type.GetCanonicalType() as CppClass;

                string baseTypeFullName = walkNamespaceTree(baseType.Parent as CppNamespace) + baseType.Name;

                if (!baseType.Attributes.Contains(new CppAttribute("reflectable"), new AttributeNameComparer()))
                {
                    cw.WriteLine($"prot.baseclasses.push_back(prototype(typeHash<{baseTypeFullName}>(), nameOfType<{baseTypeFullName})>(), prototype::member_container(), std::addressof(obj))));");
                    continue;
                }
                cw.WriteLine($"prot.baseclasses.push_back(make_prototype(*static_cast<const {baseTypeFullName}*>(&obj)))");
            }
        }

        static void prototypeFunction(CppClass type, string nameSpace, bool dummy)
        {
            Logger.Log("Enter prototypeFunction", LogLevel.trace);

            cw.WriteLine("template<>");
            cw.WriteLine($"L_NODISCARD prototype make_prototype<{nameSpace}::{type.Name}>(const {nameSpace}::{type.Name}& obj)");
            cw.OpenBraceBlock();
            cw.WriteLine("prototype prot;");
            cw.WriteLine($"prot.typeId = typeHash<{nameSpace}::{type.Name}>();");

            string nameSpaceName = nameSpace.Length > 0 ? nameSpace + "::" : "";
            cw.WriteLine($"prot.typeName = \"{nameSpaceName}{type.Name}\";");

            if (type.Attributes != null && !dummy)
            {
                foreach (CppAttribute attr in type.Attributes)
                {
                    cw.OpenBraceBlock();
                    var scopeStr = attr.Scope == null ? "" : attr.Scope + "::";
                    cw.WriteLine($"static const {scopeStr}{attr.Name}_attribute {attr.Name}_attr{{{attr.Arguments}}};");
                    cw.WriteLine($"prot.attributes.push_back(std::cref({attr.Name}_attr));");
                    cw.CloseBraceBlock();
                }
            }

            if (!dummy)
                prototypeMembers(type.Fields);

            cw.WriteLine("return prot;");

            cw.CloseBraceBlock();
            Logger.Log("Exit prototypeFunction", LogLevel.trace);
        }
    }
}
