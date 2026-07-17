using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

var root = args.Length > 0 ? args[0] : throw new ArgumentException("Project root is required.");
var key = Environment.GetEnvironmentVariable("DESKTOP_WIDGETS_WEATHER_KEY");
if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Weather key environment variable is missing.");

var plain = Encoding.UTF8.GetBytes(key.Trim());
try
{
    var encrypted = Dpapi.Protect(plain);
    var directory = Path.Combine(root, "secrets");
    Directory.CreateDirectory(directory);
    File.WriteAllBytes(Path.Combine(directory, "weather-credentials.dat"), encrypted);
    Console.WriteLine("Weather credential encrypted for the current Windows user.");
}
finally
{
    CryptographicOperations.ZeroMemory(plain);
    Environment.SetEnvironmentVariable("DESKTOP_WIDGETS_WEATHER_KEY", null);
}

static class Dpapi
{
    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob { public int Size; public IntPtr Data; }

    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern bool CryptProtectData(ref DataBlob input, string? description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr handle);

    public static byte[] Protect(byte[] input)
    {
        var inputPtr = Marshal.AllocHGlobal(input.Length);
        Marshal.Copy(input, 0, inputPtr, input.Length);
        var inputBlob = new DataBlob { Size = input.Length, Data = inputPtr };
        try
        {
            if (!CryptProtectData(ref inputBlob, "DesktopWidgets Weather API", IntPtr.Zero,
                IntPtr.Zero, IntPtr.Zero, 0, out var output))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var result = new byte[output.Size];
                Marshal.Copy(output.Data, result, 0, output.Size);
                return result;
            }
            finally { LocalFree(output.Data); }
        }
        finally { Marshal.FreeHGlobal(inputPtr); }
    }
}
