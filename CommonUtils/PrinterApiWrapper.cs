using System.Runtime.InteropServices;
using System.Security;


namespace CommonUtils
{
    using static System.Runtime.InteropServices.CharSet;
    using static System.Runtime.InteropServices.CallingConvention;

    public static class PrinterApiWrapper
    {
        const string winspool = "winspool.Drv";
        public const int PRINTER_ACCESS_USE = 0x00000008; // позволить выполнять базовые  задачи
        public const int PRINTER_ACCESS_ADMINISTER = 0x00000004; // позволить выполнять задачи на уровне администратора, такие как SetPrinter

        #region Structures

        [StructLayout(LayoutKind.Sequential, CharSet = Auto)]
        internal struct PrinterDefaults
        {
            public IntPtr pDevMode;
            [MarshalAs(UnmanagedType.LPTStr)] public string pDatatype;
            [MarshalAs(UnmanagedType.I4)] public int DesiredAccess;
        };

        [StructLayout(LayoutKind.Sequential, CharSet = Auto)]
        internal struct Size
        {
            public int width;
            public int height;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = Auto)]
        internal struct Rect
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = Auto)]
        internal struct FormInfo1
        {
            public uint Flags;
            public string pName;
            public Size Size;
            public Rect ImageableArea;
        };

        #endregion Structures


        #region Functions

        [DllImport(winspool, EntryPoint = "SetDefaultPrinter", SetLastError = true, CharSet = Unicode, ExactSpelling = false, CallingConvention = StdCall), SuppressUnmanagedCodeSecurity()]
        internal static extern bool SetDefaultPrinter(string printerName);


        [DllImport(winspool, EntryPoint = "OpenPrinter", SetLastError = true, CharSet = Unicode, ExactSpelling = false, CallingConvention = StdCall), SuppressUnmanagedCodeSecurity()]
        internal static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPTStr)] string printerName, out IntPtr phPrinter, ref PrinterDefaults pd);


        [DllImport(winspool, EntryPoint = "ClosePrinter", SetLastError = true, CharSet = Unicode, ExactSpelling = false, CallingConvention = StdCall), SuppressUnmanagedCodeSecurity()]
        internal static extern bool ClosePrinter(IntPtr phPrinter);


        [DllImport(winspool, EntryPoint = "AddFormW", SetLastError = true, CharSet = Unicode, ExactSpelling = true, CallingConvention = StdCall), SuppressUnmanagedCodeSecurity()]
        internal static extern bool AddForm(IntPtr phPrinter, [MarshalAs(UnmanagedType.I4)] int level, ref FormInfo1 form);


        [DllImport(winspool, EntryPoint = "DeleteForm", SetLastError = true, CharSet = Unicode, ExactSpelling = false, CallingConvention = StdCall), SuppressUnmanagedCodeSecurity()]
        internal static extern bool DeleteForm(IntPtr phPrinter, [MarshalAs(UnmanagedType.LPTStr)] string pName);


        [DllImport("kernel32.dll", EntryPoint = "GetLastError", SetLastError = false, ExactSpelling = true, CallingConvention = StdCall), SuppressUnmanagedCodeSecurity()]
        internal static extern int GetLastError();

        #endregion Functions



    }


}
