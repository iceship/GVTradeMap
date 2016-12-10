﻿/*-------------------------------------------------------------------------

 想定外エラー

---------------------------------------------------------------------------*/

/*-------------------------------------------------------------------------
 using
---------------------------------------------------------------------------*/
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

/*-------------------------------------------------------------------------

---------------------------------------------------------------------------*/
namespace gvtrademap_cs
{
	/*-------------------------------------------------------------------------

	---------------------------------------------------------------------------*/
	public partial class error_form : Form
	{
		private string				m_message;
	
		/*-------------------------------------------------------------------------

		---------------------------------------------------------------------------*/
		public error_form(string error_message)
		{
			m_message				= error_message;

			InitializeComponent();

			textBox1.AcceptsReturn	= true;
			textBox1.Lines			= error_message.Split(new char[]{'\n'});
			textBox1.Select(0, 0);
		}

		/*-------------------------------------------------------------------------
		 エラー内容をクリップボードにコピーする
		---------------------------------------------------------------------------*/
		private void button3_Click(object sender, EventArgs e)
		{
			Clipboard.SetText(m_message);
		}

		/*-------------------------------------------------------------------------
		 エラー報告を行うページを開く
		---------------------------------------------------------------------------*/
		private void button2_Click(object sender, EventArgs e)
		{
			Process.Start(def.URL4);
		}
	}
}
