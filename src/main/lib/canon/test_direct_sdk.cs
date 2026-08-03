using System;
using System.Reflection;
using System.Runtime.InteropServices;
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

                    // Test reading property SaveTo
                    uint saveToVal = 0;
                    uint err = EdsGetPropertyData(cameraRef, 0x00000010, 0, out saveToVal);
                    Console.WriteLine("EdsGetPropertyData SaveTo err: 0x" + err.ToString("X") + ", value: " + saveToVal);

                    // Try setting SaveTo = Host (2)
                    err = EdsSetPropertyData(cameraRef, 0x00000010, 0, 4, 2);
                    Console.WriteLine("EdsSetPropertyData SaveTo = Host (2) err: 0x" + err.ToString("X"));

                    // Try setting SaveTo = Both (3)
                    err = EdsSetPropertyData(cameraRef, 0x00000010, 0, 4, 3);
                    Console.WriteLine("EdsSetPropertyData SaveTo = Both (3) err: 0x" + err.ToString("X"));

                    // Try setting SaveTo = Camera (1)
                    err = EdsSetPropertyData(cameraRef, 0x00000010, 0, 4, 1);
                    Console.WriteLine("EdsSetPropertyData SaveTo = Camera (1) err: 0x" + err.ToString("X"));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception: " + ex.Message);
        }
    }

    [DllImport("EDSDK.dll")]
    private static extern uint EdsGetPropertyData(IntPtr inRef, uint inPropertyId, int inParam, out uint outValue);

    [DllImport("EDSDK.dll")]
    private static extern uint EdsSetPropertyData(IntPtr inRef, uint inPropertyId, int inParam, int inSize, uint inValue);
}
