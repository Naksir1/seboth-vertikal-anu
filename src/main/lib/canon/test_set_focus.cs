using System;
using Canon.Eos.Framework;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== EosFocusMode Enum Values ===");
        foreach (var val in Enum.GetValues(typeof(EosFocusMode)))
        {
            Console.WriteLine("  " + val + " = " + (int)val);
        }

        try
        {
            using (var framework = new EosFramework())
            {
                var cameras = framework.GetCameraCollection();
                if (cameras.Count == 0) return;

                using (var camera = cameras[0])
                {
                    Console.WriteLine("Current FocusMode: " + camera.FocusMode);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception: " + ex.Message);
        }
    }
}
