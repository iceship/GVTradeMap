﻿/*-------------------------------------------------------------------------

 ユニークな文字管理

---------------------------------------------------------------------------*/

/*-------------------------------------------------------------------------
 using
---------------------------------------------------------------------------*/
using System.Collections.Generic;

/*-------------------------------------------------------------------------

---------------------------------------------------------------------------*/
namespace useful
{
	/*-------------------------------------------------------------------------

	---------------------------------------------------------------------------*/
	public class unique_charactor
	{
		private Dictionary<char, int>			m_unique_tbl;

		/*-------------------------------------------------------------------------

		---------------------------------------------------------------------------*/
		public int count			{		get{	return m_unique_tbl.Count;	}}

		/*-------------------------------------------------------------------------

		---------------------------------------------------------------------------*/
		public unique_charactor()
		{
			m_unique_tbl	= new Dictionary<char,int>();
		}

		/*-------------------------------------------------------------------------
		 初期化
		---------------------------------------------------------------------------*/
		public void Initialize()
		{
			m_unique_tbl.Clear();
		}
	
		/*-------------------------------------------------------------------------
		 追加
		---------------------------------------------------------------------------*/
		public void AddText(string str)
		{
			if(str == null)	return;
			if(str == "")	return;
			foreach(char c in str){
				if(m_unique_tbl.ContainsKey(c))	continue;	// すでに含まれている
				m_unique_tbl.Add(c, 1);
			}
		}

		/*-------------------------------------------------------------------------
		 内容を文字列で得る
		---------------------------------------------------------------------------*/
		public string GetString()
		{
			string	str	= "";
			foreach(KeyValuePair<char, int> c in m_unique_tbl){
				str		+= c.Key;
			}
			return str;
		}
	}
}
