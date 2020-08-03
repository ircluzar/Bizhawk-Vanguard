﻿using RTCV.CorruptCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTCV.BizhawkVanguard
{
	public class VanguardRealTimeEvents : RTCV.CorruptCore.IRealTime
	{
		public bool SupportsRewind { get; set; } = true;
		public bool SupportsForwarding { get; set; } = true;
		public bool SupportsFastForwarding { get; set; } = true;

		public event EventHandler<RealTimeEventArgs> Step;
		public event EventHandler GameLoaded;
		public event EventHandler GameClosed;

		public void ON_STEP(bool _isForwarding, bool _isRewinding, bool _isFastForwarding)
		{
			Step?.Invoke(this, new RealTimeEventArgs()
			{
				isForwarding = _isForwarding,
				isRewinding = _isRewinding,
				isFastForwarding = _isFastForwarding,
			});

		}

		public void LOAD_GAME() => GameLoaded?.Invoke(this, new EventArgs());
		public void GAME_CLOSED() => GameClosed?.Invoke(this, new EventArgs());
	}
}
