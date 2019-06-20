﻿using BizHawk.Common;
using System.Collections.Generic;

namespace BizHawk.Emulation.Cores.Computers.SinclairSpectrum
{
    /// <summary>
    /// The NEC floppy disk controller (and floppy drive) found in the +3
    /// </summary>
    #region Attribution
    /*
        Implementation based on the information contained here:
        http://www.cpcwiki.eu/index.php/765_FDC
        and here:
        http://www.cpcwiki.eu/imgs/f/f3/UPD765_Datasheet_OCRed.pdf
    */
    #endregion
    public partial class NECUPD765
    {
        #region Devices

        /// <summary>
        /// The emulated spectrum machine
        /// </summary>
        private SpectrumBase _machine;

        #endregion

        #region Construction & Initialization

        /// <summary>
        /// Main constructor
        /// </summary>
        public NECUPD765()
        {            
            InitCommandList();
        }

        /// <summary>
        /// Initialization routine
        /// </summary>
        public void Init(SpectrumBase machine)
        {
            _machine = machine;
            FDD_Init();
            TimingInit();
            Reset();
        }
        
        /// <summary>
        /// Resets the FDC
        /// </summary>
        public void Reset()
        {
            // setup main status
            StatusMain = 0;

            Status0 = 0;
            Status1 = 0;
            Status2 = 0;
            Status3 = 0;

            SetBit(MSR_RQM, ref StatusMain);

            SetPhase_Idle();

            //FDC_FLAG_RQM = true;
            //ActiveDirection = CommandDirection.IN;
            SRT = 6;
            HUT = 16;
            HLT = 2;
            HLT_Counter = 0;
            HUT_Counter = 0;
            IndexPulseCounter = 0;
            CMD_FLAG_MF = false;

            foreach (var d in DriveStates)
            {
                //d.SeekingTrack = d.CurrentTrack;
                ////d.SeekCounter = 0;
                //d.FLAG_SEEK_INTERRUPT = false;
                //d.IntStatus = 0;
                //d.SeekState = SeekSubState.Idle;
                //d.SeekIntState = SeekIntStatus.Normal;

            }
            
        }

        /// <summary>
        /// Setup the command structure
        /// Each command represents one of the internal UPD765 commands
        /// </summary>
        private void InitCommandList()
        {
            CommandList = new List<Command>
            {
                // read data
                new Command { CommandDelegate = UPD_ReadData, CommandCode = 0x06, MT = true, MF = true, SK = true, IsRead = true,
                    Direction = CommandDirection.OUT, ParameterByteCount = 8, ResultByteCount = 7 },
                // read id
                new Command { CommandDelegate = UPD_ReadID, CommandCode = 0x0a, MF = true, IsRead = true,
                    Direction = CommandDirection.OUT, ParameterByteCount = 1, ResultByteCount = 7 },
                // specify
                new Command { CommandDelegate = UPD_Specify, CommandCode = 0x03,
                    Direction = CommandDirection.OUT, ParameterByteCount = 2, ResultByteCount = 0 },
                // read diagnostic
                new Command { CommandDelegate = UPD_ReadDiagnostic, CommandCode = 0x02, MF = true, SK = true, IsRead = true,
                    Direction = CommandDirection.OUT, ParameterByteCount = 8, ResultByteCount = 7 },
                // scan equal
                new Command { CommandDelegate = UPD_ScanEqual, CommandCode = 0x11, MT = true, MF = true, SK = true, IsRead = true,
                    Direction = CommandDirection.IN, ParameterByteCount = 8, ResultByteCount = 7 },
                // scan high or equal
                new Command { CommandDelegate = UPD_ScanHighOrEqual, CommandCode = 0x1d, MT = true, MF = true, SK = true, IsRead = true,
                    Direction = CommandDirection.IN, ParameterByteCount = 8, ResultByteCount = 7 },
                // scan low or equal
                new Command { CommandDelegate = UPD_ScanLowOrEqual, CommandCode = 0x19, MT = true, MF = true, SK = true, IsRead = true,
                    Direction = CommandDirection.IN, ParameterByteCount = 8, ResultByteCount = 7 },
                // read deleted data
                new Command { CommandDelegate = UPD_ReadDeletedData, CommandCode = 0x0c, MT = true, MF = true, SK = true, IsRead = true,
                    Direction = CommandDirection.OUT, ParameterByteCount = 8, ResultByteCount = 7 },
                // write data
                new Command { CommandDelegate = UPD_WriteData, CommandCode = 0x05, MT = true, MF = true, IsWrite = true,
                    Direction = CommandDirection.IN, ParameterByteCount = 8, ResultByteCount = 7 },
                // write id
                new Command { CommandDelegate = UPD_WriteID, CommandCode = 0x0d, MF = true, IsWrite = true,
                    Direction = CommandDirection.IN, ParameterByteCount = 5, ResultByteCount = 7 },
                // write deleted data
                new Command { CommandDelegate = UPD_WriteDeletedData, CommandCode = 0x09, MT = true, MF = true, IsWrite = true,
                    Direction = CommandDirection.IN, ParameterByteCount = 8, ResultByteCount = 7 },
                // seek
                new Command { CommandDelegate = UPD_Seek, CommandCode = 0x0f,
                    Direction = CommandDirection.OUT, ParameterByteCount = 2, ResultByteCount = 0 },
                // recalibrate (seek track00)
                new Command { CommandDelegate = UPD_Recalibrate, CommandCode = 0x07,
                    Direction = CommandDirection.OUT, ParameterByteCount = 1, ResultByteCount = 0 },
                // sense interrupt status
                new Command { CommandDelegate = UPD_SenseInterruptStatus, CommandCode = 0x08,
                    Direction = CommandDirection.OUT, ParameterByteCount = 0, ResultByteCount = 2 },
                // sense drive status
                new Command { CommandDelegate = UPD_SenseDriveStatus, CommandCode = 0x04,
                    Direction = CommandDirection.OUT, ParameterByteCount = 1, ResultByteCount = 1 },
                // version
                new Command { CommandDelegate = UPD_Version, CommandCode = 0x10,
                    Direction = CommandDirection.OUT, ParameterByteCount = 0, ResultByteCount = 1 },
                // invalid
                new Command { CommandDelegate = UPD_Invalid, CommandCode = 0x00,
                    Direction = CommandDirection.OUT, ParameterByteCount = 0, ResultByteCount = 1 },
            };
        }

        #endregion        

        #region State Serialization

        public void SyncState(Serializer ser)
        {
            ser.BeginSection("NEC-UPD765");

            #region FDD
            
            ser.Sync(nameof(FDD_FLAG_MOTOR), ref FDD_FLAG_MOTOR);

            for (int i = 0; i < 4; i++)
            {
                ser.BeginSection("HITDrive_" + i);
                DriveStates[i].SyncState(ser);
                ser.EndSection();
            }

            ser.Sync(nameof(DiskDriveIndex), ref _diskDriveIndex);
            // set active drive
            DiskDriveIndex = _diskDriveIndex;

            #endregion

            #region Registers

            ser.Sync("_RegMain", ref StatusMain);
            ser.Sync("_Reg0", ref Status0);
            ser.Sync("_Reg1", ref Status1);
            ser.Sync("_Reg2", ref Status2);
            ser.Sync("_Reg3", ref Status3);

            #endregion

            #region Controller state

            ser.Sync(nameof(DriveLight), ref DriveLight);
            ser.SyncEnum(nameof(ActivePhase), ref ActivePhase);
            //ser.SyncEnum(nameof(ActiveDirection), ref ActiveDirection);
            ser.SyncEnum(nameof(ActiveInterrupt), ref ActiveInterrupt);
            ser.Sync(nameof(CommBuffer), ref CommBuffer, false);
            ser.Sync(nameof(CommCounter), ref CommCounter);
            ser.Sync(nameof(ResBuffer), ref ResBuffer, false);
            ser.Sync(nameof(ExecBuffer), ref ExecBuffer, false);
            ser.Sync(nameof(ExecCounter), ref ExecCounter);
            ser.Sync(nameof(ExecLength), ref ExecLength);
            ser.Sync(nameof(InterruptResultBuffer), ref InterruptResultBuffer, false);
            ser.Sync(nameof(ResCounter), ref ResCounter);
            ser.Sync(nameof(ResLength), ref ResLength);            
            ser.Sync(nameof(LastSectorDataWriteByte), ref LastSectorDataWriteByte);
            ser.Sync(nameof(LastSectorDataReadByte), ref LastSectorDataReadByte);
            ser.Sync(nameof(LastByteReceived), ref LastByteReceived);
            
            ser.Sync(nameof(_cmdIndex), ref _cmdIndex);
            // resync the ActiveCommand
            CMDIndex = _cmdIndex;

            ActiveCommandParams.SyncState(ser);
            
            ser.Sync(nameof(IndexPulseCounter), ref IndexPulseCounter);
            //ser.SyncEnum(nameof(_activeStatus), ref _activeStatus);
            //ser.SyncEnum(nameof(_statusRaised), ref _statusRaised);

            ser.Sync(nameof(CMD_FLAG_MT), ref CMD_FLAG_MT);
            ser.Sync(nameof(CMD_FLAG_MF), ref CMD_FLAG_MF);
            ser.Sync(nameof(CMD_FLAG_SK), ref CMD_FLAG_SK);
            ser.Sync(nameof(SRT), ref SRT);
            ser.Sync(nameof(HUT), ref HUT);
            ser.Sync(nameof(HLT), ref HLT);
            ser.Sync(nameof(ND), ref ND);
            ser.Sync(nameof(SRT_Counter), ref SRT_Counter);
            ser.Sync(nameof(HUT_Counter), ref HUT_Counter);
            ser.Sync(nameof(HLT_Counter), ref HLT_Counter);

            ser.Sync(nameof(SectorDelayCounter), ref SectorDelayCounter);
            ser.Sync(nameof(SectorID), ref SectorID);

            #endregion

            #region Timing

            ser.Sync(nameof(LastCPUCycle), ref LastCPUCycle);
            ser.Sync(nameof(StatusDelay), ref StatusDelay);
            ser.Sync(nameof(TickCounter), ref TickCounter);
            ser.Sync(nameof(DriveCycleCounter), ref DriveCycleCounter);

            #endregion

            ser.EndSection();
        }

        #endregion
    }
}
