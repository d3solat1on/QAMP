using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace QAMP;

internal static class ShortcutHelpers
{
    public static void EnsureStartMenuShortcut(string shortcutName, string targetPath, string appId)
    {
        object? shellLinkObject = null;
        PropVariant appIdValue = default;

        try
        {
            var programs = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "Windows", "Start Menu", "Programs"
            );
            Directory.CreateDirectory(programs);
            var linkPath = Path.Combine(programs, shortcutName + ".lnk");

            shellLinkObject = new ShellLinkComObject();
            var shellLink = (IShellLinkW)shellLinkObject;
            var persistFile = (IPersistFile)shellLinkObject;

            if (File.Exists(linkPath))
            {
                Marshal.ThrowExceptionForHR(persistFile.Load(linkPath, 0));
            }

            Marshal.ThrowExceptionForHR(shellLink.SetPath(Path.GetFullPath(targetPath)));
            Marshal.ThrowExceptionForHR(shellLink.SetWorkingDirectory(Path.GetDirectoryName(Path.GetFullPath(targetPath)) ?? string.Empty));
            Marshal.ThrowExceptionForHR(shellLink.SetDescription("QAMP - Quick Attractive Media Player"));

            var propertyStore = (IPropertyStore)shellLinkObject;
            var appIdKey = new PropertyKey(
                new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
                5);
            appIdValue = PropVariant.FromString(appId);
            Marshal.ThrowExceptionForHR(propertyStore.SetValue(ref appIdKey, ref appIdValue));
            Marshal.ThrowExceptionForHR(propertyStore.Commit());
            Marshal.ThrowExceptionForHR(persistFile.Save(linkPath, true));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Start menu shortcut update failed: {ex}");
            App.LogException(ex, "StartMenuShortcutError");
        }
        finally
        {
            if (appIdValue.PointerValue != IntPtr.Zero)
            {
                _ = PropVariantClear(ref appIdValue);
            }

            if (shellLinkObject != null && Marshal.IsComObject(shellLinkObject))
            {
                Marshal.FinalReleaseComObject(shellLinkObject);
            }
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    [ClassInterface(ClassInterfaceType.None)]
    private class ShellLinkComObject
    {
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        [PreserveSig] int GetPath(System.Text.StringBuilder path, int pathLength, IntPtr findData, uint flags);
        [PreserveSig] int GetIDList(out IntPtr itemIdList);
        [PreserveSig] int SetIDList(IntPtr itemIdList);
        [PreserveSig] int GetDescription(System.Text.StringBuilder description, int descriptionLength);
        [PreserveSig] int SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
        [PreserveSig] int GetWorkingDirectory(System.Text.StringBuilder directory, int directoryLength);
        [PreserveSig] int SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        [PreserveSig] int GetArguments(System.Text.StringBuilder arguments, int argumentsLength);
        [PreserveSig] int SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        [PreserveSig] int GetHotkey(out short hotkey);
        [PreserveSig] int SetHotkey(short hotkey);
        [PreserveSig] int GetShowCmd(out int showCommand);
        [PreserveSig] int SetShowCmd(int showCommand);
        [PreserveSig] int GetIconLocation(System.Text.StringBuilder iconPath, int iconPathLength, out int iconIndex);
        [PreserveSig] int SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        [PreserveSig] int SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        [PreserveSig] int Resolve(IntPtr window, uint flags);
        [PreserveSig] int SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }

    [ComImport]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        [PreserveSig] int GetClassID(out Guid classId);
        [PreserveSig] int IsDirty();
        [PreserveSig] int Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint mode);
        [PreserveSig] int Save([MarshalAs(UnmanagedType.LPWStr)] string fileName, [MarshalAs(UnmanagedType.Bool)] bool remember);
        [PreserveSig] int SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string fileName);
        [PreserveSig] int GetCurFile(out IntPtr fileName);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey(Guid formatId, uint propertyId)
    {
        public Guid FormatId = formatId;
        public uint PropertyId = propertyId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        public ushort VariantType;
        private ushort _reserved1;
        private ushort _reserved2;
        private ushort _reserved3;
        public IntPtr PointerValue;

        public static PropVariant FromString(string value) => new()
        {
            VariantType = 31,
            PointerValue = Marshal.StringToCoTaskMemUni(value)
        };
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant propertyVariant);
}