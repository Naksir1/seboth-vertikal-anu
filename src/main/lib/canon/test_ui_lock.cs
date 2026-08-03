using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
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

                    // Send UI Lock
                    uint err = EdsSendStatusCommand(cameraRef, 1, 0); // 1 = kEdsCameraStatusCommand_UILock
                    Console.WriteLine("SendStatusCommand UILock err: 0x" + err.ToString("X"));

                    Thread.Sleep(500);

                    try
                    {
                        // Try setting SaveTo = Camera (1)
                        err = EdsSetPropertyData(cameraRef, 0x00000010, 0, 4, 1);
                        Console.WriteLine("Direct set SaveTo = Camera (1) err: 0x" + err.ToString("X"));
                    }
                    finally
                    {
                        // Send UI Unlock
                        err = EdsSendStatusCommand(cameraRef, 2, 0); // 2 = kEdsCameraStatusCommand_UIUnLock
                        Console.WriteLine("SendStatusCommand UIUnlock err: 0x" + err.ToString("X"));
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
    private static extern uint EdsSendStatusCommand(IntPtr inRef, uint inStatusCommand, int inParam);

    [DllImport("EDSDK.dll")]
    private static extern uint EdsSetPropertyData(IntPtr inRef, uint inPropertyId, int inParam, int inSize, uint inValue);
}
