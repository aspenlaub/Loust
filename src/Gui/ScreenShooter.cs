using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;

// ReSharper disable UnusedMember.Global

namespace Aspenlaub.Net.GitHub.CSharp.Loust.Gui;

public class ScreenShooter {
    public static string TakeScreenShot() {
        var rectangle = Screen.AllScreens[0].Bounds;
        var bitmap = new Bitmap(rectangle.Width, rectangle.Height, PixelFormat.Format32bppArgb);
        var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(rectangle.Left, rectangle.Top, 0, 0, rectangle.Size);
        var folder = new Folder(Path.GetTempPath()).SubFolder("AspenlaubTemp").SubFolder("LoustScreenShots");
        folder.CreateIfNecessary();
        var guid = Guid.NewGuid().ToString();
        var fileName = folder.FullName + @"\" + guid + ".jpg";
        bitmap.Save(fileName, ImageFormat.Jpeg);
        return fileName;
    }
}