﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using RTCV.CorruptCore;
using RTCV.NetCore;
using RTCV.Vanguard;

namespace TestVanguardImplemented
{
	public static class VanguardCore
	{
		public static string[] args;
		

		internal static DialogResult ShowErrorDialog(Exception exception, bool canContinue = false)
		{
			return new RTCV.NetCore.CloudDebug(exception, canContinue).Start();
		}


		/// <summary>
		/// Global exceptions in Non User Interfarce(other thread) antipicated error
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		internal static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Exception ex = (Exception)e.ExceptionObject;
			Form error = new RTCV.NetCore.CloudDebug(ex);
			var result = error.ShowDialog();

		}

		/// <summary>
		/// Global exceptions in User Interfarce antipicated error
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		internal static void ApplicationThreadException(object sender, ThreadExceptionEventArgs e)
		{
			Exception ex = e.Exception;
			Form error = new RTCV.NetCore.CloudDebug(ex);
			var result = error.ShowDialog();

			Form loaderObject = (sender as Form);

			if (result == DialogResult.Abort)
			{
				if (loaderObject != null)
					RTCV.NetCore.SyncObjectSingleton.SyncObjectExecute(loaderObject, (o, ea) =>
					{
						loaderObject.Close();
					});
			}
		}

		public static bool attached = false;

		public static string System
		{
			get => (string)VanguardSpec[VSPEC.SYSTEM.ToString()];
			set => VanguardSpec.Update(VSPEC.SYSTEM.ToString(), value);
		}
		public static string GameName
		{
			get => (string)VanguardSpec[VSPEC.GAMENAME.ToString()];
			set => VanguardSpec.Update(VSPEC.GAMENAME.ToString(), value);
		}
		public static string SystemPrefix
		{
			get => (string)VanguardSpec[VSPEC.SYSTEMPREFIX.ToString()];
			set => VanguardSpec.Update(VSPEC.SYSTEMPREFIX.ToString(), value);
		}
		public static string SystemCore
		{
			get => (string)VanguardSpec[VSPEC.SYSTEMCORE.ToString()];
			set => VanguardSpec.Update(VSPEC.SYSTEMCORE.ToString(), value);
		}
		public static string SyncSettings
		{
			get => (string)VanguardSpec[VSPEC.SYNCSETTINGS.ToString()];
			set => VanguardSpec.Update(VSPEC.SYNCSETTINGS.ToString(), value);
		}
		public static string OpenRomFilename
		{
			get => (string)VanguardSpec[VSPEC.OPENROMFILENAME.ToString()];
			set => VanguardSpec.Update(VSPEC.OPENROMFILENAME.ToString(), value);
		}
		public static int LastLoaderRom
		{
			get => (int)VanguardSpec[VSPEC.CORE_LASTLOADERROM.ToString()];
			set => VanguardSpec.Update(VSPEC.CORE_LASTLOADERROM.ToString(), value);
		}
		public static string[] BlacklistedDomains
		{
			get => (string[])VanguardSpec[VSPEC.MEMORYDOMAINS_BLACKLISTEDDOMAINS.ToString()];
			set => VanguardSpec.Update(VSPEC.MEMORYDOMAINS_BLACKLISTEDDOMAINS.ToString(), value);
		}
		public static MemoryDomainProxy[] MemoryInterfacees
		{
			get => (MemoryDomainProxy[])VanguardSpec[VSPEC.MEMORYDOMAINS_INTERFACES.ToString()];
			set => VanguardSpec.Update(VSPEC.MEMORYDOMAINS_INTERFACES.ToString(), value);
		}

		public static PartialSpec getDefaultPartial()
		{
			var partial = new PartialSpec("RTCSpec");

			partial[VSPEC.SYSTEM.ToString()] = String.Empty;
			partial[VSPEC.GAMENAME.ToString()] = String.Empty;
			partial[VSPEC.SYSTEMPREFIX.ToString()] = String.Empty;
			partial[VSPEC.OPENROMFILENAME.ToString()] = String.Empty;
			partial[VSPEC.SYNCSETTINGS.ToString()] = String.Empty;
			partial[VSPEC.OPENROMFILENAME.ToString()] = String.Empty;
			partial[VSPEC.MEMORYDOMAINS_BLACKLISTEDDOMAINS.ToString()] = new string[] { };
			partial[VSPEC.MEMORYDOMAINS_INTERFACES.ToString()] = new MemoryDomainProxy[] { };
			partial[VSPEC.CORE_LASTLOADERROM.ToString()] = -1;

			return partial;
		}

		public static volatile FullSpec VanguardSpec;


		public static void RegisterEmuhawkSpec()
		{
			PartialSpec emuSpecTemplate = new PartialSpec("VanguardSpec");

			emuSpecTemplate.Insert(VanguardCore.getDefaultPartial());

			VanguardSpec = new FullSpec(emuSpecTemplate, !CorruptCore.Attached); //You have to feed a partial spec as a template

			if (VanguardCore.attached)
				RTCV.Vanguard.VanguardConnector.PushVanguardSpecRef(VanguardCore.VanguardSpec);

			LocalNetCoreRouter.Route(NetcoreCommands.CORRUPTCORE, NetcoreCommands.REMOTE_PUSHVANGUARDSPEC, emuSpecTemplate, true);
			LocalNetCoreRouter.Route(NetcoreCommands.UI, NetcoreCommands.REMOTE_PUSHVANGUARDSPEC, emuSpecTemplate, true);


			VanguardSpec.SpecUpdated += (o, e) =>
			{
				PartialSpec partial = e.partialSpec;

				if(!VanguardCore.attached)
					RTCV.NetCore.AllSpec.VanguardSpec = VanguardSpec;

				LocalNetCoreRouter.Route(NetcoreCommands.CORRUPTCORE, NetcoreCommands.REMOTE_PUSHVANGUARDSPECUPDATE, partial, true);
				LocalNetCoreRouter.Route(NetcoreCommands.UI, NetcoreCommands.REMOTE_PUSHVANGUARDSPECUPDATE, partial, true);
			};
		}

		//This is the entry point of RTC. Without this method, nothing will load.
		public static void Start(RTC_Standalone_Form _standaloneForm = null)
		{
			//Grab an object on the main thread to use for netcore invokes
			SyncObjectSingleton.SyncObject = Program.SyncForm;

			//Start everything
			TestVanguardImplementation.StartClient();
			VanguardCore.RegisterEmuhawkSpec();
			CorruptCore.StartEmuSide();

			//Refocus on Bizhawk
			Hooks.BIZHAWK_MAINFORM_FOCUS();

			//Force create bizhawk config file if it doesn't exist
			if (!File.Exists(CorruptCore.bizhawkDir + Path.DirectorySeparatorChar + "config.ini"))
				Hooks.BIZHAWK_MAINFORM_SAVECONFIG();

			//If it's attached, lie to vanguard
			if (VanguardCore.attached)
				VanguardConnector.ImplyClientConnected();
		}


		public static void StartSound()
		{
			Hooks.BIZHAWK_STARTSOUND();
		}

		public static void StopSound()
		{
			Hooks.BIZHAWK_STOPSOUND();
		}


		public static string EmuFolderCheck(string SystemDisplayName)
		{
			//Workaround for Bizhawk's folder name quirk

			if (SystemDisplayName.Contains("(INTERIM)"))
			{
				char[] delimiters = { '(', ' ', ')' };

				string temp = SystemDisplayName.Split(delimiters)[0];
				SystemDisplayName = temp + "_INTERIM";
			}
			switch (SystemDisplayName)
			{
				case "Playstation":
					return "PSX";
				case "GG":
					return "Game Gear";
				case "Commodore 64":
					return "C64";
				case "SG":
					return "SG-1000";
				default:
					return SystemDisplayName;
			}
		}
		/// <summary>
		/// Loads a NES-based title screen.
		/// Can be overriden by putting a file named "overridedefault.nes" in the ASSETS folder
		/// </summary>
		public static void LoadDefaultRom()
		{
			LoadRom_NET("DEFAULTROM");
		}

		/// <summary>
		/// Loads a rom within Bizhawk. To be called from within Bizhawk only
		/// </summary>
		/// <param name="RomFile"></param>
		public static void LoadRom_NET(string RomFile)
		{
			var loadRomWatch = Stopwatch.StartNew();

			StopSound();

			if (RomFile == null)
				RomFile = Hooks.BIZHAWK_GET_CURRENTLYOPENEDROM(); ;


			//Stop capturing rewind while we load
			Hooks.AllowCaptureRewindState = false;
			Hooks.BIZHAWK_LOADROM(RomFile);
			Hooks.AllowCaptureRewindState = true;

			StartSound();
			loadRomWatch.Stop();
			Console.WriteLine($"Time taken for LoadRom_NET: {0}ms", loadRomWatch.ElapsedMilliseconds);
		}

		/// <summary>
		/// Creates a savestate using a key as the filename and returns the path.
		/// Bizhawk process only.
		/// </summary>
		/// <param name="Key"></param>
		/// <param name="threadSave"></param>
		/// <returns></returns>
		public static string SaveSavestate_NET(string Key, bool threadSave = false)
		{
			//Don't state if we don't have a core
			if (Hooks.BIZHAWK_ISNULLEMULATORCORE())
				return null;

			//Build the shortname
			string quickSlotName = Key + ".timejump";

			//Get the prefix for the state
			string prefix = Hooks.BIZHAWK_GET_SAVESTATEPREFIX();
			prefix = prefix.Substring(prefix.LastIndexOf('\\') + 1);

			//Build up our path
			var path = CorruptCore.workingDir + Path.DirectorySeparatorChar + "SESSION" + Path.DirectorySeparatorChar + prefix + "." + quickSlotName + ".State";

			//If the path doesn't exist, make it
			var file = new FileInfo(path);
			if (file.Directory != null && file.Directory.Exists == false)
				file.Directory.Create();

			//Savestates on a new thread. Doesn't work properly as Bizhawk doesn't support threaded states
			if (threadSave)
			{
				(new Thread(() =>
				{
					try
					{
						Hooks.BIZHAWK_SAVESTATE(path, quickSlotName);
					}
					catch (Exception ex)
					{
						Console.WriteLine("Thread collision ->\n" + ex.ToString());
					}
				})).Start();
			}
			else
				Hooks.BIZHAWK_SAVESTATE(path, quickSlotName); //savestate

			return path;
		}

		/// <summary>
		/// Loads a savestate from a path. 
		/// </summary>
		/// <param name="path">The path of the state</param>
		/// <param name="stateLocation">Where the state is located in a stashkey (used for errors, not required)</param>
		/// <returns></returns>
		public static bool LoadSavestate_NET(string path, StashKeySavestateLocation stateLocation = StashKeySavestateLocation.DEFAULTVALUE)
		{
			try
			{
				//If we don't have a core just exit out
				if (Hooks.BIZHAWK_ISNULLEMULATORCORE())
					return false;

				//If we can't find the file, throw a message
				if (File.Exists(path) == false)
				{
					Hooks.BIZHAWK_OSDMESSAGE("Unable to load " + Path.GetFileName(path) + " from " + stateLocation);
					return false;
				}

				Hooks.BIZHAWK_LOADSTATE(path);

				return true;
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.ToString());
				return false;
			}
		}

		/// <summary>
		/// Loads the window size/position from a param
		/// </summary>
		public static void LoadBizhawkWindowState()
		{
			if (RTCV.NetCore.Params.IsParamSet("BIZHAWK_SIZE"))
			{
				string[] size = RTCV.NetCore.Params.ReadParam("BIZHAWK_SIZE").Split(',');
				Hooks.BIZHAWK_GETSET_MAINFORMSIZE = new Size(Convert.ToInt32(size[0]), Convert.ToInt32(size[1]));
				string[] location = RTCV.NetCore.Params.ReadParam("BIZHAWK_LOCATION").Split(',');
				Hooks.BIZHAWK_GETSET_MAINFORMLOCATION = new Point(Convert.ToInt32(location[0]), Convert.ToInt32(location[1]));
			}
		}
		/// <summary>
		/// Saves the window size/position to a param
		/// </summary>
		public static void SaveBizhawkWindowState()
		{
			var size = Hooks.BIZHAWK_GETSET_MAINFORMSIZE;
			var location = Hooks.BIZHAWK_GETSET_MAINFORMLOCATION;

			RTCV.NetCore.Params.SetParam("BIZHAWK_SIZE", $"{size.Width},{size.Height}");
			RTCV.NetCore.Params.SetParam("BIZHAWK_LOCATION", $"{location.X},{location.Y}");
		}

		/// <summary>
		/// Loads the default rom and shows bizhawk
		/// </summary>
		public static void LoadDefaultAndShowBizhawkForm()
		{

			VanguardCore.LoadDefaultRom();
			VanguardCore.LoadBizhawkWindowState();

		}


		/// <summary>
		/// Returns the list of domains that are blacklisted from being auto-selected
		/// </summary>
		/// <param name="systemName"></param>
		/// <returns></returns>
		public static string[] GetBlacklistedDomains(string systemName)
		{
			// Returns the list of Domains that can't be rewinded and/or are just not good to use

			List<string> domainBlacklist = new List<string>();
			switch (systemName)
			{
				default:
					break;
			}

			return domainBlacklist.ToArray();;
		}

	}
}
