using System.ComponentModel;
using System.Runtime.InteropServices;
using Parallax.Core.Capture;
using Parallax.Core.Platform;

namespace Parallax.Platform.Windows;

public sealed class WindowsClipboardService : IClipboardService
{
    private const uint CfDib = 8;
    private const uint GmemMoveable = 0x0002;
    private const uint GmemZeroInit = 0x0040;
    private const uint BiRgb = 0;

    public ClipboardImageResult CopyImage(CaptureImage image)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new ClipboardImageResult(false, "Image clipboard copying is only available on Windows.");
        }

        IntPtr memory = IntPtr.Zero;
        try
        {
            byte[] dib = CreateDeviceIndependentBitmap(image);
            memory = GlobalAlloc(GmemMoveable | GmemZeroInit, (UIntPtr)dib.Length);
            if (memory == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            IntPtr target = GlobalLock(memory);
            if (target == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                Marshal.Copy(dib, 0, target, dib.Length);
            }
            finally
            {
                GlobalUnlock(memory);
            }

            if (!OpenClipboard(IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                if (!EmptyClipboard())
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                if (SetClipboardData(CfDib, memory) == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                memory = IntPtr.Zero;
                return new ClipboardImageResult(true, "Copied screenshot to the clipboard.");
            }
            finally
            {
                CloseClipboard();
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return new ClipboardImageResult(false, $"Could not copy screenshot to clipboard: {ex.Message}");
        }
        finally
        {
            if (memory != IntPtr.Zero)
            {
                GlobalFree(memory);
            }
        }
    }

    private static byte[] CreateDeviceIndependentBitmap(CaptureImage image)
    {
        const int headerSize = 40;
        int stride = image.Width * 4;
        int imageBytes = stride * image.Height;
        using var stream = new MemoryStream(headerSize + imageBytes);
        using var writer = new BinaryWriter(stream);
        writer.Write(headerSize);
        writer.Write(image.Width);
        writer.Write(image.Height);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(BiRgb);
        writer.Write((uint)imageBytes);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0u);
        writer.Write(0u);

        for (int y = image.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var color = image.GetPixel(x, y);
                writer.Write(color.B);
                writer.Write(color.G);
                writer.Write(color.R);
                writer.Write(color.A);
            }
        }

        return stream.ToArray();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);
}
