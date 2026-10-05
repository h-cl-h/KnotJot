using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace KnotJotTextStyleEditor;

// EN: Bind the preview browser and descendants to one kill-on-close Windows job, including launcher relaunches and unexpected editor termination.
// ZH: 将预览浏览器及子进程绑定到关闭即终止的 Windows 作业，涵盖启动器重启及编辑器意外终止。
internal sealed class FrameBrowserJob : IDisposable
{
    readonly SafeFileHandle handle;
    // EN: Configure process ownership before browser work begins; failure is explicit rather than falling back to unowned child processes.
    // ZH: 浏览器工作开始前配置进程所有权；失败时明确报错，不回退为无所有权的子进程。
    public FrameBrowserJob()
    {
        handle = CreateJobObject(IntPtr.Zero, null);
        if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        var info = new ExtendedLimits { Basic = new BasicLimits { LimitFlags = 0x2000 } };
        if (!SetInformationJobObject(handle, 9, ref info, (uint)Marshal.SizeOf<ExtendedLimits>())) { var error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error); }
    }
    // EN: Create the browser suspended, attach it before any child can execute, then resume; all error paths terminate the exact owned native process.
    // ZH: 以挂起状态创建浏览器，在任何子进程执行前附加到作业再恢复；全部错误路径仅终止精确自有原生进程。
    public Process Start(string executable, IEnumerable<string> arguments)
    {
        var startup = new StartupInfo { Size = (uint)Marshal.SizeOf<StartupInfo>(), Flags = 1, ShowWindow = 0 };
        var command = new StringBuilder(Quote(executable) + " " + string.Join(" ", arguments.Select(Quote)));
        if (!CreateProcess(executable, command, IntPtr.Zero, IntPtr.Zero, false, 0x08000004, IntPtr.Zero, null, ref startup, out var native)) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            if (!AssignProcessToJobObject(handle, native.Process)) throw new Win32Exception(Marshal.GetLastWin32Error());
            var process = Process.GetProcessById((int)native.ProcessId);
            _ = process.Handle;
            if (ResumeThread(native.Thread) == uint.MaxValue) { process.Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error()); }
            return process;
        }
        catch { TerminateProcess(native.Process, 1); throw; }
        finally { CloseHandle(native.Thread); CloseHandle(native.Process); }
    }
    // EN: Quote Windows argv values without a shell, doubling backslashes only before quotes or the closing delimiter.
    // ZH: 不经 Shell 引用 Windows 参数值，仅在引号或结束分隔符前将反斜线加倍。
    static string Quote(string value)
    {
        var result = new StringBuilder("\""); var slashes = 0;
        foreach (var c in value) { if (c == '\\') { slashes++; continue; } result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes).Append(c); slashes = 0; }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
    // EN: Closing the sole job handle terminates only its owned processes, even if the original launcher has exited.
    // ZH: 关闭唯一作业句柄，仅终止其拥有的进程，即使原启动器已经退出。
    public void Dispose() => handle.Dispose();

    // EN: Preserve native pointer-sized layouts so process limits work in both 32-bit and 64-bit hosts.
    // ZH: 保留原生指针大小布局，使进程限制适用于 32 位和 64 位宿主。
    [StructLayout(LayoutKind.Sequential)] struct BasicLimits { public long ProcessTime, JobTime; public uint LimitFlags; public UIntPtr MinimumWorkingSet, MaximumWorkingSet; public uint ActiveProcesses; public UIntPtr Affinity; public uint PriorityClass, SchedulingClass; }
    [StructLayout(LayoutKind.Sequential)] struct IoCounters { public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)] struct ExtendedLimits { public BasicLimits Basic; public IoCounters Io; public UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory; }
    // EN: Preserve the native startup/process information layout used only to create a hidden suspended child.
    // ZH: 保留原生启动和进程信息布局，仅用于创建隐藏挂起子进程。
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct StartupInfo { public uint Size; public string? Reserved, Desktop, Title; public uint X, Y, Width, Height, XChars, YChars, Fill, Flags; public ushort ShowWindow, ReservedSize; public IntPtr ReservedData, Input, Output, Error; }
    [StructLayout(LayoutKind.Sequential)] struct ProcessInformation { public IntPtr Process, Thread; public uint ProcessId, ThreadId; }
    // EN: Create an unnamed private Windows job; no external job or browser is opened.
    // ZH: 创建无名称私有 Windows 作业，不打开外部作业或浏览器。
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern SafeFileHandle CreateJobObject(IntPtr attributes, string? name);
    // EN: Set only the ownership cleanup flag on the newly created job.
    // ZH: 仅为新建作业设置所有权清理标记。
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool SetInformationJobObject(SafeFileHandle job, int informationClass, ref ExtendedLimits information, uint length);
    // EN: Add the owned browser process handle to the job; Windows propagates membership to its children.
    // ZH: 将自有浏览器进程句柄加入作业；Windows 将成员关系传播到其子进程。
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
    // EN: Create the exact executable with argv data and no inherited handles; flags keep it hidden and suspended until job assignment.
    // ZH: 通过参数数据创建精确可执行文件，不继承句柄；标记使其隐藏并保持挂起，直到作业分配完成。
    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool CreateProcess(string application, StringBuilder command, IntPtr processAttributes, IntPtr threadAttributes, bool inheritHandles, uint flags, IntPtr environment, string? directory, ref StartupInfo startup, out ProcessInformation process);
    // EN: Resume only the initial owned thread after its process is secured in the job.
    // ZH: 仅在进程被安全纳入作业后恢复自有初始线程。
    [DllImport("kernel32.dll", SetLastError = true)] static extern uint ResumeThread(IntPtr thread);
    // EN: Terminate the exact newly created suspended process on an initialization error.
    // ZH: 初始化错误时终止精确新建挂起进程。
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool TerminateProcess(IntPtr process, uint exitCode);
    // EN: Release temporary native process/thread handles after ownership is established or rolled back.
    // ZH: 所有权建立或回滚后释放临时原生进程及线程句柄。
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool CloseHandle(IntPtr handle);
}
