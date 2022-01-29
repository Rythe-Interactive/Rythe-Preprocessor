using System;
using CppAst;
using CppAst.CodeGen;

namespace RytheTributary
{
    class Program
    {
        static void Main(string[] args)
        {
            CppParserOptions options = new CppParserOptions();
            options.ParseAttributes = true;
            var compilation = CppParser.ParseFile(@"D:\Repos\RytheTributary\data\ReflectableTest.cpp", options);
            //foreach (var diagnostic in compilation.Diagnostics.Messages)
            //{
            //    if (diagnostic.Type != CppLogMessageType.Warning)
            //        Console.WriteLine(diagnostic); 
            //}

            foreach (CppClass cppClass in compilation.Classes)
            {
                foreach (CppAttribute attr in cppClass.Attributes)
                {
                    if (!attr.Scope.Equals("legion") || !attr.Scope.Equals("rythe"))
                        continue;

                    if(attr.Name.Equals("reflectable"))
                    {
                        Console.WriteLine(cppClass.Name);
                        for (int i = 0; i < cppClass.Fields.Count; i++)
                        {
                            Console.WriteLine("{0} {1} {2}",cppClass.Fields[i].Visibility,cppClass.Fields[i].Type, cppClass.Fields[i].Name);
                        }
                    }
                }
            }
        }
    }
}
