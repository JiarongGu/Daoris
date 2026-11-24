using System.Runtime.InteropServices;

namespace Daoris.Driver;

public sealed partial class ProcessJob
{
    /// <summary>
    /// The most memory the job's processes have held at once, in bytes (the system's <c>PeakJobMemoryUsed</c>): what the room learns a
    /// gate's need from. Null where there is no job or the figure did not read.
    /// </summary>
    public long? PeakMemory
    {
        get
        {
            var handle = Volatile.Read(ref _handle);
            if (handle == IntPtr.Zero) return null;
            var size = Marshal.SizeOf<JobObjectExtendedLimitInformation>();
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (!QueryInformationJobObject(handle, ExtendedLimitInformation, buffer, (uint)size, IntPtr.Zero)) return null;
                return (long)Marshal.PtrToStructure<JobObjectExtendedLimitInformation>(buffer).PeakJobMemoryUsed;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
