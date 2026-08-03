using System;
using System.Reflection;
using Canon.Eos.Framework;
using Canon.Eos.Framework.Internal.SDK;

class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            using (var framework = new EosFramework())
            {
                var cameras = framework.GetCameraCollection();
                if (cameras.Count == 0)
                {
                    Console.WriteLine("No cameras found.");
                    return;
                }

                using (var camera = cameras[0])
                {
                    Console.WriteLine("Connected to: " + camera.DeviceDescription);
                    Console.WriteLine("ProductName: " + camera.ProductName);
                    Console.WriteLine("SerialNumber: " + camera.SerialNumber);
                    Console.WriteLine("FirmwareVersion: " + camera.FirmwareVersion);
                    
                    try
                    {
                        Console.WriteLine("AEMode: " + camera.AEMode);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to get AEMode: " + ex.Message);
                    }

                    try
                    {
                        Console.WriteLine("BatteryLevel: " + camera.BatteryLevel);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to get BatteryLevel: " + ex.Message);
                    }

                    try
                    {
                        Console.WriteLine("FocusMode: " + camera.FocusMode);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to get FocusMode: " + ex.Message);
                    }

                    try
                    {
                        Console.WriteLine("ImageQuality: " + camera.ImageQuality);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to get ImageQuality: " + ex.Message);
                    }

                    try
                    {
                        // Print raw kEdsPropID_SaveTo (0x10)
                        long saveTo = camera.GetProperty(0x00000010);
                        Console.WriteLine("Raw SaveTo property: " + saveTo);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to get SaveTo raw: " + ex.Message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception: " + ex.Message);
        }
    }
}
