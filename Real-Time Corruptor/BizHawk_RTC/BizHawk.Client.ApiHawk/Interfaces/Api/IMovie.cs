﻿using System.Collections.Generic;
namespace BizHawk.Client.ApiHawk
{
	public interface IMovie : IExternalApi
	{
		bool StartsFromSavestate();
		bool StartsFromSaveram();
		string Filename();
		Dictionary<string, dynamic> GetInput(int frame);
		string GetInputAsMnemonic(int frame);
		bool GetReadOnly();
		ulong GetRerecordCount();
		bool GetRerecordCounting();
		bool IsLoaded();
		double Length();
		string Mode();
		void Save(string filename = "");
		void SetReadOnly(bool readOnly);
		void SetRerecordCount(double count);
		void SetRerecordCounting(bool counting);
		void Stop();
		double GetFps();
		Dictionary<string, string> GetHeader();
		List<string> GetComments();
		List<string> GetSubtitles();
	}
}
