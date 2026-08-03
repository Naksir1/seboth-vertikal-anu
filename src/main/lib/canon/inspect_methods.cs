using System;
using System.Reflection;
using Canon.Eos.Framework;

class Program
{
    static void Main()
    {
        Type type = typeof(EosCamera);
        string[] methods = { "SavePicturesToCamera", "SavePicturesToHost", "SavePicturesToHostAndCamera" };

        foreach (var name in methods)
        {
            Console.WriteLine("Method: " + name);
            var overloads = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            foreach (var method in overloads)
            {
                if (method.Name == name)
                {
                    Console.Write("  (");
                    var parameters = method.GetParameters();
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        Console.Write(parameters[i].ParameterType.FullName + " " + parameters[i].Name);
                        if (i < parameters.Length - 1) Console.Write(", ");
                    }
                    Console.WriteLine(")");
                }
            }
        }
    }
}
