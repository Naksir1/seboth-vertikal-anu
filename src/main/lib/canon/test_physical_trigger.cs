using System;
using System.IO;
using System.Threading;
using System.Reflection;
using System.Windows.Forms;
using Canon.Eos.Framework;
using Canon.Eos.Framework.Eventing;

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

                    camera.Error += (sender, e) => {
                        Console.WriteLine("=== CAMERA ERROR EVENT: " + e.Exception.Message);
                    };

                    camera.PropertyChanged += (sender, e) => {
                        Console.WriteLine("=== PROPERTY CHANGED EVENT ===");
                    };

                    bool captureDone = false;
                    camera.PictureTaken += (sender, e) =>
                    {
                        try
                        {
                            Console.WriteLine("=== EVENT_PictureTaken ===");
                            using (Stream stream = e.GetStream())
                            using (MemoryStream ms = new MemoryStream())
                            {
                                stream.CopyTo(ms);
                                byte[] bytes = ms.ToArray();
                                Console.WriteLine("Image size received: " + bytes.Length + " bytes");
                                File.WriteAllBytes("test_physical_trigger.jpg", bytes);
                            }
                            captureDone = true;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("=== EVENT_ERROR: " + ex.Message);
                            captureDone = true;
                        }
                    };

                    Console.WriteLine("Configuring SavePicturesToHost...");
                    try
                    {
                        camera.SavePicturesToHost(Environment.CurrentDirectory, Environment.CurrentDirectory);
                        Console.WriteLine("SavePicturesToHost succeeded");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("SavePicturesToHost failed: " + ex.Message);
                    }

                    Console.WriteLine("\n=======================================================");
                    Console.WriteLine("PLEASE PRESS THE SHUTTER BUTTON PHYSICALLY ON THE CAMERA BODY!");
                    Console.WriteLine("We will wait 30 seconds for the capture...");
                    Console.WriteLine("=======================================================");

                    // Pump messages and events for 30 seconds
                    for (int i = 0; i < 300; i++)
                    {
                        Application.DoEvents();
                        try { EdsGetEvent_Pin(); } catch { }
                        Thread.Sleep(100);
                        if (captureDone) break;
                    }

                    if (captureDone)
                    {
                        Console.WriteLine("=== SUCCESS: Captured via physical shutter press! ===");
                    }
                    else
                    {
                        Console.WriteLine("=== TIMEOUT: No photo captured within 30 seconds ===");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception in Main: " + ex.Message);
        }
    }

    [System.Runtime.InteropServices.DllImport("EDSDK.dll", EntryPoint = "EdsGetEvent")]
    private static extern uint EdsGetEvent_Pin();
}
