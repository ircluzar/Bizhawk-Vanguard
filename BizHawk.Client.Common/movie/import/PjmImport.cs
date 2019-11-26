﻿using System;
using System.IO;
using BizHawk.Emulation.Cores.Sony.PSX;

namespace BizHawk.Client.Common
{
	[ImporterFor("PSXjin", ".pjm")]
	internal class PjmImport : MovieImporter
	{
		protected override void RunImport()
		{
			Result.Movie.HeaderEntries[HeaderKeys.PLATFORM] = "PSX";

			using var fs = SourceFile.OpenRead();
			using var br = new BinaryReader(fs);
			var info = ParseHeader(Result.Movie, "PJM ", br);

			fs.Seek(info.ControllerDataOffset, SeekOrigin.Begin);

			if (info.BinaryFormat)
			{
				ParseBinaryInputLog(br, Result.Movie, info);
			}
			else
			{
				ParseTextInputLog(br, Result.Movie, info);
			}
		}

		protected MiscHeaderInfo ParseHeader(IMovie movie, string expectedMagic, BinaryReader br)
		{
			var info = new MiscHeaderInfo();

			string magic = new string(br.ReadChars(4));
			if (magic != expectedMagic)
			{
				Result.Errors.Add($"Not a {expectedMagic}file: invalid magic number in file header.");
				return info;
			}

			uint movieVersionNumber = br.ReadUInt32();
			if (movieVersionNumber != 2)
			{
				Result.Warnings.Add($"Unexpected movie version: got {movieVersionNumber}, expecting 2");
			}

			// 008: UInt32 emulator version.
			br.ReadUInt32();

			byte flags = br.ReadByte();
			byte flags2 = br.ReadByte();
			if ((flags & 0x02) != 0)
			{
				Result.Errors.Add("Movie starts from savestate; this is currently unsupported.");
			}

			if ((flags & 0x04) != 0)
			{
				movie.HeaderEntries[HeaderKeys.PAL] = "1";
			}

			if ((flags & 0x08) != 0)
			{
				Result.Errors.Add("Movie contains embedded memory cards; this is currently unsupported.");
			}

			if ((flags & 0x10) != 0)
			{
				Result.Errors.Add("Movie contains embedded cheat list; this is currently unsupported.");
			}

			if ((flags & 0x20) != 0 || (flags2 & 0x06) != 0)
			{
				Result.Errors.Add("Movie relies on emulator hacks; this is currently unsupported.");
			}

			if ((flags & 0x40) != 0)
			{
				info.BinaryFormat = false;
			}

			if ((flags & 0x80) != 0 || (flags2 & 0x01) != 0)
			{
				Result.Errors.Add("Movie uses multitap; this is currently unsupported.");
				return info;
			}

			// Player 1 controller type
			switch (br.ReadByte())
			{
				// It seems to be inconsistent in the files I looked at which of these is used
				// to mean no controller present.
				case 0:
				case 8:
					info.Player1Type = OctoshockDll.ePeripheralType.None;
					break;
				case 4:
					info.Player1Type = OctoshockDll.ePeripheralType.Pad;
					break;
				case 7:
					info.Player1Type = OctoshockDll.ePeripheralType.DualShock;
					break;
				default:
					Result.Errors.Add("Movie has unrecognized controller type for Player 1.");
					return info;
			}

			// Player 2 controller type
			switch (br.ReadByte())
			{
				case 0:
				case 8:
					info.Player2Type = OctoshockDll.ePeripheralType.None;
					break;
				case 4:
					info.Player2Type = OctoshockDll.ePeripheralType.Pad;
					break;
				case 7:
					info.Player2Type = OctoshockDll.ePeripheralType.DualShock;
					break;
				default:
					Result.Errors.Add("Movie has unrecognized controller type for Player 2.");
					return info;
			}

			var syncSettings = new Octoshock.SyncSettings
			{
				FIOConfig =
				{
					Devices8 = new[]
					{
						info.Player1Type,
						OctoshockDll.ePeripheralType.None,
						OctoshockDll.ePeripheralType.None,
						OctoshockDll.ePeripheralType.None,
						info.Player2Type,
						OctoshockDll.ePeripheralType.None,
						OctoshockDll.ePeripheralType.None,
						OctoshockDll.ePeripheralType.None
					}
				}
			};

			movie.SyncSettingsJson = ConfigService.SaveWithType(syncSettings);

			info.FrameCount = br.ReadUInt32();
			uint rerecordCount = br.ReadUInt32();
			movie.HeaderEntries[HeaderKeys.RERECORDS] = rerecordCount.ToString();

			// 018: UInt32 savestateOffset
			// 01C: UInt32 memoryCard1Offset
			// 020: UInt32 memoryCard2Offset
			// 024: UInt32 cheatListOffset

			// 028: UInt32 cdRomIdOffset
			// Source format is just the first up-to-8 alphanumeric characters of the CD label, 
			// so not so useful.
			br.ReadBytes(20);

			info.ControllerDataOffset = br.ReadUInt32();

			uint authorNameLength = br.ReadUInt32();
			char[] authorName = br.ReadChars((int)authorNameLength);

			movie.HeaderEntries[HeaderKeys.AUTHOR] = new string(authorName);

			info.ParseSuccessful = true;
			return info;
		}

		protected void ParseBinaryInputLog(BinaryReader br, IMovie movie, MiscHeaderInfo info)
		{
			var settings = new Octoshock.SyncSettings();
			var controllers = new SimpleController();
			settings.FIOConfig.Devices8 = new[]
			{ 
				info.Player1Type,
				OctoshockDll.ePeripheralType.None, OctoshockDll.ePeripheralType.None, OctoshockDll.ePeripheralType.None,
				info.Player2Type,
				OctoshockDll.ePeripheralType.None, OctoshockDll.ePeripheralType.None, OctoshockDll.ePeripheralType.None
			};
			controllers.Definition = Octoshock.CreateControllerDefinition(settings);

			string[] buttons =
			{
				"Select", "L3", "R3", "Start", "Up", "Right", "Down", "Left",
				"L2", "R2", "L1", "R1", "Triangle", "Circle", "Cross", "Square"
			};

			bool isCdTrayOpen = false;
			int cdNumber = 1;

			for (int frame = 0; frame < info.FrameCount; ++frame)
			{
				if (info.Player1Type != OctoshockDll.ePeripheralType.None)
				{
					ushort controllerState = br.ReadUInt16();

					// As L3 and R3 don't exist on a standard gamepad, handle them separately later.  Unfortunately
					// due to the layout, we handle select separately too first.
					controllers["P1 Select"] = (controllerState & 0x1) != 0;

					for (int button = 3; button < buttons.Length; button++)
					{
						controllers[$"P1 {buttons[button]}"] = ((controllerState >> button) & 0x1) != 0;
						if (((controllerState >> button) & 0x1) != 0 && button > 15)
						{
							continue;
						}
					}

					if (info.Player1Type == OctoshockDll.ePeripheralType.DualShock)
					{
						controllers["P1 L3"] = (controllerState & 0x2) != 0;
						controllers["P1 R3"] = (controllerState & 0x4) != 0;
						var leftX = new Tuple<string, float>("P1 LStick X", br.ReadByte());
						var leftY = new Tuple<string, float>("P1 LStick Y", br.ReadByte());
						var rightX = new Tuple<string, float>("P1 RStick X", br.ReadByte());
						var rightY = new Tuple<string, float>("P1 RStick Y", br.ReadByte());

						controllers.AcceptNewFloats(new[] { leftX, leftY, rightX, rightY });
					}
				}

				if (info.Player2Type != OctoshockDll.ePeripheralType.None)
				{
					ushort controllerState = br.ReadUInt16();
					for (int button = 0; button < buttons.Length; button++)
					{
						controllers[$"P2 {buttons[button]}"] = ((controllerState >> button) & 0x1) != 0;
						if (((controllerState >> button) & 0x1) != 0 && button > 15)
						{
							continue;
						}
					}

					if (info.Player2Type == OctoshockDll.ePeripheralType.DualShock)
					{
						var leftX = new Tuple<string, float>("P2 LStick X", br.ReadByte());
						var leftY = new Tuple<string, float>("P2 LStick Y", br.ReadByte());
						var rightX = new Tuple<string, float>("P2 RStick X", br.ReadByte());
						var rightY = new Tuple<string, float>("P2 RStick Y", br.ReadByte());

						controllers.AcceptNewFloats(new[] { leftX, leftY, rightX, rightY });
					}
				}

				byte controlState = br.ReadByte();
				controllers["Reset"] = (controlState & 0x02) != 0;
				if ((controlState & 0x04) != 0)
				{
					if (isCdTrayOpen)
					{
						controllers["Close"] = true;
						cdNumber++;
					}
					else
					{
						controllers["Open"] = true;
					}

					isCdTrayOpen = !isCdTrayOpen;
				}
				else
				{
					controllers["Close"] = false;
					controllers["Open"] = false;
				}

				Tuple<string, float> discSelect = new Tuple<string, float>("Disc Select", cdNumber);
				controllers.AcceptNewFloats(new[] { discSelect });

				if ((controlState & 0xFC) != 0)
				{
					Result.Warnings.Add($"Ignored toggle hack flag on frame {frame}");
				}

				movie.AppendFrame(controllers);
			}
		}

		protected void ParseTextInputLog(BinaryReader br, IMovie movie, MiscHeaderInfo info)
		{
			Octoshock.SyncSettings settings = new Octoshock.SyncSettings();
			SimpleController controllers = new SimpleController();
			settings.FIOConfig.Devices8 = new[]
			{
				info.Player1Type,
				OctoshockDll.ePeripheralType.None, OctoshockDll.ePeripheralType.None, OctoshockDll.ePeripheralType.None,
				info.Player2Type,
				OctoshockDll.ePeripheralType.None, OctoshockDll.ePeripheralType.None, OctoshockDll.ePeripheralType.None
			};
			controllers.Definition = Octoshock.CreateControllerDefinition(settings);

			string[] buttons =
			{
				"Start", "Up", "Right", "Down", "Left",
				"L2", "R2", "L1", "R1", "Triangle", "Circle", "Cross", "Square"
			};

			bool isCdTrayOpen = false;
			int cdNumber = 1;

			int player1Count = info.Player1Type == OctoshockDll.ePeripheralType.None ? 1 : info.Player1Type == OctoshockDll.ePeripheralType.Pad ? 15 : 31;
			int player2Count = info.Player2Type == OctoshockDll.ePeripheralType.None ? 1 : info.Player2Type == OctoshockDll.ePeripheralType.Pad ? 15 : 31;
			int strCount = player1Count + player2Count + 4; // 2 for control byte and pipe and line feed chars

			for (int frame = 0; frame < info.FrameCount; ++frame)
			{
				var mnemonicStr = new string(br.ReadChars(strCount));

				// Junk whitespace at the end of a file
				if (string.IsNullOrWhiteSpace(mnemonicStr))
				{
					continue;
				}

				// Gross, if not CR LF, this will fail, but will the PSXjin?
				if (!mnemonicStr.EndsWith("|\r\n"))
				{
					Result.Errors.Add("Unable to parse text input, unknown configuration");
				}

				var split = mnemonicStr.Replace("\r\n", "").Split('|');
				var player1Str = split[0];
				var player2Str = split[1];
				var controltStr = split[2];
				if (info.Player1Type != OctoshockDll.ePeripheralType.None)
				{
					// As L3 and R3 don't exist on a standard gamepad, handle them separately later.  Unfortunately
					// due to the layout, we handle select separately too first.
					controllers["P1 Select"] = player1Str[0] != '.';

					if (info.Player1Type == OctoshockDll.ePeripheralType.DualShock)
					{
						controllers["P1 L3"] = player1Str[1] != '.';
						controllers["P1 R3"] = player1Str[2] != '.';
					}

					int offSet = info.Player1Type == OctoshockDll.ePeripheralType.Pad ? 0 : 2;
					for (int button = 1; button < buttons.Length; button++)
					{
						controllers[$"P1 {buttons[button]}"] = player1Str[button + offSet] != '.';
					}

					if (info.Player1Type == OctoshockDll.ePeripheralType.DualShock)
					{
						// The analog controls are encoded as four space-separated numbers with a leading space
						string leftXRaw = player1Str.Substring(14, 4);
						string leftYRaw = player1Str.Substring(18, 4);
						string rightXRaw = player1Str.Substring(22, 4);
						string rightYRaw = player1Str.Substring(26, 4);

						Tuple<string, float> leftX = new Tuple<string, float>("P1 LStick X", float.Parse(leftXRaw));
						Tuple<string, float> leftY = new Tuple<string, float>("P1 LStick Y", float.Parse(leftYRaw));
						Tuple<string, float> rightX = new Tuple<string, float>("P1 RStick X", float.Parse(rightXRaw));
						Tuple<string, float> rightY = new Tuple<string, float>("P1 RStick Y", float.Parse(rightYRaw));

						controllers.AcceptNewFloats(new[] { leftX, leftY, rightX, rightY });
					}
				}

				if (info.Player2Type != OctoshockDll.ePeripheralType.None)
				{
					// As L3 and R3 don't exist on a standard gamepad, handle them separately later.  Unfortunately
					// due to the layout, we handle select separately too first.
					controllers["P2 Select"] = player2Str[0] != '.';

					if (info.Player2Type == OctoshockDll.ePeripheralType.DualShock)
					{
						controllers["P2 L3"] = player2Str[1] != '.';
						controllers["P2 R3"] = player2Str[2] != '.';
					}

					int offSet = info.Player2Type == OctoshockDll.ePeripheralType.Pad ? 0 : 2;
					for (int button = 1; button < buttons.Length; button++)
					{
						controllers[$"P2 {buttons[button]}"] = player2Str[button + offSet] != '.';
					}

					if (info.Player2Type == OctoshockDll.ePeripheralType.DualShock)
					{
						// The analog controls are encoded as four space-separated numbers with a leading space
						string leftXRaw = new string(br.ReadChars(4)).Trim();
						string leftYRaw = new string(br.ReadChars(4)).Trim();
						string rightXRaw = new string(br.ReadChars(4)).Trim();
						string rightYRaw = new string(br.ReadChars(4)).Trim();

						Tuple<string, float> leftX = new Tuple<string, float>("P2 LStick X", float.Parse(leftXRaw));
						Tuple<string, float> leftY = new Tuple<string, float>("P2 LStick Y", float.Parse(leftYRaw));
						Tuple<string, float> rightX = new Tuple<string, float>("P2 RStick X", float.Parse(rightXRaw));
						Tuple<string, float> rightY = new Tuple<string, float>("P2 RStick Y", float.Parse(rightYRaw));

						controllers.AcceptNewFloats(new[] { leftX, leftY, rightX, rightY });
					}
				}

				byte controlState = (byte)controltStr[0];
				controllers["Reset"] = (controlState & 0x02) != 0;
				if ((controlState & 0x04) != 0)
				{
					if (isCdTrayOpen)
					{
						controllers["Close"] = true;
						cdNumber++;
					}
					else
					{
						controllers["Open"] = true;
					}

					isCdTrayOpen = !isCdTrayOpen;
				}
				else
				{
					controllers["Close"] = false;
					controllers["Open"] = false;
				}

				Tuple<string, float> discSelect = new Tuple<string, float>("Disc Select", cdNumber);
				controllers.AcceptNewFloats(new[] { discSelect });

				if ((controlState & 0xFC) != 0)
				{
					Result.Warnings.Add($"Ignored toggle hack flag on frame {frame}");
				}

				movie.AppendFrame(controllers);
			}
		}

		protected class MiscHeaderInfo
		{
			public bool BinaryFormat { get; set; } = true;
			public uint ControllerDataOffset { get; set; }
			public uint FrameCount { get; set; }
			public OctoshockDll.ePeripheralType Player1Type { get; set; }
			public OctoshockDll.ePeripheralType Player2Type { get; set; }
			public bool ParseSuccessful { get; set; }
		}
	}
}
