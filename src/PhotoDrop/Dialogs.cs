using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace PhotoDrop;

static class Dialogs
{
    public static void Info(string text) =>
        Win32.MessageBox(IntPtr.Zero, text, Program.AppName, Win32.MB_OK | Win32.MB_ICONINFO);

    public static void Warn(string text) =>
        Win32.MessageBox(IntPtr.Zero, text, Program.AppName, Win32.MB_OK | Win32.MB_ICONWARNING);

    public static bool AskYesNo(string text) =>
        Win32.MessageBox(IntPtr.Zero, text, Program.AppName,
                         Win32.MB_YESNO | Win32.MB_ICONQUESTION) == Win32.IDYES;

    /// <summary>The modern folder picker. Returns null if the user backed out.</summary>
    // COM dispatch is by vtable slot, so every method must survive trimming even though
    // most go uncalled - drop one and every later slot shifts. IL2050 warns about exactly
    // this; these roots are what make it safe.
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(IFileDialog))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(IShellItem))]
    public static string? PickFolder(string current)
    {
        var clsid = new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7");   // FileOpenDialog
        var iid = typeof(IFileDialog).GUID;

        int hr;
        IFileDialog? dialog;
        try
        {
            // Marshalling itself can throw here, so this call belongs inside a guard too.
            hr = CoCreateInstance(ref clsid, IntPtr.Zero, 1, ref iid, out dialog);
        }
        catch (Exception ex)
        {
            Warn($"The folder picker couldn't open.\n\n{ex.Message}");
            return null;
        }

        if (hr != 0 || dialog is null)
        {
            // Never silently look like "the user cancelled" - that hides real breakage.
            Warn($"The folder picker couldn't open (0x{hr:X8}).");
            return null;
        }

        try
        {
            dialog.SetOptions(FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST);
            dialog.SetTitle("Where should photos from your phone be saved?");

            if (Directory.Exists(current))
            {
                var shellItemId = typeof(IShellItem).GUID;
                if (SHCreateItemFromParsingName(current, IntPtr.Zero, ref shellItemId,
                                                out var start) == 0 && start is not null)
                {
                    dialog.SetFolder(start);
                    Marshal.ReleaseComObject(start);
                }
            }

            if (dialog.Show(IntPtr.Zero) != 0) return null;    // cancelled

            dialog.GetResult(out var chosen);
            if (chosen is null) return null;

            try
            {
                chosen.GetDisplayName(SIGDN_FILESYSPATH, out var buffer);
                if (buffer == IntPtr.Zero) return null;
                try { return Marshal.PtrToStringUni(buffer); }
                finally { Marshal.FreeCoTaskMem(buffer); }
            }
            finally { Marshal.ReleaseComObject(chosen); }
        }
        catch (Exception ex)
        {
            Warn($"The folder picker didn't open.\n\n{ex.Message}");
            return null;
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    const uint FOS_PICKFOLDERS = 0x20;
    const uint FOS_FORCEFILESYSTEM = 0x40;
    const uint FOS_PATHMUSTEXIST = 0x800;
    const uint SIGDN_FILESYSPATH = 0x80058000;

    [DllImport("ole32.dll")]
    static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, uint context,
                                       ref Guid iid,
                                       [MarshalAs(UnmanagedType.Interface)] out IFileDialog? result);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid iid,
                                                  [MarshalAs(UnmanagedType.Interface)] out IShellItem? item);

    // Methods must stay in vtable order even though most go unused; unneeded parameter
    // types are IntPtr because every one of them is pointer-sized.
    [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFileDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        void SetFileTypes(uint count, IntPtr filters);
        void SetFileTypeIndex(uint index);
        void GetFileTypeIndex(out uint index);
        void Advise(IntPtr sink, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(uint options);
        void GetOptions(out uint options);
        void SetDefaultFolder(IShellItem item);
        void SetFolder(IShellItem item);
        void GetFolder(out IShellItem item);
        void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void GetResult(out IShellItem item);
        void AddPlace(IShellItem item, int alignment);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
        void Close(int result);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr filter);
    }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItem
    {
        void BindToHandler(IntPtr context, ref Guid bhid, ref Guid iid, out IntPtr result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint form, out IntPtr name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }
}
