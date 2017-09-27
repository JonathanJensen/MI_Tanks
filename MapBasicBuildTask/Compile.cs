/*****************************************************************************
*       Copyright © 2016 Pitney Bowes Software Inc.
*       All rights reserved.
*****************************************************************************/
using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Win32;

namespace MapBasicBuild
{
	public class CompileMb : Microsoft.Build.Utilities.Task
	{
		public string MapBasicExe { get; set; }
		public string MapBasicArguments { get; set; }
		[Required]
		public string OutputFolder { get; set; }
		public string IntermediateFolder { get; set; }

		[Required]
		public ITaskItem[] SourceFiles { get; set; }

		public CompileMb()
		{
		}

		void FindMapBasic()
		{
			if (string.IsNullOrWhiteSpace(MapBasicExe) || !File.Exists(MapBasicExe))
			{
				MapBasicExe = Environment.GetEnvironmentVariable("MAPBASICEXE"); // allow for local env to override registry
				if (File.Exists(MapBasicExe))
				{
					return;
				}

				var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\mapbasic.exe")?.GetValue(null);
				if (key != null)
				{
					MapBasicExe = key.ToString();
				}
				if (!File.Exists(MapBasicExe))
				{
					MapBasicExe = @"C:\Program Files\MapInfo\MapBasic\mapbasic.exe";
				}
			}
		}

		//TODO: see if there is a project file and if so compile mb files into mbo then link project
		public override bool Execute()
		{
			bool errors = false;

			FindMapBasic();

			if (string.IsNullOrWhiteSpace(MapBasicExe) || !File.Exists(MapBasicExe))
			{
				Log.LogError("MapBasicExe not specified or not found. Path='{0}'", MapBasicExe);
				return false;
			}
			else
			{
				Log.LogMessage(MessageImportance.Normal, string.Format("MapBasicExe='{0}'", MapBasicExe));
			}

			foreach (var item in SourceFiles)
			{
				Log.LogMessage(MessageImportance.Normal, string.Format("compiling {0} to {1}", item.ItemSpec, OutputFolder));

				if (item.ItemSpec == null) continue;

				var startInfo = new System.Diagnostics.ProcessStartInfo
				{
					CreateNoWindow = true,
					UseShellExecute = false,
					FileName = MapBasicExe,
					//WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
					Arguments = MapBasicArguments + "-NOSPLASH -server -nodde -d " + Path.GetFileName(item.ItemSpec),
					WorkingDirectory = Path.GetDirectoryName(item.ItemSpec)
				};

				try
				{
					// Start the process with the info we specified.
					// Call WaitForExit and then the using statement will close.
					using (System.Diagnostics.Process exeProcess = System.Diagnostics.Process.Start(startInfo))
					{
						if (exeProcess != null) exeProcess.WaitForExit();
					}

					// move file to output dir
					var srcmbx = Path.Combine(Path.GetDirectoryName(item.ItemSpec), Path.GetFileNameWithoutExtension(item.ItemSpec) + ".mbx");
					var destmbx = Path.Combine(OutputFolder, Path.GetFileNameWithoutExtension(item.ItemSpec) + ".mbx");

					if (ProcessErrors(item.ItemSpec))
					{
						if (File.Exists(destmbx))
						{
							File.Delete(destmbx);
						}
						;
						File.Move(srcmbx, destmbx);
					}
					else
					{
						errors = true;
					}
				}
				catch (Exception)
				{
					throw;
				}
			}
			return !errors;
		}

		bool ProcessErrors(string file)
		{
			// Put all err file names in current directory.
			string[] errFiles = Directory.GetFiles(@".\", @"*.err");
			bool errors = false;
			foreach (var errFile in errFiles)
			{
				using (StreamReader r = new StreamReader(errFile))
				{
					string errLine;
					while ((errLine = r.ReadLine()) != null)
					{
						var line = 0;
						// sample error:  (prospy.mb:72) Found: [End ] while searching for [End Program], [End MapInfo], or [End function]. 
						// TODO: Anshul would do this with a regex
						var n = errLine.IndexOf(':');
						if (n != -1)
						{
							int end = errLine.IndexOf(')');
							int.TryParse(errLine.Substring(n + 1, end - n -1), out line);
						}
						Log.LogError(null, null, null, file, line, 0, 0, 0, errLine, null);
						errors = true;
					}
				}
			}
			return !errors;
		}

	}
}
