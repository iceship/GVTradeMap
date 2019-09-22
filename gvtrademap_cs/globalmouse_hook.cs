﻿/*-------------------------------------------------------------------------

 mouse hook

---------------------------------------------------------------------------*/

/*-------------------------------------------------------------------------
 
---------------------------------------------------------------------------*/
using System;
using System.Collections.Generic;
using System.Text;
using win32;
using System.Windows.Forms;
using System.Runtime.InteropServices;

/*-------------------------------------------------------------------------
 
---------------------------------------------------------------------------*/
namespace gvtrademap_cs
{
	/*-------------------------------------------------------------------------
 
	---------------------------------------------------------------------------*/
	class globalmouse_hook : IDisposable
	{
		public enum SendKeyType{
			TOGGLE_SKILL	= 1,	// 스킬パネル開閉			Ctrl+Q
			TOGGLE_CUSTOM_SLOT,		// カスタムス행운권を開く		Ctrl+Z
			OPEN_ITEM_WINDOW,		// 아이템윈도우を開く	Ctrl+W
		};
	
		private IntPtr					m_handle;		// DLLのハンドル

		// DLL내の関수
		private delegate int SetDolMouseHookEx(int xbutton1_keytype, int xbutton2_keytype);
		private delegate int UnhookDolMouseHook();
	
		/*-------------------------------------------------------------------------
		 초기화
		 마우스훅を開始する
		---------------------------------------------------------------------------*/
		public globalmouse_hook()
			: this(SendKeyType.TOGGLE_SKILL, SendKeyType.OPEN_ITEM_WINDOW)
		{
		}
		public globalmouse_hook(SendKeyType xbutton1, SendKeyType xbutton2)
		{
			m_handle		= kernel32.LoadLibrary("mousehook.dll");
			if(m_handle == IntPtr.Zero){
				MessageBox.Show("mousehook.dll 의 읽기에 실패");
				return;
			}

			IntPtr	func	= kernel32.GetProcAddress(m_handle, "SetDolMouseHookEx");
			if(func == IntPtr.Zero){
				MessageBox.Show("SetDolMouseHookEx() 의 주소 획득 실패");
				return;
			}

			SetDolMouseHookEx	setDolMouseHook = (SetDolMouseHookEx)Marshal.GetDelegateForFunctionPointer(func, typeof(SetDolMouseHookEx));
			setDolMouseHook((int)xbutton1, (int)xbutton2);
		}

		/*-------------------------------------------------------------------------
		 
		---------------------------------------------------------------------------*/
		~globalmouse_hook()
		{
			Dispose();
		}

		/*-------------------------------------------------------------------------
		 훅されていれば종료させる
		---------------------------------------------------------------------------*/
		public void Dispose()
		{
			if(m_handle == IntPtr.Zero){
				return;
			}

			IntPtr	func	= kernel32.GetProcAddress(m_handle, "UnhookDolMouseHook");
			if(func == IntPtr.Zero){
				MessageBox.Show("UnhookDolMouseHook() 의 주소 획득에 실패");
				return;
			}

			UnhookDolMouseHook	unhookDolMouseHook = (UnhookDolMouseHook)Marshal.GetDelegateForFunctionPointer(func, typeof(UnhookDolMouseHook));
			unhookDolMouseHook();

			kernel32.FreeLibrary(m_handle);
			m_handle		= IntPtr.Zero;
		}
	}
}
