using System;
using System.Threading.Tasks;

namespace WindowsFormschoRobotcuaG8
{
    public class PythonBridge
    {
        public event Action<string> OnPythonLog;

        public async Task RunPythonScriptAsync(string scriptName, string arguments)
        {
            // Bỏ toàn bộ code gọi process ngầm. 
            // Chỉ in ra dòng nhắc nhở lên giao diện C#
            OnPythonLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] [Hệ THỐNG] Chế độ thủ công: Vui lòng tự chạy file {scriptName} trên Terminal/VS Code.");
            await Task.CompletedTask;
        }

        public void StopPython()
        {
            // Không làm gì cả, hệ thống không còn quản lý tiến trình Python nữa
            OnPythonLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] [Hệ THỐNG] Chế độ thủ công: Vui lòng sang Terminal nhấn Ctrl + C để tắt AI và nhả Camera.");
        }
    }
}
/*
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace WindowsFormschoRobotcuaG8
{
    public class PythonBridge
    {
        public event Action<string> OnPythonLog;

        private Process _pythonProcess;

        private string ResolvePath(string scriptName)
        {
            // Đường dẫn cứng đến thư mục bin\Debug
            string hardcodedDir = @"D:\du lieu o C\HUST\WindowsFormschoRobotcuaG8 - CopyforPython - Copy\bin\Debug";
            string candidate = Path.Combine(hardcodedDir, scriptName);
            if (File.Exists(candidate)) return candidate;

            // Fallback: thư mục exe
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string candidate2 = Path.Combine(exeDir, scriptName);
            if (File.Exists(candidate2)) return candidate2;

            if (File.Exists(scriptName)) return scriptName;
            return null;
        }
        private string FindPython()
        {
            // Thử các lệnh phổ biến trước
            foreach (string cmd in new[] { "python", "python3", "py" })
            {
                try
                {
                    var test = new ProcessStartInfo
                    {
                        FileName = cmd,
                        Arguments = "--version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    using (var p = Process.Start(test))
                    {
                        p?.WaitForExit(3000);
                        if (p != null && p.ExitCode == 0) return cmd;
                    }
                }
                catch { }
            }

            // Thử tìm trong các đường dẫn cài đặt phổ biến
            string[] commonPaths = {
                @"C:\Python311\python.exe",
                @"C:\Python310\python.exe",
                @"C:\Python39\python.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Programs\Python\Python311\python.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Programs\Python\Python310\python.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Programs\Python\Python39\python.exe"),
            };

            foreach (string path in commonPaths)
                if (File.Exists(path)) return path;

            return null;
        }

        public async Task RunPythonScriptAsync(string scriptName, string arguments)
        {
            await Task.Run(() =>
            {
                StopPython();

                // Tìm file script
                string scriptPath = ResolvePath(scriptName);
                if (scriptPath == null)
                {
                    OnPythonLog?.Invoke(
                        $"[{DateTime.Now:HH:mm:ss}] [PythonBridge] KHÔNG TÌM THẤY: {scriptName}\n" +
                        $"  → Đặt file vào: {AppDomain.CurrentDomain.BaseDirectory}");
                    return;
                }

                // Tìm Python interpreter
                string pythonExe = FindPython();
                if (pythonExe == null)
                {
                    OnPythonLog?.Invoke(
                        $"[{DateTime.Now:HH:mm:ss}] [PythonBridge] KHÔNG TÌM THẤY PYTHON!\n" +
                        $"  → Mở cmd, gõ: where python\n" +
                        $"  → Paste đường dẫn vào PythonBridge.cs dòng FindPython()");
                    return;
                }

                OnPythonLog?.Invoke(
                    $"[{DateTime.Now:HH:mm:ss}] [PythonBridge] Dùng: {pythonExe}\n" +
                    $"  → Script: {scriptPath}");

                var startInfo = new ProcessStartInfo
                {
                    FileName = pythonExe,
                    Arguments = $"\"{scriptPath}\" {arguments}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(scriptPath)
                };

                try
                {
                    _pythonProcess = Process.Start(startInfo);
                    if (_pythonProcess == null)
                    {
                        OnPythonLog?.Invoke(
                            $"[{DateTime.Now:HH:mm:ss}] [PythonBridge] Không thể start tiến trình.");
                        return;
                    }

                    _pythonProcess.OutputDataReceived += (s, e) => {
                        if (!string.IsNullOrEmpty(e.Data))
                            OnPythonLog?.Invoke($"[Python] {e.Data}");
                    };
                    _pythonProcess.ErrorDataReceived += (s, e) => {
                        if (!string.IsNullOrEmpty(e.Data))
                            OnPythonLog?.Invoke($"[Python-ERR] {e.Data}");
                    };
                    _pythonProcess.BeginOutputReadLine();
                    _pythonProcess.BeginErrorReadLine();

                    OnPythonLog?.Invoke(
                        $"[{DateTime.Now:HH:mm:ss}] [PythonBridge] ✓ Đang chạy ngầm (PID={_pythonProcess.Id})");
                }
                catch (Exception ex)
                {
                    OnPythonLog?.Invoke(
                        $"[{DateTime.Now:HH:mm:ss}] [PythonBridge] Lỗi: {ex.Message}");
                }
            });
        }

        public void StopPython()
        {
            try
            {
                if (_pythonProcess != null && !_pythonProcess.HasExited)
                {
                    _pythonProcess.Kill();
                    _pythonProcess.WaitForExit(2000);
                    OnPythonLog?.Invoke(
                        $"[{DateTime.Now:HH:mm:ss}] [PythonBridge] Đã dừng Python.");
                }
            }
            catch { }
            finally { _pythonProcess = null; }
        }
    }
}
*/