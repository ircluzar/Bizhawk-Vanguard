﻿using System.Collections.Generic;

namespace BizHawk.Client.ApiHawk
{
	public interface IGameInfo : IExternalApi
	{
		string GetRomName();
		string GetRomHash();
		bool InDatabase();
		string GetStatus();
		bool IsStatusBad();
		string GetBoardType();
		Dictionary<string, string> GetOptions();
	}
}
