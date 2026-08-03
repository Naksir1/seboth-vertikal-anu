using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
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

                    var handleField = typeof(EosObject).GetField("_handle", BindingFlags.NonPublic | BindingFlags.Instance);
                    IntPtr cameraRef = (IntPtr)handleField.GetValue(camera);

                    bool captureDone = false;
                    camera.PictureTaken += (sender, e) =>
                    {
                        Console.WriteLine("=== EVENT_PictureTaken ===");
                        captureDone = true;
                    };

                    // Let's set up event pumping loop
                    Thread eventThread = new Thread(() =>
                    {
                        for (int i = 0; i < 100; i++)
                        {
                            Application.DoEvents();
                            try { EdsGetEvent_Pin(); } catch { }
                            Thread.Sleep(100);
                            if (captureDone) break;
                        }
                    });
                    eventThread.IsBackground = true;
                    eventThread.Start();

                    Console.WriteLine("\n--- Simulating Shutter Button Press ---");

                    // 1. Press Halfway (focus)
                    Console.WriteLine("Pressing shutter halfway (0x00000004, param: 1)...");
                    uint err = EdsSendCommand(cameraRef, 4, 1);
                    Console.WriteLine("Press halfway err: 0x" + err.ToString("X"));

                    Thread.Sleep(1000); // Wait for focus lock

                    // 2. Press Completely (take photo)
                    Console.WriteLine("Pressing shutter completely (0x00000004, param: 3)...");
                    err = EdsSendCommand(cameraRef, 4, 3);
                    Console.WriteLine("Press completely err: 0x" + err.ToString("X"));

                    Thread.Sleep(1000); // Settle time

                    // 3. Release Shutter (off)
                    Console.WriteLine("Releasing shutter button (0x00000004, param: 0)...");
                    err = EdsSendCommand(cameraRef, 4, 0);
                    Console.WriteLine("Release shutter err: 0x" + err.ToString("X"));

                    // Wait to see if event is received
                    Console.WriteLine("Waiting 5 seconds for PictureTaken event...");
                    Thread.Sleep(5000);

                    if (captureDone)
                    {
                        Console.WriteLine("=== SUCCESS ===");
                    }
                    else
                    {
                        Console.WriteLine("=== TIMEOUT ===");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception: " + ex.Message);
        }
    }

    [DllImport("EDSDK.dll")]
    private static extern uint EdsSendCommand(IntPtr inRef, uint inCommand, int inParam);

    [DllImport("EDSDK.dll", EntryPoint = "EdsGetEvent")]
    private static extern uint EdsGetEvent_Pin();
}
