using System;
using System.IO;
using System.Threading;
using System.Reflection;
using System.Windows.Forms;
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

                    var handleField = typeof(EosObject).GetField("_handle", BindingFlags.NonPublic | BindingFlags.Instance);
                    IntPtr cameraRef = (IntPtr)handleField.GetValue(camera);

                    // Set SaveTo to Camera-only (1) just like CanonHelper does
                    try
                    {
                        camera.SetProperty(0x00000010, 1);
                        Console.WriteLine(" kEdsPropID_SaveTo set to Camera (1)");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to set SaveTo: " + ex.Message);
                    }

                    Console.WriteLine("\n--- FILES BEFORE CAPTURE ---");
                    ListFiles(cameraRef);

                    Console.WriteLine("\n--- TRIGGERING SHUTTER ---");
                    try
                    {
                        camera.TakePictureNoAf();
                        Console.WriteLine("Shutter triggered with TakePictureNoAf");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("TakePictureNoAf failed: " + ex.Message + ". Trying TakePicture...");
                        try
                        {
                            camera.TakePicture();
                            Console.WriteLine("Shutter triggered with TakePicture");
                        }
                        catch (Exception ex2)
                        {
                            Console.WriteLine("TakePicture also failed: " + ex2.Message);
                        }
                    }

                    // Pump messages and events for 5 seconds
                    Console.WriteLine("Waiting 5 seconds for file write...");
                    for (int i = 0; i < 50; i++)
                    {
                        Application.DoEvents();
                        try { EdsGetEvent_Pin(); } catch { }
                        Thread.Sleep(100);
                    }

                    Console.WriteLine("\n--- FILES AFTER CAPTURE ---");
                    ListFiles(cameraRef);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception: " + ex.Message);
        }
    }

    static void ListFiles(IntPtr cameraRef)
    {
        int volumeCount = 0;
        uint err = EdsGetChildCount_Pin(cameraRef, out volumeCount);
        if (err != 0)
        {
            Console.WriteLine("Failed to get volumes: 0x" + err.ToString("X"));
            return;
        }

        for (int i = 0; i < volumeCount; i++)
        {
            IntPtr volumeRef = IntPtr.Zero;
            err = EdsGetChildAtIndex_Pin(cameraRef, i, out volumeRef);
            if (err == 0 && volumeRef != IntPtr.Zero)
            {
                Edsdk.EdsVolumeInfo vInfo;
                err = Edsdk.EdsGetVolumeInfo(volumeRef, out vInfo);
                Console.WriteLine("Volume " + i + " (" + vInfo.szVolumeLabel + "):");
                ListDirectory(volumeRef, "  ");
            }
        }
    }

    static void ListDirectory(IntPtr parentRef, string indent)
    {
        int count = 0;
        uint err = EdsGetChildCount_Pin(parentRef, out count);
        if (err != 0) return;

        for (int i = 0; i < count; i++)
        {
            IntPtr childRef = IntPtr.Zero;
            err = EdsGetChildAtIndex_Pin(parentRef, i, out childRef);
            if (err == 0 && childRef != IntPtr.Zero)
            {
                Edsdk.EdsDirectoryItemInfo info;
                err = Edsdk.EdsGetDirectoryItemInfo(childRef, out info);
                if (err == 0)
                {
                    if (info.isFolder != 0)
                    {
                        Console.WriteLine(indent + "[Folder] " + info.szFileName);
                        ListDirectory(childRef, indent + "  ");
                    }
                    else
                    {
                        Console.WriteLine(indent + "[File] " + info.szFileName + " (" + info.Size + " bytes)");
                    }
                }
            }
        }
    }

    [System.Runtime.InteropServices.DllImport("EDSDK.dll", EntryPoint = "EdsGetChildCount")]
    private static extern uint EdsGetChildCount_Pin(IntPtr inParentRef, out int outCount);

    [System.Runtime.InteropServices.DllImport("EDSDK.dll", EntryPoint = "EdsGetChildAtIndex")]
    private static extern uint EdsGetChildAtIndex_Pin(IntPtr inParentRef, int inIndex, out IntPtr outChildRef);

    [System.Runtime.InteropServices.DllImport("EDSDK.dll", EntryPoint = "EdsGetEvent")]
    private static extern uint EdsGetEvent_Pin();
}
