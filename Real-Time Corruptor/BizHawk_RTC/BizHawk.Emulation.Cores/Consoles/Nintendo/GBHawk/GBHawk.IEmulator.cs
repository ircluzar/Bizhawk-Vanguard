﻿using BizHawk.Common.NumberExtensions;
using BizHawk.Emulation.Common;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BizHawk.Emulation.Cores.Nintendo.GBHawk
{
	public partial class GBHawk : IEmulator, IVideoProvider
	{
		public IEmulatorServiceProvider ServiceProvider { get; }

		public ControllerDefinition ControllerDefinition => _controllerDeck.Definition;

		public byte controller_state;
		public ushort Acc_X_state;
		public ushort Acc_Y_state;
		public bool in_vblank_old;
		public bool in_vblank;
		public bool vblank_rise;

		public bool FrameAdvance(IController controller, bool render, bool rendersound)
		{
			//Console.WriteLine("-----------------------FRAME-----------------------");

			//Update the color palette if a setting changed
			if(_settings.Palette == GBSettings.PaletteType.BW)
			{
				color_palette[0] = color_palette_BW[0];
				color_palette[1] = color_palette_BW[1];
				color_palette[2] = color_palette_BW[2];
				color_palette[3] = color_palette_BW[3];
			}
			else
			{
				color_palette[0] = color_palette_Gr[0];
				color_palette[1] = color_palette_Gr[1];
				color_palette[2] = color_palette_Gr[2];
				color_palette[3] = color_palette_Gr[3];
			}

			if (_tracer.Enabled)
			{
				cpu.TraceCallback = s => _tracer.Put(s);
			}
			else
			{
				cpu.TraceCallback = null;
			}

			_frame++;

			if (controller.IsPressed("Power"))
			{
				HardReset();
			}

			_islag = true;

			GetControllerState(controller);

			do_frame();

			if (_scanlineCallback != null)
			{
				if (_scanlineCallbackLine == -1)
				{
					GetGPU();
					_scanlineCallback(ppu.LCDC);
				}
			}

			if (_islag)
			{
				_lagcount++;
			}

			return true;
		}

		public void do_frame()
		{
			// gameboy frames can be variable lengths
			// we want to end a frame when VBlank turns from false to true
			int ticker = 0;

			// check if new input changed the input register and triggered IRQ
			byte contr_prev = input_register;

			input_register &= 0xF0;
			if ((input_register & 0x30) == 0x20)
			{
				input_register |= (byte)(controller_state & 0xF);
			}
			else if ((input_register & 0x30) == 0x10)
			{
				input_register |= (byte)((controller_state & 0xF0) >> 4);
			}
			else if ((input_register & 0x30) == 0x00)
			{
				// if both polls are set, then a bit is zero if either or both pins are zero
				byte temp = (byte)((controller_state & 0xF) & ((controller_state & 0xF0) >> 4));
				input_register |= temp;
			}
			else
			{
				input_register |= 0xF;
			}

			// check for interrupts			
			if (((contr_prev & 8) > 0) && ((input_register & 8) == 0) ||
				((contr_prev & 4) > 0) && ((input_register & 4) == 0) ||
				((contr_prev & 2) > 0) && ((input_register & 2) == 0) ||
				((contr_prev & 1) > 0) && ((input_register & 1) == 0))
			{
				if (REG_FFFF.Bit(4)) { cpu.FlagI = true; }
				REG_FF0F |= 0x10;
			}
			
			while (!vblank_rise)
			{
				// These things do not change speed in GBC double spped mode
				audio.tick();
				ppu.tick();
				if (Use_MT) { mapper.Mapper_Tick(); }

				if (!HDMA_transfer)
				{
					// These things all tick twice as fast in GBC double speed mode
					ppu.DMA_tick();
					timer.tick_1();
					serialport.serial_transfer_tick();					
					cpu.ExecuteOne(ref REG_FF0F, REG_FFFF);
					timer.tick_2();

					if (double_speed)
					{
						ppu.DMA_tick();
						timer.tick_1();
						serialport.serial_transfer_tick();
						cpu.ExecuteOne(ref REG_FF0F, REG_FFFF);
						timer.tick_2();
					}
				}
				else
				{
					timer.tick_1();
					timer.tick_2();
					cpu.TotalExecutedCycles++;
					if (double_speed)
					{
						timer.tick_1();
						timer.tick_2();
						cpu.TotalExecutedCycles++;
					}
				}

				if (in_vblank && !in_vblank_old)
				{
					vblank_rise = true;
				}

				ticker++;
				if (ticker > 42134400) { throw new Exception("ERROR: Unable to Resolve Frame"); }

				in_vblank_old = in_vblank;
				REG_FF0F_OLD = REG_FF0F;
			}

			vblank_rise = false;			
		}

		public void do_single_step()
		{
			// These things do not change speed in GBC double spped mode
			audio.tick();
			ppu.tick();
			if (Use_MT) { mapper.Mapper_Tick(); }

			if (!HDMA_transfer)
			{
				// These things all tick twice as fast in GBC double speed mode
				ppu.DMA_tick();
				timer.tick_1();
				serialport.serial_transfer_tick();
				cpu.ExecuteOne(ref REG_FF0F, REG_FFFF);
				timer.tick_2();

				if (double_speed)
				{
					ppu.DMA_tick();
					timer.tick_1();
					serialport.serial_transfer_tick();
					cpu.ExecuteOne(ref REG_FF0F, REG_FFFF);
					timer.tick_2();
				}
			}
			else
			{
				timer.tick_1();
				timer.tick_2();
				cpu.TotalExecutedCycles++;
				if (double_speed)
				{
					timer.tick_1();
					timer.tick_2();
					cpu.TotalExecutedCycles++;
				}
			}

			if (in_vblank && !in_vblank_old)
			{
				vblank_rise = true;
			}

			in_vblank_old = in_vblank;
			REG_FF0F_OLD = REG_FF0F;
		}

		public void do_controller_check()
		{
			// check if new input changed the input register and triggered IRQ
			byte contr_prev = input_register;

			input_register &= 0xF0;
			if ((input_register & 0x30) == 0x20)
			{
				input_register |= (byte)(controller_state & 0xF);
			}
			else if ((input_register & 0x30) == 0x10)
			{
				input_register |= (byte)((controller_state & 0xF0) >> 4);
			}
			else if ((input_register & 0x30) == 0x00)
			{
				// if both polls are set, then a bit is zero if either or both pins are zero
				byte temp = (byte)((controller_state & 0xF) & ((controller_state & 0xF0) >> 4));
				input_register |= temp;
			}
			else
			{
				input_register |= 0xF;
			}

			// check for interrupts			
			if (((contr_prev & 8) > 0) && ((input_register & 8) == 0) ||
				((contr_prev & 4) > 0) && ((input_register & 4) == 0) ||
				((contr_prev & 2) > 0) && ((input_register & 2) == 0) ||
				((contr_prev & 1) > 0) && ((input_register & 1) == 0))
			{
				if (REG_FFFF.Bit(4)) { cpu.FlagI = true; }
				REG_FF0F |= 0x10;
			}
		}

		// Switch Speed (GBC only)
		public int SpeedFunc(int temp)
		{
			if (is_GBC)
			{
				if (speed_switch)
				{
					speed_switch = false;

					int ret = double_speed ? 50000 : 25000; // actual time needs checking
					double_speed = !double_speed;
					return ret;
				}

				// if we are not switching speed, return 0
				return 0;
			}

			// if we are in GB mode, return 0 indicating not switching speed
			return 0;
		}

		public void GetControllerState(IController controller)
		{
			InputCallbacks.Call();
			controller_state = _controllerDeck.ReadPort1(controller);

			Acc_X_state = _controllerDeck.ReadAccX1(controller);
			Acc_Y_state = _controllerDeck.ReadAccY1(controller);
		}

		public int Frame => _frame;

		public string SystemId => "GB"; 

		public bool DeterministicEmulation { get; set; }

		public void ResetCounters()
		{
			_frame = 0;
			_lagcount = 0;
			_islag = false;
		}

		public CoreComm CoreComm { get; }

		public void Dispose()
		{
			Marshal.FreeHGlobal(iptr0);
			Marshal.FreeHGlobal(iptr1);
			Marshal.FreeHGlobal(iptr2);
			Marshal.FreeHGlobal(iptr3);

			audio.DisposeSound();
		}

		#region Video provider

		public int _frameHz = 60;

		public int[] _vidbuffer;

		public int[] GetVideoBuffer()
		{
			if (ppu.blank_frame)
			{
				for (int i = 0; i < _vidbuffer.Length; i++)
				{
					_vidbuffer[i] = (int)color_palette[0];
				}
				ppu.blank_frame = false;
			}
			return _vidbuffer;		
		}

		public int VirtualWidth => 160;
		public int VirtualHeight => 144;
		public int BufferWidth => 160;
		public int BufferHeight => 144;
		public int BackgroundColor => unchecked((int)0xFF000000);
		public int VsyncNumerator => _frameHz;
		public int VsyncDenominator => 1;

		public static readonly uint[] color_palette_BW = { 0xFFFFFFFF , 0xFFAAAAAA, 0xFF555555, 0xFF000000 };
		public static readonly uint[] color_palette_Gr = { 0xFFA4C505, 0xFF88A905, 0xFF1D551D, 0xFF052505 };

		public uint[] color_palette = new uint[4];

		#endregion
	}
}
