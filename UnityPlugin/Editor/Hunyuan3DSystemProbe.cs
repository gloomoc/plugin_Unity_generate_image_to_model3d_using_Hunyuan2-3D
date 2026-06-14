using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Hunyuan3D.Editor
{
    internal sealed class Hunyuan3DEnvironmentSnapshot
    {
        public Hunyuan3DEnvironmentSnapshot()
        {
            System = new Hunyuan3DSystemInfo();
            Python = new Hunyuan3DPythonInfo();
            Cuda = new Hunyuan3DCudaInfo();
            Git = new Hunyuan3DGitInfo();
        }

        public Hunyuan3DSystemInfo System { get; set; }
        public Hunyuan3DPythonInfo Python { get; set; }
        public Hunyuan3DCudaInfo Cuda { get; set; }
        public Hunyuan3DGitInfo Git { get; set; }
    }

    internal sealed class Hunyuan3DSystemInfo
    {
        public Hunyuan3DSystemInfo()
        {
            DisplayName = "Unknown";
            Architecture = "Unknown";
        }

        public string DisplayName { get; set; }
        public string Architecture { get; set; }
        public bool IsWindows { get; set; }
        public bool IsMacOS { get; set; }
        public bool IsLinux { get; set; }
        public bool Is64Bit { get; set; }
    }

    internal sealed class Hunyuan3DPythonInfo
    {
        public Hunyuan3DPythonInfo()
        {
            CheckedLocations = new List<string>();
        }

        public bool IsDetected { get; set; }
        public bool IsSupported { get; set; }
        public string ExecutablePath { get; set; }
        public string VersionText { get; set; }
        public int Major { get; set; }
        public int Minor { get; set; }
        public string Source { get; set; }
        public string DownloadUrl { get; set; }
        public string Summary { get; set; }
        public List<string> CheckedLocations { get; private set; }
    }

    internal sealed class Hunyuan3DCudaInfo
    {
        public bool DriverDetected { get; set; }
        public bool ToolkitDetected { get; set; }
        public bool NvccDetected { get; set; }
        public string DriverCudaVersion { get; set; }
        public string ToolkitVersion { get; set; }
        public string ToolkitRoot { get; set; }
        public string DownloadUrl { get; set; }
        public string Summary { get; set; }
        public string GpuSummary { get; set; }
    }

    internal sealed class Hunyuan3DGitInfo
    {
        public bool IsDetected { get; set; }
        public string ExecutablePath { get; set; }
        public string VersionText { get; set; }
        public string Source { get; set; }
        public string DownloadUrl { get; set; }
        public string Summary { get; set; }
    }

    internal static class Hunyuan3DSystemProbe
    {
        private const int MinimumPythonMajor = 3;
        private const int MinimumPythonMinor = 8;
        private const int MaximumPythonMinor = 12;

        private sealed class PythonCandidate
        {
            public string Value { get; set; }
            public string Source { get; set; }
            public int Priority { get; set; }
        }

        private sealed class CommandResult
        {
            public int ExitCode { get; set; }
            public string Output { get; set; }
            public bool TimedOut { get; set; }

            public bool Succeeded
            {
                get
                {
                    return !TimedOut && ExitCode == 0 && !string.IsNullOrWhiteSpace(Output) &&
                           Output.IndexOf("ERROR:", StringComparison.OrdinalIgnoreCase) < 0;
                }
            }
        }

        public static Hunyuan3DEnvironmentSnapshot Probe(string configuredPythonPath, string preferredVirtualEnvironmentPath)
        {
            var system = ProbeSystem();
            return new Hunyuan3DEnvironmentSnapshot
            {
                System = system,
                Python = ProbePython(system, configuredPythonPath, preferredVirtualEnvironmentPath),
                Cuda = ProbeCuda(system),
                Git = ProbeGit(system)
            };
        }

        public static Hunyuan3DSystemInfo ProbeSystem()
        {
            return new Hunyuan3DSystemInfo
            {
                DisplayName = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.OSArchitecture.ToString(),
                IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
                IsMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX),
                IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
                Is64Bit = Environment.Is64BitOperatingSystem
            };
        }

        public static string GetVirtualEnvironmentPythonPath(string environmentPath)
        {
            if (string.IsNullOrWhiteSpace(environmentPath))
            {
                return null;
            }

            var candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new[]
                {
                    Path.Combine(environmentPath, "Scripts", "python.exe")
                }
                : new[]
                {
                    Path.Combine(environmentPath, "bin", "python3"),
                    Path.Combine(environmentPath, "bin", "python")
                };

            return candidates.FirstOrDefault(File.Exists);
        }

        public static string GetVirtualEnvironmentPipPath(string environmentPath)
        {
            if (string.IsNullOrWhiteSpace(environmentPath))
            {
                return null;
            }

            var candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new[]
                {
                    Path.Combine(environmentPath, "Scripts", "pip.exe")
                }
                : new[]
                {
                    Path.Combine(environmentPath, "bin", "pip3"),
                    Path.Combine(environmentPath, "bin", "pip")
                };

            return candidates.FirstOrDefault(File.Exists);
        }

        public static bool TryExtractVirtualEnvironment(string pythonExecutablePath, out string executablesDirectory, out string environmentPath)
        {
            executablesDirectory = null;
            environmentPath = null;

            if (string.IsNullOrWhiteSpace(pythonExecutablePath) || !File.Exists(pythonExecutablePath))
            {
                return false;
            }

            string directory = Path.GetDirectoryName(pythonExecutablePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return false;
            }

            string directoryName = new DirectoryInfo(directory).Name;
            if (!string.Equals(directoryName, "Scripts", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(directoryName, "bin", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            DirectoryInfo parentDirectory = Directory.GetParent(directory);
            string parent = parentDirectory != null ? parentDirectory.FullName : null;
            if (string.IsNullOrWhiteSpace(parent))
            {
                return false;
            }

            executablesDirectory = directory;
            environmentPath = parent;
            return true;
        }

        private static Hunyuan3DPythonInfo ProbePython(Hunyuan3DSystemInfo system, string configuredPythonPath, string preferredVirtualEnvironmentPath)
        {
            var result = new Hunyuan3DPythonInfo
            {
                DownloadUrl = GetPythonDownloadUrl(system)
            };

            var candidates = BuildPythonCandidates(system, configuredPythonPath, preferredVirtualEnvironmentPath);
            Hunyuan3DPythonInfo firstDetected = null;

            foreach (var candidate in candidates)
            {
                result.CheckedLocations.Add(candidate.Value);

                Hunyuan3DPythonInfo probe = ProbePythonCandidate(candidate);
                if (probe == null)
                {
                    continue;
                }

                probe.DownloadUrl = result.DownloadUrl;

                if (probe.IsSupported)
                {
                    return probe;
                }

                if (firstDetected == null)
                {
                    firstDetected = probe;
                }
            }

            if (firstDetected != null)
            {
                return firstDetected;
            }

            result.Summary = "Compatible Python 3.8 to 3.12 was not detected on this machine.";
            return result;
        }

        private static List<PythonCandidate> BuildPythonCandidates(Hunyuan3DSystemInfo system, string configuredPythonPath, string preferredVirtualEnvironmentPath)
        {
            var candidates = new List<PythonCandidate>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string venvPython = GetVirtualEnvironmentPythonPath(preferredVirtualEnvironmentPath);
            TryAddPythonCandidate(candidates, seen, venvPython, "Local virtual environment", 200);

            if (IsDefaultPythonAlias(configuredPythonPath))
            {
                TryAddPythonCandidate(candidates, seen, configuredPythonPath, "Configured Python command", 145);
            }
            else
            {
                TryAddPythonCandidate(candidates, seen, configuredPythonPath, "Configured Python path", 180);
            }

            foreach (string path in GetPythonLauncherPaths(system))
            {
                TryAddPythonCandidate(candidates, seen, path, "Python launcher", 170);
            }

            foreach (string path in GetRegistryPythonPaths(system))
            {
                TryAddPythonCandidate(candidates, seen, path, "Windows registry", 160);
            }

            foreach (string path in GetPathPythonCandidates(system))
            {
                TryAddPythonCandidate(candidates, seen, path, "System PATH", 150);
            }

            foreach (string path in GetCommonInstallCandidates(system))
            {
                TryAddPythonCandidate(candidates, seen, path, "Common installation folder", 140);
            }

            return candidates
                .OrderByDescending(candidate => candidate.Priority)
                .ThenByDescending(candidate => GetPythonPreferenceScore(candidate.Value))
                .ToList();
        }

        private static void TryAddPythonCandidate(List<PythonCandidate> candidates, HashSet<string> seen, string value, string source, int priority)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string normalized = value.Trim();
            if (seen.Contains(normalized))
            {
                return;
            }

            seen.Add(normalized);
            candidates.Add(new PythonCandidate
            {
                Value = normalized,
                Source = source,
                Priority = priority
            });
        }

        private static bool IsDefaultPythonAlias(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            string normalized = value.Trim().Replace("/", "\\");
            return normalized.Equals("python", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("python.exe", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("python3", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("python3.exe", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWindowsStoreAlias(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !Path.IsPathRooted(value))
            {
                return false;
            }

            string fileName = Path.GetFileName(value);
            if (!fileName.Equals("python.exe", StringComparison.OrdinalIgnoreCase) &&
                !fileName.Equals("python3.exe", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string directory = Path.GetDirectoryName(value);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return false;
            }

            string normalizedDirectory = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return normalizedDirectory.EndsWith(@"\Microsoft\WindowsApps", StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> GetPythonLauncherPaths(Hunyuan3DSystemInfo system)
        {
            if (!system.IsWindows)
            {
                return Enumerable.Empty<string>();
            }

            var output = RunCommand("py", "-0p", 3000);
            if (!output.Succeeded)
            {
                return Enumerable.Empty<string>();
            }

            return output.Output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line =>
                {
                    Match match = Regex.Match(line, @"([A-Za-z]:\\.*python\.exe)", RegexOptions.IgnoreCase);
                    return match.Success ? match.Groups[1].Value.Trim() : null;
                })
                .Where(path => !string.IsNullOrWhiteSpace(path));
        }

        private static IEnumerable<string> GetRegistryPythonPaths(Hunyuan3DSystemInfo system)
        {
            if (!system.IsWindows)
            {
                return Enumerable.Empty<string>();
            }

            var results = new List<string>();
            string[] registryRoots =
            {
                @"SOFTWARE\Python\PythonCore",
                @"SOFTWARE\WOW6432Node\Python\PythonCore"
            };

            foreach (Microsoft.Win32.RegistryKey root in new[]
            {
                Microsoft.Win32.Registry.CurrentUser,
                Microsoft.Win32.Registry.LocalMachine
            })
            {
                foreach (string registryRoot in registryRoots)
                {
                    using (Microsoft.Win32.RegistryKey pythonRoot = root.OpenSubKey(registryRoot))
                    {
                        if (pythonRoot == null)
                        {
                            continue;
                        }

                        foreach (string versionKey in pythonRoot.GetSubKeyNames())
                        {
                            using (Microsoft.Win32.RegistryKey installPathKey = pythonRoot.OpenSubKey(versionKey + "\\InstallPath"))
                            {
                                string installPath = installPathKey != null ? installPathKey.GetValue(null) as string : null;
                                if (string.IsNullOrWhiteSpace(installPath))
                                {
                                    continue;
                                }

                                string pythonExe = Path.Combine(installPath, "python.exe");
                                if (File.Exists(pythonExe))
                                {
                                    results.Add(pythonExe);
                                }
                            }
                        }
                    }
                }
            }

            return results;
        }

        private static IEnumerable<string> GetPathPythonCandidates(Hunyuan3DSystemInfo system)
        {
            var candidates = new List<string>();
            foreach (string command in system.IsWindows ? new[] { "python", "python3" } : new[] { "python3", "python" })
            {
                foreach (string location in GetCommandLocations(command, system))
                {
                    candidates.Add(location);
                }

                if (!system.IsWindows)
                {
                    candidates.Add(command);
                }
            }

            return candidates;
        }

        private static IEnumerable<string> GetCommandLocations(string command, Hunyuan3DSystemInfo system)
        {
            CommandResult commandResult = system.IsWindows
                ? RunCommand("where.exe", command, 2500)
                : RunCommand("which", command, 2500);

            if (!commandResult.Succeeded)
            {
                return Enumerable.Empty<string>();
            }

            return commandResult.Output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line));
        }

        private static IEnumerable<string> GetCommonInstallCandidates(Hunyuan3DSystemInfo system)
        {
            if (system.IsWindows)
            {
                string localPrograms = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs",
                    "Python"
                );

                var versionFolders = new[] { "Python312", "Python311", "Python310", "Python39", "Python38" };
                var candidates = new List<string>();

                foreach (string folder in versionFolders)
                {
                    candidates.Add(Path.Combine(@"C:\", folder, "python.exe"));
                    candidates.Add(Path.Combine(localPrograms, folder, "python.exe"));
                }

                return candidates;
            }

            if (system.IsMacOS)
            {
                return new[]
                {
                    "/opt/homebrew/bin/python3",
                    "/usr/local/bin/python3",
                    "/usr/bin/python3"
                };
            }

            return new[]
            {
                "/usr/local/bin/python3",
                "/usr/bin/python3",
                "/bin/python3"
            };
        }

        private static int GetPythonPreferenceScore(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            string normalized = value.Replace("\\", "/");
            if (normalized.IndexOf("python311", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("3.11", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 40;
            }

            if (normalized.IndexOf("python310", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("3.10", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 35;
            }

            if (normalized.IndexOf("python312", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("3.12", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 30;
            }

            if (normalized.IndexOf("python39", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("3.9", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 20;
            }

            if (normalized.IndexOf("python38", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("3.8", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 10;
            }

            return 0;
        }

        private static Hunyuan3DPythonInfo ProbePythonCandidate(PythonCandidate candidate)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.Value))
            {
                return null;
            }

            if (IsWindowsStoreAlias(candidate.Value))
            {
                return null;
            }

            if (Path.IsPathRooted(candidate.Value) && !File.Exists(candidate.Value))
            {
                return null;
            }

            var commandResult = RunCommand(
                candidate.Value,
                "-c \"import platform,sys; print(sys.executable); print(platform.python_version())\"",
                4000
            );

            if (!commandResult.Succeeded)
            {
                return null;
            }

            string[] lines = commandResult.Output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .ToArray();

            string executablePath = lines.FirstOrDefault(line => line.IndexOf("python", StringComparison.OrdinalIgnoreCase) >= 0);
            string versionText = lines.FirstOrDefault(line => Regex.IsMatch(line, @"^\d+\.\d+(\.\d+)?$"));

            if (string.IsNullOrWhiteSpace(executablePath))
            {
                executablePath = Path.IsPathRooted(candidate.Value) ? candidate.Value : candidate.Value.Trim();
            }

            if (string.IsNullOrWhiteSpace(versionText))
            {
                Match versionMatch = Regex.Match(commandResult.Output, @"(\d+)\.(\d+)(\.\d+)?");
                if (versionMatch.Success)
                {
                    versionText = versionMatch.Value;
                }
            }

            if (string.IsNullOrWhiteSpace(versionText))
            {
                return null;
            }

            Match match = Regex.Match(versionText, @"^(?<major>\d+)\.(?<minor>\d+)");
            if (!match.Success)
            {
                return null;
            }

            int major = int.Parse(match.Groups["major"].Value);
            int minor = int.Parse(match.Groups["minor"].Value);
            bool isSupported = major == MinimumPythonMajor &&
                               minor >= MinimumPythonMinor &&
                               minor <= MaximumPythonMinor;

            return new Hunyuan3DPythonInfo
            {
                IsDetected = true,
                IsSupported = isSupported,
                ExecutablePath = executablePath,
                VersionText = "Python " + versionText,
                Major = major,
                Minor = minor,
                Source = candidate.Source,
                Summary = isSupported
                    ? "Compatible Python detected."
                    : "Python detected, but this plugin expects Python 3.8 to 3.12."
            };
        }

        private static string GetPythonDownloadUrl(Hunyuan3DSystemInfo system)
        {
            if (system.IsWindows)
            {
                return "https://www.python.org/downloads/windows/";
            }

            if (system.IsMacOS)
            {
                return "https://www.python.org/downloads/macos/";
            }

            return "https://www.python.org/downloads/source/";
        }

        private static Hunyuan3DCudaInfo ProbeCuda(Hunyuan3DSystemInfo system)
        {
            var result = new Hunyuan3DCudaInfo
            {
                DownloadUrl = "https://developer.nvidia.com/cuda-downloads"
            };

            CommandResult smiOutput = RunCommand("nvidia-smi", "", 4000);
            if (smiOutput.Succeeded)
            {
                result.DriverDetected = true;
                result.DriverCudaVersion = ExtractVersion(smiOutput.Output, @"CUDA Version:\s*(\d+\.\d+)");
            }

            CommandResult gpuOutput = RunCommand("nvidia-smi", "--query-gpu=name,driver_version --format=csv,noheader", 4000);
            if (gpuOutput.Succeeded)
            {
                result.DriverDetected = true;
                result.GpuSummary = gpuOutput.Output
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault();
            }

            CommandResult nvccOutput = RunCommand("nvcc", "--version", 4000);
            if (nvccOutput.Succeeded)
            {
                result.NvccDetected = true;
                result.ToolkitDetected = true;
                result.ToolkitVersion = ExtractVersion(nvccOutput.Output, @"release\s+(\d+\.\d+)");
            }

            foreach (string root in GetCudaToolkitRoots(system))
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                {
                    continue;
                }

                result.ToolkitDetected = true;
                result.ToolkitRoot = root;

                if (string.IsNullOrWhiteSpace(result.ToolkitVersion))
                {
                    result.ToolkitVersion = ExtractVersion(root, @"v(\d+\.\d+)");
                }

                if (string.IsNullOrWhiteSpace(result.ToolkitVersion))
                {
                    string versionFile = Path.Combine(root, "version.txt");
                    if (File.Exists(versionFile))
                    {
                        string versionText = File.ReadAllText(versionFile);
                        result.ToolkitVersion = ExtractVersion(versionText, @"CUDA Version\s+(\d+\.\d+)");
                    }
                }

                break;
            }

            if (result.DriverDetected && result.ToolkitDetected)
            {
                result.Summary = "NVIDIA GPU and CUDA Toolkit detected.";
            }
            else if (result.DriverDetected)
            {
                result.Summary = "NVIDIA GPU detected, but the CUDA Toolkit is missing.";
            }
            else
            {
                result.Summary = "NVIDIA driver was not detected. CPU mode will still work.";
            }

            return result;
        }

        private static Hunyuan3DGitInfo ProbeGit(Hunyuan3DSystemInfo system)
        {
            var result = new Hunyuan3DGitInfo
            {
                DownloadUrl = "https://git-scm.com/download/win"
            };

            List<string> candidates = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string path in GetCommandLocations("git", system))
            {
                TryAddStringCandidate(candidates, seen, path);
            }

            foreach (string path in GetCommonGitCandidates(system))
            {
                TryAddStringCandidate(candidates, seen, path);
            }

            foreach (string candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                if (Path.IsPathRooted(candidate) && !File.Exists(candidate))
                {
                    continue;
                }

                CommandResult versionOutput = RunCommand(candidate, "--version", 4000);
                if (!versionOutput.Succeeded || versionOutput.Output.IndexOf("git version", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                result.IsDetected = true;
                result.ExecutablePath = Path.IsPathRooted(candidate) ? candidate : ResolveGitFromWhere(system, candidate);
                result.VersionText = versionOutput.Output
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault();
                result.Source = Path.IsPathRooted(candidate) ? "Detected installation path" : "System PATH";
                result.Summary = "Git detected and ready for repository installs.";
                return result;
            }

            result.Summary = "Git was not detected. Repository installs will fail until Git is available to Unity.";
            return result;
        }

        private static string ResolveGitFromWhere(Hunyuan3DSystemInfo system, string fallbackCommand)
        {
            foreach (string path in GetCommandLocations("git", system))
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    return path;
                }
            }

            return fallbackCommand;
        }

        private static IEnumerable<string> GetCommonGitCandidates(Hunyuan3DSystemInfo system)
        {
            if (system.IsWindows)
            {
                return new[]
                {
                    @"C:\Program Files\Git\cmd\git.exe",
                    @"C:\Program Files\Git\bin\git.exe",
                    @"C:\Program Files (x86)\Git\cmd\git.exe",
                    @"C:\Program Files (x86)\Git\bin\git.exe",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Git", "cmd", "git.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Git", "bin", "git.exe")
                };
            }

            if (system.IsMacOS)
            {
                return new[]
                {
                    "/usr/bin/git",
                    "/opt/homebrew/bin/git",
                    "/usr/local/bin/git"
                };
            }

            return new[]
            {
                "/usr/bin/git",
                "/usr/local/bin/git",
                "/bin/git"
            };
        }

        private static IEnumerable<string> GetCudaToolkitRoots(Hunyuan3DSystemInfo system)
        {
            var roots = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                string key = entry.Key as string;
                string value = entry.Value as string;

                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (key.Equals("CUDA_HOME", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("CUDA_PATH", StringComparison.OrdinalIgnoreCase) ||
                    key.StartsWith("CUDA_PATH_V", StringComparison.OrdinalIgnoreCase))
                {
                    TryAddCudaRoot(roots, seen, value);
                }
            }

            foreach (string nvccLocation in GetCommandLocations("nvcc", system))
            {
                try
                {
                    string binDirectory = Path.GetDirectoryName(nvccLocation);
                    string rootDirectory = Directory.GetParent(binDirectory) != null ? Directory.GetParent(binDirectory).FullName : null;
                    TryAddCudaRoot(roots, seen, rootDirectory);
                }
                catch
                {
                }
            }

            if (system.IsWindows)
            {
                foreach (string baseDirectory in new[]
                {
                    @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA",
                    @"C:\Program Files\NVIDIA Corporation\CUDA"
                })
                {
                    if (!Directory.Exists(baseDirectory))
                    {
                        continue;
                    }

                    foreach (string versionDirectory in Directory.GetDirectories(baseDirectory, "v*")
                        .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase))
                    {
                        TryAddCudaRoot(roots, seen, versionDirectory);
                    }
                }
            }
            else
            {
                foreach (string root in new[]
                {
                    "/usr/local/cuda",
                    "/opt/cuda"
                })
                {
                    TryAddCudaRoot(roots, seen, root);
                }
            }

            return roots;
        }

        private static void TryAddCudaRoot(List<string> roots, HashSet<string> seen, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string normalized = path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (seen.Contains(normalized))
            {
                return;
            }

            seen.Add(normalized);
            roots.Add(normalized);
        }

        private static void TryAddStringCandidate(List<string> values, HashSet<string> seen, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string normalized = path.Trim();
            if (seen.Contains(normalized))
            {
                return;
            }

            seen.Add(normalized);
            values.Add(normalized);
        }

        private static string ExtractVersion(string text, string pattern)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            Match match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static CommandResult RunCommand(string fileName, string arguments, int timeoutMs)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return new CommandResult
                        {
                            ExitCode = -1,
                            Output = "ERROR: Could not start process."
                        };
                    }

                    bool exited = process.WaitForExit(timeoutMs);
                    if (!exited)
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                        }

                        return new CommandResult
                        {
                            ExitCode = -1,
                            TimedOut = true,
                            Output = "ERROR: Command timed out."
                        };
                    }

                    string output = (process.StandardOutput.ReadToEnd() + Environment.NewLine + process.StandardError.ReadToEnd()).Trim();
                    return new CommandResult
                    {
                        ExitCode = process.ExitCode,
                        Output = output
                    };
                }
            }
            catch (Exception ex)
            {
                return new CommandResult
                {
                    ExitCode = -1,
                    Output = "ERROR: " + ex.Message
                };
            }
        }
    }
}
