﻿// Copyright (c) 2016 Eberhard Beilharz
// Copyright (c) 2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using Bugsnag;
using Bugsnag.Payload;
using Palaso.PlatformUtilities;
using StackTrace = System.Diagnostics.StackTrace;

namespace LfMerge.Core.Logging
{
	public class ExceptionLogging : Client
	{
		private readonly string _solutionPath;

		private ExceptionLogging(string apiKey, string executable, string callerFilePath)
			: base(apiKey)
		{
			_solutionPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(callerFilePath), "../.."));
			if (!_solutionPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
				_solutionPath = _solutionPath + Path.DirectorySeparatorChar;

			Setup(executable);
		}

		private static Dictionary<string, string> FindMetadata(string key,
			ICollection<KeyValuePair<string, object>>                 metadata)
		{
			foreach (var kv in metadata)
			{
				if (kv.Key == key)
					return kv.Value as Dictionary<string, string>;
			}

			var dict = new Dictionary<string, string>();
			metadata.Add(new KeyValuePair<string, object>(key, dict));
			return dict;
		}

		private static string GetOSInfo()
		{
			switch (Environment.OSVersion.Platform)
			{
				// Platform is Windows 95, Windows 98, Windows 98 Second Edition,
				// or Windows Me.
				case PlatformID.Win32Windows:
					// Platform is Windows 95, Windows 98, Windows 98 Second Edition,
					// or Windows Me.
					switch (Environment.OSVersion.Version.Minor)
					{
						case 0:
							return "Windows 95";
						case 10:
							return "Windows 98";
						case 90:
							return "Windows Me";
						default:
							return "UNKNOWN";
					}
				case PlatformID.Win32NT:
					return GetWin32NTVersion();
				case PlatformID.Unix:
				case PlatformID.MacOSX:
					return UnixOrMacVersion();
				default:
					return "UNKNOWN";
			}
		}

		/// <summary>
		/// Enum holding the different types of OS
		/// </summary>
		private enum OsType
		{
			Server,
			Desktop,
			Unknown
		}

		/// <summary>
		/// Detects the current operating system version if its Win32 NT
		/// </summary>
		/// <returns>The operation system version</returns>
		private static string GetWin32NTVersion()
		{
			switch (Environment.OSVersion.Version.Major)
			{
				case 3:
					return "Windows NT 3.51";
				case 4:
					return "Windows NT 4.0";
				case 5:
					return Environment.OSVersion.Version.Minor == 0 ? "Windows 2000" : "Windows XP";
				case 6:
					switch (Environment.OSVersion.Version.Minor)
					{
						case 0:
							return "Windows Server 2008";
						case 1:
							switch (GetOsType())
							{
								case OsType.Desktop:
									return "Windows 7";
								case OsType.Server:
									return "Windows Server 2008 R2";
								case OsType.Unknown:
									return "Windows 7 / Server 2008 R2";
							}

							break;
						case 2:
							switch (GetOsType())
							{
								case OsType.Desktop:
									return "Windows 8";
								case OsType.Server:
									return "Windows Server 2012";
								case OsType.Unknown:
									return "Windows 8 / Server 2012";
							}

							break;
						case 3:
							switch (GetOsType())
							{
								case OsType.Desktop:
									return "Windows 8.1";
								case OsType.Server:
									return "Windows Server 2012 R2";
								case OsType.Unknown:
									return "Windows 8 / Server 2012 R2";
							}

							break;
					}

					return "UNKNOWN";
				case 10:
					return "Windows 10";
				default:
					return "UNKNOWN";
			}
		}

		/// <summary>
		/// Determines if the current operating system is the server version
		/// </summary>
		/// <returns>True if the current operating system is the server version, otherwise false</returns>
		private static OsType GetOsType()
		{
			try
			{
				using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem"))
				{
					foreach (var managementObject in searcher.Get())
					{
						// ProductType will be one of:
						// 1: Workstation
						// 2: Domain Controller
						// 3: Server
						var productType = (uint) managementObject.GetPropertyValue("ProductType");
						return productType != 1 ? OsType.Server : OsType.Desktop;
					}
				}
			}
			catch (UnauthorizedAccessException)
			{
				// If we don't have permssions to query the WMI, then indicate we don't know the OS type
				return OsType.Unknown;
			}

			return OsType.Desktop;
		}

		/// <summary>
		/// Determines the OS version if on a UNIX based system
		/// </summary>
		/// <returns></returns>
		private static string UnixOrMacVersion()
		{
			if (RunTerminalCommand("uname") == "Darwin")
			{
				var osName = RunTerminalCommand("sw_vers", "-productName");
				var osVersion = RunTerminalCommand("sw_vers", "-productVersion");
				return osName + " (" + osVersion + ")";
			}

			var distro = RunTerminalCommand("bash", "-c \"[ $(which lsb_release) ] && lsb_release -d -s\"");
			return string.IsNullOrEmpty(distro) ? "UNIX" : distro;
		}

		/// <summary>
		/// Executes a command with arguments, used to send terminal commands in UNIX systems
		/// </summary>
		/// <param name="cmd">The command to send</param>
		/// <param name="args">The arguments to send</param>
		/// <returns>The returned output</returns>
		private static string RunTerminalCommand(string cmd, string args = null)
		{
			var proc = new Process {
				EnableRaisingEvents = false,
				StartInfo = {
					FileName = cmd,
					Arguments = args,
					UseShellExecute = false,
					RedirectStandardOutput = true
				}
			};
			proc.Start();
			proc.WaitForExit();
			var output = proc.StandardOutput.ReadToEnd();
			return output.Trim();
		}

		private void Setup(string executable)
		{
			BeforeNotify(OnBeforeNotify);

			var configuration = Configuration as Configuration;

			configuration.ProjectNamespaces = new[] { "SIL", "LfMerge" };
			configuration.ProjectRoots = new[] { _solutionPath };
			configuration.AutoCaptureSessions = true;
			configuration.AutoNotify = true;
			configuration.AppType = executable;

			if (string.IsNullOrEmpty(Configuration.ReleaseStage))
			{
				var isDevelopment = Debugger.IsAttached;
#if DEBUG
				isDevelopment = true;
#endif
				if (isDevelopment)
					configuration.ReleaseStage = "development";
			}

			var metadata = new List<KeyValuePair<string, object>>();
			if (configuration.GlobalMetadata != null)
				metadata.AddRange(configuration.GlobalMetadata);

			var app = FindMetadata("App", metadata);
			app.Add("executable", executable);
			app.Add("runtime", Platform.IsMono ? "Mono" : ".NET");
			app.Add("appArchitecture", Environment.Is64BitProcess ? "64 bit" : "32 bit");
			app.Add("clrVersion", Environment.Version.ToString());
			var entryAssembly = Assembly.GetEntryAssembly();
			if (entryAssembly != null)
			{
				if (string.IsNullOrEmpty(Configuration.AppVersion))
					configuration.AppVersion = entryAssembly.GetName().Version.ToString();

				var informationalVersion = entryAssembly
					.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), true)
					.FirstOrDefault() as AssemblyInformationalVersionAttribute;
				if (informationalVersion != null)
					app.Add("infoVersion", informationalVersion.InformationalVersion);
			}

			var device = FindMetadata("Device", metadata);
			device.Add("osVersion", GetOSInfo());
			if (!string.IsNullOrEmpty(Environment.OSVersion.ServicePack))
				device.Add("servicePack", Environment.OSVersion.ServicePack);
			device.Add("osArchitecture", Environment.Is64BitOperatingSystem ? "64 bit" : "32 bit");
			device.Add("processorCount", Environment.ProcessorCount + " core(s)");
			device.Add("machineName", Environment.MachineName);
			device.Add("hostName", Dns.GetHostName());

			configuration.GlobalMetadata = metadata.ToArray();

			// The BaseClient class deals with catching unhandled exceptions
		}

		public void AddInfo(string projectCode, string modelVersion)
		{
			var configuration = Configuration as Configuration;
			var metadata = new List<KeyValuePair<string, object>>();
			if (configuration.GlobalMetadata != null)
				metadata.AddRange(configuration.GlobalMetadata);
			var app = FindMetadata("App", metadata);
			app.Add("Project", projectCode);
			app.Add("ModelVersion", modelVersion);
			configuration.GlobalMetadata = metadata.ToArray();
		}

		private string RemoveFileNamePrefix(string fileName)
		{
			var ret = fileName.StartsWith(_solutionPath)
				? fileName.Substring(_solutionPath.Length)
				: fileName;
			return ret.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		}

		private void OnBeforeNotify(Report report)
		{
			var exception = report.Event.Exceptions[0].OriginalException;
			var stackTrace = new StackTrace(exception, true);
			if (stackTrace.FrameCount <= 0)
				return;

			var frame = stackTrace.GetFrame(0);
//			// During development the line number probably changes frequently, but we want
//			// to treat all errors with the same exception in the same method as being the
//			// same, even when the line numbers differ, so we set it to 0. For releases
//			// we can assume the line number to be constant for a released build.
//			var linenumber = Configuration.ReleaseStage == "development" ? 0 : frame.GetFileLineNumber();
			var linenumber = frame.GetFileLineNumber();
			report.Event.GroupingHash =
				string.Format("{0} {1} {2} {3}", report.Event.Exceptions[0].OriginalException.GetType().Name,
					RemoveFileNamePrefix(frame.GetFileName()), frame.GetMethod().Name, linenumber);
		}

		public static void Initialize(string apiKey, string executable,
			[CallerFilePath] string filename = null)
		{
			Client = new ExceptionLogging(apiKey, executable, filename);
		}

		public static ExceptionLogging Client { get; private set; }
	}
}