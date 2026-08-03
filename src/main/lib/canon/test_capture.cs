using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Canon.Eos.Framework;
using Canon.Eos.Framework.Eventing;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            using (var framework = new EosFramework())
            {
                var cameras = framework.GetCameraCollection();
                if (cameras.Count == 0)
                {
                    Console.WriteLine("===ERROR===No cameras found.");
                    return;
                }

                using (var camera = cameras[0])
                {
                    Console.WriteLine("===CONNECTED===" + camera.DeviceDescription);
                    
                    bool captureDone = false;
                    
                    camera.PictureTaken += (sender, e) =>
                    {
                        try
                        {
                            Console.WriteLine("===EVENT_PictureTaken===");
                            using (Stream stream = e.GetStream())
                            using (MemoryStream ms = new MemoryStream())
                            {
                                stream.CopyTo(ms);
                                byte[] bytes = ms.ToArray();
                                Console.WriteLine("Image size: " + bytes.Length + " bytes");
                                File.WriteAllBytes("test_capture_cs.jpg", bytes);
                            }
                            captureDone = true;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("===EVENT_ERROR===" + ex.Message);
                            captureDone = true;
                        }
                    };

                    camera.SavePicturesToHost(Environment.CurrentDirectory, "test_capture_prefix");
                    
                    Console.WriteLine("===TRIGGERING===");
                    try
                    {
                        camera.TakePictureNoAf();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("TakePictureNoAf failed: " + ex.Message + ". Falling back to TakePicture...");
                        camera.TakePicture();
                    }
                    
                    // Loop to pump messages and trigger EdsGetEvent
                    int timeoutCount = 100; // 10 seconds timeout
                    while (!captureDone && timeoutCount > 0)
                    {
                        Application.DoEvents();
                        // Call native EdsGetEvent if needed
                        try
                        {
                            EdsGetEvent_Pin();
                        }
                        catch { }
                        
                        Thread.Sleep(100);
                        timeoutCount--;
                    }

                    if (captureDone)
                    {
                        Console.WriteLine("===SUCCESS===");
                    }
                    else
                    {
                        Console.WriteLine("===TIMEOUT===");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("===EXCEPTION===" + ex.Message);
        }
    }

    [System.Runtime.InteropServices.DllImport("EDSDK.dll", EntryPoint = "EdsGetEvent")]
    private static extern uint EdsGetEvent_Pin();
}
