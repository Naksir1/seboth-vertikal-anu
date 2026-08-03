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

                    var handleField = typeof(EosObject).GetField("_handle", BindingFlags.NonPublic | BindingFlags.Instance);
                    IntPtr cameraRef = (IntPtr)handleField.GetValue(camera);

                    int volumeCount = 0;
                    uint err = EdsGetChildCount_Pin(cameraRef, out volumeCount);
                    Console.WriteLine("EdsGetChildCount err: 0x" + err.ToString("X") + ", Volume Count: " + volumeCount);

                    for (int i = 0; i < volumeCount; i++)
                    {
                        IntPtr volumeRef = IntPtr.Zero;
                        err = EdsGetChildAtIndex_Pin(cameraRef, i, out volumeRef);
                        Console.WriteLine("Volume index " + i + " err: 0x" + err.ToString("X"));
                        if (err == 0 && volumeRef != IntPtr.Zero)
                        {
                            Edsdk.EdsVolumeInfo vInfo;
                            err = Edsdk.EdsGetVolumeInfo(volumeRef, out vInfo);
                            Console.WriteLine("  Volume label: " + vInfo.szVolumeLabel);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception: " + ex.Message);
        }
    }

    [System.Runtime.InteropServices.DllImport("EDSDK.dll", EntryPoint = "EdsGetChildCount")]
    private static extern uint EdsGetChildCount_Pin(IntPtr inParentRef, out int outCount);

    [System.Runtime.InteropServices.DllImport("EDSDK.dll", EntryPoint = "EdsGetChildAtIndex")]
    private static extern uint EdsGetChildAtIndex_Pin(IntPtr inParentRef, int inIndex, out IntPtr outChildRef);
}
